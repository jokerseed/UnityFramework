using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Framework.Events;
using Framework.Logging;

namespace Framework.Network
{
    /// <summary>TCP 异步频道：IO 完成端口收发，主线程分发消息与事件。</summary>
    public sealed class TcpNetworkChannel : INetworkChannel
    {
        const int ReceiveBufferSize = 8192;
        const int MaxPacketSize = 1024 * 1024;

        readonly object _sync = new object();
        readonly Queue<PendingDispatch> _pending = new Queue<PendingDispatch>(32);
        readonly Queue<byte[]> _sendQueue = new Queue<byte[]>(16);
        readonly Dictionary<ushort, Action<INetworkChannel, NetworkPacket>> _handlers =
            new Dictionary<ushort, Action<INetworkChannel, NetworkPacket>>(32);
        readonly INetworkChannelHelper _helper;
        readonly MemoryStream _serializeStream = new MemoryStream(256);
        readonly SocketAsyncEventArgs _connectArgs = new SocketAsyncEventArgs();
        readonly SocketAsyncEventArgs _receiveArgs = new SocketAsyncEventArgs();
        readonly SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();

        byte[] _receiveBuffer = new byte[ReceiveBufferSize];
        int _receiveOffset;
        int _packetBodyLength = -1;
        bool _sending;
        bool _closed = true;
        Socket _socket;
        Action<INetworkChannel, NetworkPacket> _defaultHandler;
        float _heartBeatElapse;
        int _generation;
        bool _connectArgsBusy;
        bool _receiveArgsBusy;
        bool _sendArgsBusy;
        bool _queuedConnect;
        IPAddress _queuedAddress;
        int _queuedPort;

        /// <summary>创建 TCP 频道。</summary>
        /// <param name="name">频道名称。</param>
        /// <param name="helper">协议辅助器，不可为 null。</param>
        public TcpNetworkChannel(string name, INetworkChannelHelper helper)
        {
            Name = name ?? string.Empty;
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            if (_helper.PacketHeaderLength <= 0)
            {
                throw new ArgumentException("PacketHeaderLength must be greater than 0.", nameof(helper));
            }

            _connectArgs.Completed += OnSocketCompleted;
            _receiveArgs.Completed += OnSocketCompleted;
            _sendArgs.Completed += OnSocketCompleted;
            _receiveArgs.SetBuffer(_receiveBuffer, 0, _receiveBuffer.Length);
        }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public NetworkChannelState State { get; private set; } = NetworkChannelState.Disconnected;

        /// <inheritdoc />
        public bool Connected
        {
            get
            {
                lock (_sync)
                {
                    var socket = _socket;
                    return State == NetworkChannelState.Connected && socket != null && socket.Connected;
                }
            }
        }

        /// <inheritdoc />
        public Socket Socket
        {
            get
            {
                lock (_sync)
                {
                    return _socket;
                }
            }
        }

        /// <inheritdoc />
        public NetworkServiceType ServiceType => NetworkServiceType.Tcp;

        /// <inheritdoc />
        public float HeartBeatInterval { get; set; }

        /// <inheritdoc />
        public bool ResetHeartBeatElapseSecondsWhenReceivePacket { get; set; } = true;

        /// <inheritdoc />
        public int MissHeartBeatCount { get; private set; }

        /// <inheritdoc />
        public int SentPacketCount { get; private set; }

        /// <inheritdoc />
        public int ReceivedPacketCount { get; private set; }

        /// <inheritdoc />
        public void Connect(string host, int port)
        {
            if (string.IsNullOrEmpty(host))
            {
                EnqueueError(NetworkErrorCode.AddressError, SocketError.AddressNotAvailable, "Host is empty.");
                return;
            }

            if (!IPAddress.TryParse(host, out var address))
            {
                try
                {
                    var addresses = Dns.GetHostAddresses(host);
                    address = SelectAddress(addresses);
                }
                catch (Exception ex)
                {
                    EnqueueError(NetworkErrorCode.AddressError, SocketError.HostNotFound, ex.Message);
                    return;
                }
            }

            if (address == null)
            {
                EnqueueError(NetworkErrorCode.AddressError, SocketError.AddressNotAvailable, $"No usable address for {host}.");
                return;
            }

            Connect(address, port);
        }

        /// <inheritdoc />
        public void Connect(IPAddress address, int port)
        {
            if (address == null)
            {
                EnqueueError(NetworkErrorCode.AddressError, SocketError.AddressNotAvailable, "Address is null.");
                return;
            }

            var busy = false;
            lock (_sync)
            {
                busy = State == NetworkChannelState.Connecting || State == NetworkChannelState.Connected;
            }

            if (busy)
            {
                EnqueueError(NetworkErrorCode.ConnectError, SocketError.IsConnected, "Channel is already connecting or connected.");
                return;
            }

            CloseSocket();
            lock (_sync)
            {
                if (State == NetworkChannelState.Connecting || State == NetworkChannelState.Connected)
                {
                    busy = true;
                }
                else
                {
                    _queuedAddress = address;
                    _queuedPort = port;
                    _queuedConnect = true;
                }
            }

            if (busy)
            {
                EnqueueError(NetworkErrorCode.ConnectError, SocketError.IsConnected, "Channel is already connecting or connected.");
                return;
            }

            TryStartQueuedConnect();
        }

        /// <inheritdoc />
        public void Close()
        {
            if (CloseSocket())
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
                if (!Connected)
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

                var bytes = _serializeStream.ToArray();
                lock (_sync)
                {
                    _sendQueue.Enqueue(bytes);
                }

                TrySend();
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

        /// <summary>主线程轮询：分发收包、心跳。</summary>
        /// <param name="elapseSeconds">真实流逝时间（秒）。</param>
        public void Update(float elapseSeconds)
        {
            ProcessPending();
            UpdateHeartBeat(elapseSeconds);
        }

        /// <summary>关闭频道并释放 Socket 资源。</summary>
        public void Shutdown()
        {
            CloseSocket();
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

                _sendQueue.Clear();
            }

            for (var i = 0; i < released.Count; i++)
            {
                global::Framework.MemoryPool.MemoryPool.Release(released[i]);
            }

            _handlers.Clear();
            _defaultHandler = null;
        }

        void UpdateHeartBeat(float elapseSeconds)
        {
            var due = false;
            var miss = 0;
            lock (_sync)
            {
                var socket = _socket;
                var connected = State == NetworkChannelState.Connected && socket != null && socket.Connected;
                if (!connected || HeartBeatInterval <= 0f)
                {
                    return;
                }

                _heartBeatElapse += elapseSeconds;
                if (_heartBeatElapse < HeartBeatInterval)
                {
                    return;
                }

                _heartBeatElapse = 0f;
                MissHeartBeatCount++;
                miss = MissHeartBeatCount;
                due = true;
            }

            if (!due)
            {
                return;
            }

            Enqueue(new PendingDispatch { Kind = PendingKind.MissHeartBeat, MissCount = miss });
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
                        GameLog.Info(LogCategories.Network, $"Channel {LogStyle.Name(Name)} {LogStyle.Ok("connected")}");
                        break;
                    case PendingKind.Closed:
                        GameEvent.Send(new NetworkClosedEvent { ChannelName = Name });
                        GameLog.Info(LogCategories.Network, $"Channel {LogStyle.Name(Name)} {LogStyle.Muted("closed")}");
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
                }
        }

        void DispatchPacket(NetworkPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            try
            {
                lock (_sync)
                {
                    ReceivedPacketCount++;
                    if (ResetHeartBeatElapseSecondsWhenReceivePacket)
                    {
                        _heartBeatElapse = 0f;
                        MissHeartBeatCount = 0;
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

        void OnSocketCompleted(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Connect:
                    OnConnectCompleted(e);
                    break;
                case SocketAsyncOperation.Receive:
                    OnReceiveCompleted(e);
                    break;
                case SocketAsyncOperation.Send:
                    OnSendCompleted(e);
                    break;
            }
        }

        void OnConnectCompleted(SocketAsyncEventArgs e)
        {
            var generation = e.UserToken is int connectToken ? connectToken : -1;
            var accepted = false;
            lock (_sync)
            {
                accepted = !_closed && generation == _generation;
                if (accepted)
                {
                    _connectArgsBusy = false;
                }
            }

            if (!accepted)
            {
                TryStartQueuedConnect();
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                EnqueueError(NetworkErrorCode.ConnectError, e.SocketError, e.SocketError.ToString());
                CloseSocket(generation);
                return;
            }

            var current = false;
            lock (_sync)
            {
                current = !_closed && e.UserToken is int token && token == _generation;
                if (current)
                {
                    State = NetworkChannelState.Connected;
                }
            }

            if (!current)
            {
                TryStartQueuedConnect();
                return;
            }

            Enqueue(new PendingDispatch { Kind = PendingKind.Connected });
            BeginReceive();
        }

        void BeginReceive()
        {
            Socket socket;
            var generation = 0;
            lock (_sync)
            {
                if (_closed || _socket == null || _receiveArgsBusy)
                {
                    return;
                }

                var remaining = _receiveBuffer.Length - _receiveOffset;
                if (remaining <= 0)
                {
                    GrowReceiveBuffer(_receiveBuffer.Length * 2);
                    remaining = _receiveBuffer.Length - _receiveOffset;
                }

                generation = _generation;
                _receiveArgs.UserToken = generation;
                _receiveArgs.SetBuffer(_receiveBuffer, _receiveOffset, remaining);
                _receiveArgsBusy = true;
                socket = _socket;
            }

            try
            {
                if (!socket.ReceiveAsync(_receiveArgs))
                {
                    OnReceiveCompleted(_receiveArgs);
                }
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    if (generation == _generation)
                    {
                        _receiveArgsBusy = false;
                    }
                }

                EnqueueError(NetworkErrorCode.ReceiveError, SocketError.SocketError, ex.Message);
                if (CloseSocket(generation))
                {
                    Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                }
            }
        }

        void OnReceiveCompleted(SocketAsyncEventArgs e)
        {
            var generation = e.UserToken is int token ? token : -1;
            var current = false;
            var fatal = false;
            var reportError = false;
            SocketError socketError = SocketError.Success;
            byte[] copy = null;
            var length = 0;
            var bodyLength = -1;
            lock (_sync)
            {
                current = !_closed && generation == _generation;
                if (current)
                {
                _receiveArgsBusy = false;
                if (e.SocketError != SocketError.Success)
                {
                    fatal = true;
                    reportError = true;
                    socketError = e.SocketError;
                }
                else if (e.BytesTransferred <= 0)
                {
                    fatal = true;
                }
                else
                {
                    _receiveOffset += e.BytesTransferred;
                    length = _receiveOffset;
                    bodyLength = _packetBodyLength;
                    copy = new byte[length];
                    Buffer.BlockCopy(_receiveBuffer, 0, copy, 0, length);
                }
                }
            }

            if (!current)
            {
                TryStartQueuedConnect();
                return;
            }

            if (fatal)
            {
                if (reportError)
                {
                    EnqueueError(NetworkErrorCode.ReceiveError, socketError, socketError.ToString());
                }

                if (CloseSocket(generation))
                {
                    Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                }

                return;
            }

            var parsed = ParseReceive(copy, length, bodyLength);
            if (!parsed.Ok)
            {
                ReleasePackets(parsed.Packets);
                EnqueueError(NetworkErrorCode.DeserializeError, SocketError.SocketError, parsed.Error);
                if (CloseSocket(generation))
                {
                    Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                }

                return;
            }

            var stale = false;
            lock (_sync)
            {
                if (_closed || _generation != generation)
                {
                    stale = true;
                }
                else
                {
                    _packetBodyLength = parsed.BodyLength;
                    _receiveOffset = parsed.Remain;
                    if (parsed.Remain > 0)
                    {
                        if (_receiveBuffer.Length < parsed.Remain)
                        {
                            GrowReceiveBuffer(NextBufferSize(parsed.Remain));
                        }

                        Buffer.BlockCopy(parsed.Remainder, 0, _receiveBuffer, 0, parsed.Remain);
                    }

                    if (parsed.RequiredCapacity > _receiveBuffer.Length)
                    {
                        GrowReceiveBuffer(NextBufferSize(parsed.RequiredCapacity));
                    }
                }
            }

            if (stale)
            {
                ReleasePackets(parsed.Packets);
                TryStartQueuedConnect();
                return;
            }

            for (var i = 0; i < parsed.Packets.Count; i++)
            {
                Enqueue(new PendingDispatch { Kind = PendingKind.Packet, Packet = parsed.Packets[i] });
            }

            BeginReceive();
        }

        ParsedReceive ParseReceive(byte[] buffer, int length, int bodyLength)
        {
            var result = new ParsedReceive
            {
                Ok = true,
                BodyLength = bodyLength,
                Packets = new List<NetworkPacket>(4),
            };
            var headerLength = _helper.PacketHeaderLength;
            var readOffset = 0;
            while (true)
            {
                var available = length - readOffset;
                if (result.BodyLength < 0)
                {
                    if (available < headerLength)
                    {
                        break;
                    }

                    var packetBody = _helper.ParsePacketLength(buffer, readOffset);
                    if (packetBody < 0 || packetBody > MaxPacketSize)
                    {
                        result.Ok = false;
                        result.Error = "Invalid packet length: " + packetBody;
                        return result;
                    }

                    result.BodyLength = packetBody;
                }

                if (length - readOffset < headerLength + result.BodyLength)
                {
                    result.RequiredCapacity = headerLength + result.BodyLength;
                    break;
                }

                var packet = _helper.DeserializePacket(buffer, readOffset + headerLength, result.BodyLength);
                if (packet == null)
                {
                    result.Ok = false;
                    result.Error = "Deserialize packet failed.";
                    return result;
                }

                result.Packets.Add(packet);
                readOffset += headerLength + result.BodyLength;
                result.BodyLength = -1;
            }

            result.Remain = length - readOffset;
            if (result.Remain > 0)
            {
                result.Remainder = new byte[result.Remain];
                Buffer.BlockCopy(buffer, readOffset, result.Remainder, 0, result.Remain);
            }

            return result;
        }

        static void ReleasePackets(List<NetworkPacket> packets)
        {
            if (packets == null)
            {
                return;
            }

            for (var i = 0; i < packets.Count; i++)
            {
                if (packets[i] != null)
                {
                    global::Framework.MemoryPool.MemoryPool.Release(packets[i]);
                }
            }
        }

        static int NextBufferSize(int required)
        {
            var size = ReceiveBufferSize;
            while (size < required)
            {
                size *= 2;
            }

            return size;
        }

        void TrySend()
        {
            Socket socket;
            var generation = 0;
            lock (_sync)
            {
                if (_sending || _sendArgsBusy || _sendQueue.Count == 0 || _socket == null || _closed)
                {
                    return;
                }

                var data = _sendQueue.Dequeue();
                generation = _generation;
                _sending = true;
                _sendArgsBusy = true;
                _sendArgs.UserToken = generation;
                _sendArgs.SetBuffer(data, 0, data.Length);
                socket = _socket;
            }

            try
            {
                if (!socket.SendAsync(_sendArgs))
                {
                    OnSendCompleted(_sendArgs);
                }
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    if (generation == _generation)
                    {
                        _sendArgsBusy = false;
                        _sending = false;
                    }
                }

                EnqueueError(NetworkErrorCode.SendError, SocketError.SocketError, ex.Message);
                if (CloseSocket(generation))
                {
                    Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                }
            }
        }

        void OnSendCompleted(SocketAsyncEventArgs e)
        {
            var generation = e.UserToken is int token ? token : -1;
            var current = false;
            lock (_sync)
            {
                current = !_closed && generation == _generation;
                if (current)
                {
                    _sendArgsBusy = false;
                }
            }

            if (!current)
            {
                TryStartQueuedConnect();
                return;
            }

            if (e.SocketError != SocketError.Success || e.BytesTransferred <= 0)
            {
                var error = e.SocketError != SocketError.Success ? e.SocketError : SocketError.ConnectionReset;
                EnqueueError(NetworkErrorCode.SendError, error, error.ToString());
                if (CloseSocket(generation))
                {
                    Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                }

                return;
            }

            if (e.BytesTransferred < e.Count)
            {
                var offset = e.Offset + e.BytesTransferred;
                var remain = e.Count - e.BytesTransferred;
                Socket socket = null;
                var stillCurrent = false;
                lock (_sync)
                {
                    stillCurrent = !_closed && _socket != null && generation == _generation;
                    if (stillCurrent)
                    {
                        _sendArgs.UserToken = generation;
                        _sendArgs.SetBuffer(offset, remain);
                        _sendArgsBusy = true;
                        socket = _socket;
                    }
                }

                if (!stillCurrent)
                {
                    TryStartQueuedConnect();
                    return;
                }

                try
                {
                    if (!socket.SendAsync(_sendArgs))
                    {
                        OnSendCompleted(_sendArgs);
                    }
                }
                catch (Exception ex)
                {
                    lock (_sync)
                    {
                        if (generation == _generation)
                        {
                            _sendArgsBusy = false;
                            _sending = false;
                        }
                    }

                    EnqueueError(NetworkErrorCode.SendError, SocketError.SocketError, ex.Message);
                    if (CloseSocket(generation))
                    {
                        Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                    }
                }

                return;
            }

            lock (_sync)
            {
                if (generation == _generation)
                {
                    SentPacketCount++;
                    _sending = false;
                }
            }

            TrySend();
        }

        void EnsureReceiveCapacity(int required)
        {
            if (_receiveBuffer.Length >= required)
            {
                return;
            }

            var size = _receiveBuffer.Length;
            while (size < required)
            {
                size *= 2;
            }

            GrowReceiveBuffer(size);
        }

        void GrowReceiveBuffer(int size)
        {
            var next = new byte[size];
            if (_receiveOffset > 0)
            {
                Buffer.BlockCopy(_receiveBuffer, 0, next, 0, _receiveOffset);
            }

            _receiveBuffer = next;
        }

        /// <summary>关闭套接字。同一场连接只算一次，避免收发两条错误各报一次 Closed。</summary>
        /// <returns>这次调用确实关掉了一场连接或一次正在进行的连接时返回 true。</returns>
        bool CloseSocket() => FinishClose(false, 0);

        /// <summary>只关闭仍是这一代的连接。过期回调返回 false，不会拆掉新连接。</summary>
        /// <param name="generation">发起这次操作时的连接代数。</param>
        /// <returns>这一代确实被关掉时返回 true。</returns>
        bool CloseSocket(int generation) => FinishClose(true, generation);

        bool FinishClose(bool matchGeneration, int generation)
        {
            Socket socket;
            bool closedNow;
            lock (_sync)
            {
                if (matchGeneration && generation != _generation)
                {
                    return false;
                }

                closedNow = _socket != null || State != NetworkChannelState.Disconnected;
                _generation++;
                _closed = true;
                State = NetworkChannelState.Disconnected;
                _sending = false;
                _connectArgsBusy = false;
                _receiveArgsBusy = false;
                _sendArgsBusy = false;
                _queuedConnect = false;
                _queuedAddress = null;
                _queuedPort = 0;
                _receiveOffset = 0;
                _packetBodyLength = -1;
                _heartBeatElapse = 0f;
                MissHeartBeatCount = 0;
                SentPacketCount = 0;
                ReceivedPacketCount = 0;
                _sendQueue.Clear();
                socket = _socket;
                _socket = null;
            }

            if (socket == null)
            {
                return closedNow;
            }

            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                socket.Close();
            }
            catch (Exception)
            {
                // ignored
            }

            return closedNow;
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

        void TryStartQueuedConnect()
        {
            IPAddress address;
            var port = 0;
            lock (_sync)
            {
                if (!_queuedConnect || _connectArgsBusy || _receiveArgsBusy || _sendArgsBusy)
                {
                    return;
                }

                address = _queuedAddress;
                port = _queuedPort;
                _queuedAddress = null;
                _queuedConnect = false;
                if (address == null)
                {
                    return;
                }

                _connectArgsBusy = true;
            }

            BeginConnect(address, port);
        }

        void BeginConnect(IPAddress address, int port)
        {
            if (address == null)
            {
                lock (_sync)
                {
                    _connectArgsBusy = false;
                }

                return;
            }

            Socket socket;
            var generation = 0;
            lock (_sync)
            {
                _closed = false;
                State = NetworkChannelState.Connecting;
                _receiveOffset = 0;
                _packetBodyLength = -1;
                _socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                };
                generation = _generation;
                _connectArgs.UserToken = generation;
                _receiveArgs.UserToken = generation;
                _sendArgs.UserToken = generation;
                _connectArgs.RemoteEndPoint = new IPEndPoint(address, port);
                socket = _socket;
            }

            try
            {
                if (!socket.ConnectAsync(_connectArgs))
                {
                    OnConnectCompleted(_connectArgs);
                }
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    if (generation == _generation)
                    {
                        _connectArgsBusy = false;
                    }
                }

                EnqueueError(NetworkErrorCode.ConnectError, SocketError.SocketError, ex.Message);
                CloseSocket(generation);
            }
        }

        bool IsCurrentOperation(SocketAsyncEventArgs args)
        {
            lock (_sync)
            {
                return !_closed && args.UserToken is int token && token == _generation;
            }
        }

        void EnqueueError(NetworkErrorCode errorCode, SocketError socketError, string message)
        {
            Enqueue(new PendingDispatch
            {
                Kind = PendingKind.Error,
                ErrorCode = errorCode,
                SocketError = socketError,
                Message = message,
            });
        }

        void Enqueue(PendingDispatch item)
        {
            lock (_sync)
            {
                _pending.Enqueue(item);
            }
        }

        static IPAddress SelectAddress(IPAddress[] addresses)
        {
            if (addresses == null || addresses.Length == 0)
            {
                return null;
            }

            for (var i = 0; i < addresses.Length; i++)
            {
                if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    return addresses[i];
                }
            }

            return addresses[0];
        }

        struct ParsedReceive
        {
            public bool Ok;
            public string Error;
            public int BodyLength;
            public int Remain;
            public byte[] Remainder;
            public List<NetworkPacket> Packets;
            public int RequiredCapacity;
        }

        enum PendingKind
        {
            Packet,
            Connected,
            Closed,
            Error,
            MissHeartBeat,
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
