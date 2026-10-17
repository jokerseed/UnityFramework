using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Framework.Events;
using Framework.Logging;
using KcpCSharp;

namespace Framework.Network
{
    /// <summary>
    /// KCP 频道：一条 KCP 消息就是一个完整包（包头 + 包体，格式由 <see cref="INetworkChannelHelper"/> 决定，与 TCP / WebSocket 相同）。
    /// 数据报回调可能来自任意线程，只入队；KCP 状态机、消息和事件都在主线程 <see cref="Update"/> 中推进。
    /// WebGL / 小游戏没有 UDP，构造时抛 <see cref="PlatformNotSupportedException"/>。
    /// <para>
    /// UDP 没有连接：建好 socket 即为 Connected，服务器不可达不会立刻报错。断线靠三道检测：
    /// KCP 分片重传达到上限（死链）时关闭；开启心跳后连续丢失 <see cref="MaxMissHeartBeatCount"/> 次时关闭；
    /// <see cref="ResponseTimeoutSeconds"/> 大于 0 时，连续这么久收不到对端任何数据报也关闭。
    /// 握手需要服务端协议配合，框架不强制；联机项目建议开启心跳并设置 ResponseTimeoutSeconds。
    /// </para>
    /// </summary>
    public sealed class KcpNetworkChannel : INetworkChannel, INetworkChannelDriver, IKcpDatagramListener
    {
        const int MaxPacketSize = 1024 * 1024;

        readonly object _sync = new object();
        readonly Queue<PendingDispatch> _pending = new Queue<PendingDispatch>(32);
        readonly Queue<ArraySegment<byte>> _incoming = new Queue<ArraySegment<byte>>(32);
        readonly List<ArraySegment<byte>> _drain = new List<ArraySegment<byte>>(8);
        readonly ByteBuf _inputBuf = new ByteBuf(0);
        readonly ByteBuf _sendBuf = new ByteBuf(0);
        ByteBuf _receiveBuf = new ByteBuf(4096);
        int _connectGeneration;
        float _silenceElapsed;
        readonly Dictionary<ushort, Action<INetworkChannel, NetworkPacket>> _handlers =
            new Dictionary<ushort, Action<INetworkChannel, NetworkPacket>>(32);
        readonly INetworkChannelHelper _helper;
        readonly Func<IKcpDatagramListener, IKcpDatagramTransport> _transportFactory;
        readonly MemoryStream _serializeStream = new MemoryStream(256);

        IKcpDatagramTransport _transport;
        Kcp _kcp;
        Action<INetworkChannel, NetworkPacket> _defaultHandler;
        float _heartBeatElapse;
        int _missHeartBeatCount;
        int _sentPacketCount;
        int _receivedPacketCount;
        bool _needFlush;
        bool _pumping;
        NetworkChannelState _state = NetworkChannelState.Disconnected;

        /// <summary>使用 UDP 传输创建频道。两端的 <paramref name="conversation"/> 必须相同。</summary>
        /// <param name="name">频道名称。</param>
        /// <param name="helper">协议辅助器，不可为 null。</param>
        /// <param name="conversation">KCP 会话号。</param>
        /// <exception cref="ArgumentNullException"><paramref name="helper"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><see cref="INetworkChannelHelper.PacketHeaderLength"/> 小于等于 0。</exception>
        /// <exception cref="PlatformNotSupportedException">WebGL / 小游戏没有 UDP。</exception>
        public KcpNetworkChannel(string name, INetworkChannelHelper helper, int conversation = 0)
            : this(name, helper, DefaultTransportFactory(), conversation)
        {
        }

        /// <summary>使用自定义数据报传输创建频道（测试替身，或非 UDP 的中继）。</summary>
        /// <param name="name">频道名称。</param>
        /// <param name="helper">协议辅助器，不可为 null。</param>
        /// <param name="transportFactory">每次连接调用一次，返回新的传输实例，不可为 null。</param>
        /// <param name="conversation">KCP 会话号。两端必须相同。</param>
        /// <exception cref="ArgumentNullException"><paramref name="helper"/> 或 <paramref name="transportFactory"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><see cref="INetworkChannelHelper.PacketHeaderLength"/> 小于等于 0。</exception>
        /// <exception cref="PlatformNotSupportedException">WebGL / 小游戏没有 UDP。</exception>
        public KcpNetworkChannel(
            string name,
            INetworkChannelHelper helper,
            Func<IKcpDatagramListener, IKcpDatagramTransport> transportFactory,
            int conversation)
        {
            EnsureUdpSupported();
            Name = name ?? string.Empty;
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
            if (_helper.PacketHeaderLength <= 0)
            {
                throw new ArgumentException("PacketHeaderLength must be greater than 0.", nameof(helper));
            }

            Conversation = conversation;
        }

        /// <summary>KCP 会话号。连接两端必须使用同一个值。</summary>
        public int Conversation { get; }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public NetworkChannelState State
        {
            get
            {
                lock (_sync)
                {
                    return _state;
                }
            }
        }

        /// <inheritdoc />
        public bool Connected
        {
            get
            {
                lock (_sync)
                {
                    return _state == NetworkChannelState.Connected;
                }
            }
        }

        /// <inheritdoc />
        public NetworkServiceType ServiceType => NetworkServiceType.Kcp;

        /// <inheritdoc />
        public float HeartBeatInterval { get; set; }

        /// <inheritdoc />
        public bool ResetHeartBeatElapseSecondsWhenReceivePacket { get; set; } = true;

        /// <inheritdoc />
        public int MissHeartBeatCount
        {
            get
            {
                lock (_sync)
                {
                    return _missHeartBeatCount;
                }
            }
        }

        /// <inheritdoc />
        public int MaxMissHeartBeatCount { get; set; } = 3;

        /// <summary>连续这么多秒收不到对端任何数据报即断开（报 <see cref="NetworkErrorCode.HeartBeatTimeout"/>）；小于等于 0 关闭此检测（默认）。</summary>
        public float ResponseTimeoutSeconds { get; set; }

        /// <inheritdoc />
        public int SentPacketCount
        {
            get
            {
                lock (_sync)
                {
                    return _sentPacketCount;
                }
            }
        }

        /// <inheritdoc />
        public int ReceivedPacketCount
        {
            get
            {
                lock (_sync)
                {
                    return _receivedPacketCount;
                }
            }
        }

        /// <inheritdoc />
        public void Connect(string host, int port)
        {
            if (string.IsNullOrEmpty(host))
            {
                EnqueueError(NetworkErrorCode.AddressError, SocketError.AddressNotAvailable, "Host is empty.");
                return;
            }

            if (IPAddress.TryParse(host, out var literal))
            {
                Connect(literal, port);
                return;
            }

            if (!TryBeginConnecting(port, out var generation))
            {
                return;
            }

            // 后台解析域名，不阻塞主线程。
            Dns.GetHostAddressesAsync(host).ContinueWith(
                task =>
                {
                    var address = task.Status == TaskStatus.RanToCompletion ? SelectAddress(task.Result) : null;
                    if (address == null)
                    {
                        lock (_sync)
                        {
                            if (generation != _connectGeneration || _state != NetworkChannelState.Connecting)
                            {
                                return;
                            }

                            _state = NetworkChannelState.Disconnected;
                        }

                        var message = task.Exception != null
                            ? task.Exception.GetBaseException().Message
                            : $"No usable address for {host}.";
                        EnqueueError(NetworkErrorCode.AddressError, SocketError.HostNotFound, message);
                        return;
                    }

                    StartTransport(address, port, generation);
                },
                TaskScheduler.Default);
        }

        /// <inheritdoc />
        public void Connect(IPAddress address, int port)
        {
            if (address == null)
            {
                EnqueueError(NetworkErrorCode.AddressError, SocketError.AddressNotAvailable, "Address is null.");
                return;
            }

            if (TryBeginConnecting(port, out var generation))
            {
                StartTransport(address, port, generation);
            }
        }

        /// <summary>校验端口与状态，进入 Connecting 并分配一个连接代号；失败时已入队错误。</summary>
        bool TryBeginConnecting(int port, out int generation)
        {
            generation = 0;
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            {
                EnqueueError(NetworkErrorCode.AddressError, SocketError.AddressNotAvailable, $"Invalid port: {port}.");
                return false;
            }

            lock (_sync)
            {
                if (_state != NetworkChannelState.Disconnected)
                {
                    EnqueueError(NetworkErrorCode.ConnectError, SocketError.IsConnected, "Channel is already connecting or connected.");
                    return false;
                }

                _state = NetworkChannelState.Connecting;
                generation = ++_connectGeneration;
                return true;
            }
        }

        void StartTransport(IPAddress address, int port, int generation)
        {
            lock (_sync)
            {
                if (generation != _connectGeneration || _state != NetworkChannelState.Connecting)
                {
                    // 解析期间被 Close 或重新 Connect 了。
                    return;
                }
            }

            IKcpDatagramTransport transport;
            try
            {
                transport = _transportFactory(this);
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    _state = NetworkChannelState.Disconnected;
                }

                EnqueueError(NetworkErrorCode.ConnectError, SocketError.SocketError, ex.Message);
                return;
            }

            if (transport == null)
            {
                lock (_sync)
                {
                    _state = NetworkChannelState.Disconnected;
                }

                EnqueueError(NetworkErrorCode.ConnectError, SocketError.SocketError, "Transport factory returned null.");
                return;
            }

            var kcp = new Kcp(Conversation, new DatagramOutput(this), null);
            kcp.SetStream(false);
            kcp.NoDelay(1, 10, 2, 1);
            kcp.WndSize(128, 128);
            kcp.SetACKNoDelay(true);
            kcp.SetMaxRto(3000);

            lock (_sync)
            {
                if (_state != NetworkChannelState.Connecting || generation != _connectGeneration)
                {
                    transport.Close();
                    if (generation == _connectGeneration)
                    {
                        _state = NetworkChannelState.Disconnected;
                    }

                    EnqueueError(NetworkErrorCode.ConnectError, SocketError.Shutdown, "Channel closed while connecting.");
                    return;
                }

                _transport = transport;
                _kcp = kcp;
                _state = NetworkChannelState.Connected;
                _silenceElapsed = 0f;
            }

            try
            {
                transport.Start(new IPEndPoint(address, port));
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    if (_transport == transport)
                    {
                        ResetConnection();
                    }
                }

                transport.Close();
                EnqueueError(NetworkErrorCode.ConnectError, SocketError.SocketError, ex.Message);
                return;
            }

            Enqueue(new PendingDispatch { Kind = PendingKind.Connected });
        }

        /// <inheritdoc />
        public void Close()
        {
            if (DetachTransport())
            {
                Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
            }
        }

        /// <inheritdoc />
        public void Send(NetworkPacket packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            try
            {
                Kcp kcp;
                lock (_sync)
                {
                    kcp = _state == NetworkChannelState.Connected ? _kcp : null;
                }

                if (kcp == null)
                {
                    EnqueueError(NetworkErrorCode.SendError, SocketError.NotConnected, "Channel is not connected.");
                    return;
                }

                _serializeStream.SetLength(0);
                _serializeStream.Position = 0;
                if (!_helper.SerializePacket(packet, _serializeStream))
                {
                    EnqueueError(NetworkErrorCode.SendError, SocketError.SocketError, "Serialize packet failed.");
                    return;
                }

                var length = (int)_serializeStream.Length;
                if (length == 0 || length > MaxPacketSize)
                {
                    EnqueueError(NetworkErrorCode.SendError, SocketError.MessageSize, $"Packet size {length} is invalid.");
                    return;
                }

                // Kcp.Send 会把数据拷进分片，直接包装序列化缓冲即可，不必 ToArray。
                if (kcp.Send(_sendBuf.Wrap(_serializeStream.GetBuffer(), length)) < 0)
                {
                    EnqueueError(NetworkErrorCode.SendError, SocketError.SocketError, "KCP rejected the packet.");
                    return;
                }

                lock (_sync)
                {
                    _sentPacketCount++;
                }

                _needFlush = true;
                if (!_pumping)
                {
                    Pump();
                }
            }
            finally
            {
                global::Framework.MemoryPool.MemoryPool.Release(packet);
            }
        }

        /// <inheritdoc />
        public void Send(ushort messageId, byte[] payload)
        {
            Send(NetworkPacket.Create(messageId, payload));
        }

        /// <inheritdoc />
        public void RegisterHandler(INetworkPacketHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            SetHandler(handler.MessageId, handler.Handle);
        }

        /// <inheritdoc />
        public void RegisterHandler(ushort messageId, Action<INetworkChannel, NetworkPacket> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            SetHandler(messageId, handler);
        }

        /// <inheritdoc />
        public bool UnregisterHandler(ushort messageId)
        {
            return _handlers.Remove(messageId);
        }

        /// <inheritdoc />
        public void SetDefaultHandler(Action<INetworkChannel, NetworkPacket> handler)
        {
            _defaultHandler = handler;
        }

        /// <summary>主线程轮询：推进 KCP、分发收包、心跳。</summary>
        /// <param name="elapseSeconds">真实流逝时间（秒）。</param>
        public void Update(float elapseSeconds)
        {
            Pump();
            ProcessPending();
            UpdateResponseTimeout(elapseSeconds);
            UpdateHeartBeat(elapseSeconds);
            ProcessPending();
        }

        void UpdateResponseTimeout(float elapseSeconds)
        {
            float silence;
            lock (_sync)
            {
                if (_state != NetworkChannelState.Connected || ResponseTimeoutSeconds <= 0f)
                {
                    _silenceElapsed = 0f;
                    return;
                }

                _silenceElapsed += elapseSeconds;
                silence = _silenceElapsed;
            }

            if (silence >= ResponseTimeoutSeconds)
            {
                Fail(NetworkErrorCode.HeartBeatTimeout, $"No datagram from server for {ResponseTimeoutSeconds}s.");
            }
        }

        /// <summary>关闭频道，丢弃未分发的消息并清空处理器。</summary>
        public void Shutdown()
        {
            DetachTransport();
            var released = new List<NetworkPacket>();
            lock (_sync)
            {
                while (_pending.Count > 0)
                {
                    var item = _pending.Dequeue();
                    if (item.Packet != null)
                    {
                        released.Add(item.Packet);
                    }
                }
            }

            for (var i = 0; i < released.Count; i++)
            {
                global::Framework.MemoryPool.MemoryPool.Release(released[i]);
            }

            _handlers.Clear();
            _defaultHandler = null;
        }

        /// <inheritdoc />
        void IKcpDatagramListener.OnDatagram(byte[] data, int offset, int count)
        {
            if (data == null || count <= 0 || offset < 0 || offset + count > data.Length)
            {
                return;
            }

            // 传输层会复用接收缓冲，这里拷进从池里租的数组；KCP Input 之后在 Pump 里归还。
            var copy = ArrayPool<byte>.Shared.Rent(count);
            Buffer.BlockCopy(data, offset, copy, 0, count);
            lock (_sync)
            {
                if (_state == NetworkChannelState.Connected)
                {
                    _incoming.Enqueue(new ArraySegment<byte>(copy, 0, count));
                    return;
                }
            }

            ArrayPool<byte>.Shared.Return(copy);
        }

        /// <inheritdoc />
        void IKcpDatagramListener.OnTransportError(string message)
        {
            Enqueue(new PendingDispatch { Kind = PendingKind.TransportError, Message = message });
        }

        static Func<IKcpDatagramListener, IKcpDatagramTransport> DefaultTransportFactory()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new PlatformNotSupportedException(
                "KCP requires UDP and is not available on WebGL or mini-game players. Use WebSocket.");
#else
            return listener => new UdpKcpDatagramTransport(listener);
#endif
        }

        static void EnsureUdpSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new PlatformNotSupportedException(
                "KCP requires UDP and is not available on WebGL or mini-game players. Use WebSocket.");
#endif
        }

        static IPAddress SelectAddress(IPAddress[] addresses)
        {
            if (addresses == null || addresses.Length == 0)
            {
                return null;
            }

            for (var i = 0; i < addresses.Length; i++)
            {
                if (addresses[i] != null && addresses[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    return addresses[i];
                }
            }

            return addresses[0];
        }

        void Pump()
        {
            if (_pumping)
            {
                return;
            }

            _pumping = true;
            try
            {
                DrainIncoming();
                var kcp = _kcp;
                if (kcp == null)
                {
                    return;
                }

                if (_drain.Count > 0)
                {
                    lock (_sync)
                    {
                        _silenceElapsed = 0f;
                    }
                }

                for (var i = 0; i < _drain.Count; i++)
                {
                    var segment = _drain[i];
                    var code = kcp.Input(_inputBuf.Wrap(segment.Array, segment.Count));
                    if (code < 0)
                    {
                        Fail(NetworkErrorCode.ReceiveError, $"KCP input rejected ({code}).");
                        return;
                    }
                }

                var now = Environment.TickCount;
                kcp.SyncMillis(now);
                if (_needFlush || _drain.Count > 0)
                {
                    kcp.MarkFlushImmediate(now);
                    _needFlush = false;
                }

                kcp.Update(now);
                if (kcp.IsDeadLink)
                {
                    // 某个分片重传达到上限：对端不可达。UDP 本身不会报错，不检查就会一直显示已连接。
                    Fail(NetworkErrorCode.HeartBeatTimeout, "KCP dead link: peer unreachable.");
                    return;
                }

                PullMessages(kcp);
            }
            catch (Exception ex)
            {
                Fail(NetworkErrorCode.ReceiveError, ex.Message);
            }
            finally
            {
                ReturnSegments(_drain);
                _drain.Clear();
                _pumping = false;
            }
        }

        void DrainIncoming()
        {
            ReturnSegments(_drain);
            _drain.Clear();
            lock (_sync)
            {
                while (_incoming.Count > 0)
                {
                    _drain.Add(_incoming.Dequeue());
                }
            }
        }

        static void ReturnSegments(List<ArraySegment<byte>> segments)
        {
            for (var i = 0; i < segments.Count; i++)
            {
                if (segments[i].Array != null)
                {
                    ArrayPool<byte>.Shared.Return(segments[i].Array);
                }
            }
        }

        void PullMessages(Kcp kcp)
        {
            while (true)
            {
                var size = kcp.PeekSize();
                if (size < 0)
                {
                    return;
                }

                if (size == 0 || size > MaxPacketSize)
                {
                    Fail(NetworkErrorCode.DeserializeError, $"KCP message size {size} is invalid.");
                    return;
                }

                // 复用同一个接收缓冲（容量会按需增长）；DeserializePacket 会把包体拷出去。
                var message = _receiveBuf.Clear();
                if (kcp.Receive(message) < 0)
                {
                    Fail(NetworkErrorCode.DeserializeError, "KCP receive failed.");
                    return;
                }

                var raw = message.GetRaw();
                var length = message.ReadableBytes();
                var headerLength = _helper.PacketHeaderLength;
                var bodyLength = length >= headerLength ? _helper.ParsePacketLength(raw, message.ReaderIndex()) : -1;
                if (bodyLength < 0 || bodyLength > MaxPacketSize || headerLength + bodyLength != length)
                {
                    Fail(NetworkErrorCode.DeserializeError,
                        $"Message length {length} does not match one packet (header {headerLength}, body {bodyLength}).");
                    return;
                }

                var packet = _helper.DeserializePacket(raw, message.ReaderIndex() + headerLength, bodyLength);
                if (packet == null)
                {
                    Fail(NetworkErrorCode.DeserializeError, "Deserialize packet failed.");
                    return;
                }

                Enqueue(new PendingDispatch { Kind = PendingKind.Packet, Packet = packet });
            }
        }

        void SendDatagram(byte[] raw, int offset, int count)
        {
            _transport?.Send(raw, offset, count);
        }

        bool DetachTransport()
        {
            IKcpDatagramTransport transport;
            lock (_sync)
            {
                if (_state == NetworkChannelState.Disconnected && _transport == null)
                {
                    return false;
                }

                transport = _transport;
                ResetConnection();
            }

            transport?.Close();
            return true;
        }

        /// <summary>须持有 <see cref="_sync"/>。</summary>
        void ResetConnection()
        {
            _transport = null;
            _kcp = null;
            _state = NetworkChannelState.Disconnected;
            _heartBeatElapse = 0f;
            _missHeartBeatCount = 0;
            _sentPacketCount = 0;
            _receivedPacketCount = 0;
            _needFlush = false;
            _silenceElapsed = 0f;
            while (_incoming.Count > 0)
            {
                var segment = _incoming.Dequeue();
                if (segment.Array != null)
                {
                    ArrayPool<byte>.Shared.Return(segment.Array);
                }
            }
        }

        void Fail(NetworkErrorCode errorCode, string message)
        {
            IKcpDatagramTransport transport;
            lock (_sync)
            {
                if (_state == NetworkChannelState.Disconnected && _transport == null)
                {
                    return;
                }

                transport = _transport;
                ResetConnection();
                _pending.Enqueue(NewError(errorCode, SocketError.SocketError, message));
                _pending.Enqueue(new PendingDispatch { Kind = PendingKind.Closed, Message = message });
            }

            try
            {
                transport?.Close();
            }
            catch (Exception)
            {
            }
        }

        void UpdateHeartBeat(float elapseSeconds)
        {
            int miss;
            lock (_sync)
            {
                if (_state != NetworkChannelState.Connected || HeartBeatInterval <= 0f)
                {
                    return;
                }

                _heartBeatElapse += elapseSeconds;
                if (_heartBeatElapse < HeartBeatInterval)
                {
                    return;
                }

                _heartBeatElapse = 0f;
                miss = ++_missHeartBeatCount;
            }

            Enqueue(new PendingDispatch { Kind = PendingKind.MissHeartBeat, MissCount = miss });
            if (MaxMissHeartBeatCount > 0 && miss >= MaxMissHeartBeatCount)
            {
                Fail(NetworkErrorCode.HeartBeatTimeout, $"Missed {miss} heartbeats.");
                return;
            }

            try
            {
                _helper.TrySendHeartBeat(this);
            }
            catch (Exception ex)
            {
                GameLog.Exception(LogCategories.Network, ex, $"Channel {Name} heartbeat failed.");
            }
        }

        void ProcessPending()
        {
            while (true)
            {
                PendingDispatch item;
                lock (_sync)
                {
                    if (_pending.Count == 0)
                    {
                        return;
                    }

                    item = _pending.Dequeue();
                }

                try
                {
                    DispatchPending(item);
                }
                catch (Exception ex)
                {
                    GameLog.Exception(LogCategories.Network, ex, $"Channel {Name} dispatch failed.");
                }
            }
        }

        void DispatchPending(PendingDispatch item)
        {
            switch (item.Kind)
            {
                case PendingKind.Connected:
                    GameEvent.Send(new NetworkConnectedEvent { ChannelName = Name });
                    GameLog.Info(LogCategories.Network, $"Channel {LogStyle.Name(Name)} {LogStyle.Ok("connected")} KCP conv={LogStyle.Value(Conversation)}");
                    break;
                case PendingKind.Closed:
                    GameEvent.Send(new NetworkClosedEvent { ChannelName = Name });
                    GameLog.Info(LogCategories.Network, $"Channel {LogStyle.Name(Name)} {LogStyle.Muted("closed")} {item.Message}");
                    break;
                case PendingKind.Error:
                    GameEvent.Send(new NetworkErrorEvent
                    {
                        ChannelName = Name,
                        ErrorCode = item.ErrorCode,
                        SocketError = item.SocketError,
                        Message = item.Message,
                    });
                    GameLog.Error(LogCategories.Network,
                        $"Channel {LogStyle.Name(Name)} {LogStyle.Fail(item.ErrorCode)} {item.Message}");
                    break;
                case PendingKind.MissHeartBeat:
                    GameEvent.Send(new NetworkMissHeartBeatEvent { ChannelName = Name, MissCount = item.MissCount });
                    GameLog.Warning(LogCategories.Network,
                        $"Channel {LogStyle.Name(Name)} miss heartbeat {LogStyle.Value(item.MissCount)}");
                    break;
                case PendingKind.Packet:
                    DispatchPacket(item.Packet);
                    break;
                case PendingKind.TransportError:
                    Fail(NetworkErrorCode.ReceiveError, item.Message ?? "KCP transport failed.");
                    break;
            }
        }

        void DispatchPacket(NetworkPacket packet)
        {
            try
            {
                lock (_sync)
                {
                    _receivedPacketCount++;
                    if (ResetHeartBeatElapseSecondsWhenReceivePacket)
                    {
                        _heartBeatElapse = 0f;
                        _missHeartBeatCount = 0;
                    }
                }

                if (packet.MessageId == DefaultNetworkChannelHelper.HeartBeatMessageId &&
                    !_handlers.ContainsKey(packet.MessageId))
                {
                    return;
                }

                if (_handlers.TryGetValue(packet.MessageId, out var handler))
                {
                    handler(this, packet);
                }
                else
                {
                    _defaultHandler?.Invoke(this, packet);
                }
            }
            finally
            {
                global::Framework.MemoryPool.MemoryPool.Release(packet);
            }
        }

        void SetHandler(ushort messageId, Action<INetworkChannel, NetworkPacket> handler)
        {
            if (_handlers.ContainsKey(messageId))
            {
                GameLog.Warning(
                    LogCategories.Network,
                    $"Channel {LogStyle.Name(Name)} handler {LogStyle.Value(messageId)} overwritten.");
            }

            _handlers[messageId] = handler;
        }

        void EnqueueError(NetworkErrorCode errorCode, SocketError socketError, string message)
        {
            Enqueue(NewError(errorCode, socketError, message));
        }

        static PendingDispatch NewError(NetworkErrorCode errorCode, SocketError socketError, string message)
        {
            return new PendingDispatch
            {
                Kind = PendingKind.Error,
                ErrorCode = errorCode,
                SocketError = socketError,
                Message = message,
            };
        }

        void Enqueue(PendingDispatch item)
        {
            lock (_sync)
            {
                _pending.Enqueue(item);
            }
        }

        sealed class DatagramOutput : Output
        {
            readonly KcpNetworkChannel _owner;

            public DatagramOutput(KcpNetworkChannel owner)
            {
                _owner = owner;
            }

            public override void output(ByteBuf msg, Kcp kcp, object user)
            {
                var count = msg.ReadableBytes();
                if (count <= 0)
                {
                    return;
                }

                _owner.SendDatagram(msg.GetRaw(), msg.ReaderIndex(), count);
            }
        }

        enum PendingKind
        {
            Packet,
            Connected,
            Closed,
            Error,
            MissHeartBeat,
            TransportError,
        }

        struct PendingDispatch
        {
            public PendingKind Kind;
            public NetworkPacket Packet;
            public NetworkErrorCode ErrorCode;
            public SocketError SocketError;
            public string Message;
            public int MissCount;
        }
    }
}
