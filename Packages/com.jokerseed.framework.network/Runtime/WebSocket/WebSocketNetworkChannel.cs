using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Framework.Events;
using Framework.Logging;

namespace Framework.Network
{
    /// <summary>
    /// WebSocket 频道：一条二进制消息就是一个完整包（包头 + 包体，格式由 <see cref="INetworkChannelHelper"/> 决定，与 TCP 频道相同）。
    /// 传输回调可能来自任意线程，只入队；消息与事件都在主线程 <see cref="Update"/> 中分发。
    /// </summary>
    public sealed class WebSocketNetworkChannel : INetworkChannel, INetworkChannelDriver, IWebSocketTransportListener
    {
        const int MaxPacketSize = 1024 * 1024;

        readonly object _sync = new object();
        readonly Queue<PendingDispatch> _pending = new Queue<PendingDispatch>(32);
        readonly Dictionary<ushort, Action<INetworkChannel, NetworkPacket>> _handlers =
            new Dictionary<ushort, Action<INetworkChannel, NetworkPacket>>(32);
        readonly INetworkChannelHelper _helper;
        readonly Func<IWebSocketTransportListener, IWebSocketTransport> _transportFactory;
        readonly MemoryStream _serializeStream = new MemoryStream(256);

        IWebSocketTransport _transport;
        Action<INetworkChannel, NetworkPacket> _defaultHandler;
        float _heartBeatElapse;
        int _missHeartBeatCount;
        int _sentPacketCount;
        int _receivedPacketCount;
        NetworkChannelState _state = NetworkChannelState.Disconnected;

        /// <summary>使用平台默认传输创建频道（非 WebGL：ClientWebSocket；WebGL：浏览器 WebSocket）。</summary>
        /// <param name="name">频道名称。</param>
        /// <param name="helper">协议辅助器，不可为 null。</param>
        public WebSocketNetworkChannel(string name, INetworkChannelHelper helper)
            : this(name, helper, CreateDefaultTransport)
        {
        }

        /// <summary>使用自定义传输创建频道（如微信小游戏 SDK、测试替身）。</summary>
        /// <param name="name">频道名称。</param>
        /// <param name="helper">协议辅助器，不可为 null。</param>
        /// <param name="transportFactory">每次连接调用一次，返回新的传输实例。</param>
        public WebSocketNetworkChannel(
            string name,
            INetworkChannelHelper helper,
            Func<IWebSocketTransportListener, IWebSocketTransport> transportFactory)
        {
            Name = name ?? string.Empty;
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
            if (_helper.PacketHeaderLength <= 0)
            {
                throw new ArgumentException("PacketHeaderLength must be greater than 0.", nameof(helper));
            }
        }

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

        /// <summary>最近一次连接的地址；未连接过时为 null。</summary>
        public Uri Uri { get; private set; }

        /// <inheritdoc />
        public NetworkServiceType ServiceType => NetworkServiceType.WebSocket;

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

        /// <summary>连接 <c>ws://host:port/</c>；需要 wss 或路径时用 <see cref="Connect(Uri)"/>。</summary>
        /// <param name="host">IP 或主机名。</param>
        /// <param name="port">端口。</param>
        public void Connect(string host, int port)
        {
            if (string.IsNullOrEmpty(host))
            {
                EnqueueError(NetworkErrorCode.AddressError, SocketError.AddressNotAvailable, "Host is empty.");
                return;
            }

            Uri uri;
            try
            {
                uri = new UriBuilder("ws", host, port, "/").Uri;
            }
            catch (UriFormatException ex)
            {
                EnqueueError(NetworkErrorCode.AddressError, SocketError.AddressNotAvailable, ex.Message);
                return;
            }

            Connect(uri);
        }

        /// <summary>连接 <c>ws://address:port/</c>。</summary>
        /// <param name="address">IP 地址。</param>
        /// <param name="port">端口。</param>
        public void Connect(IPAddress address, int port)
        {
            if (address == null)
            {
                EnqueueError(NetworkErrorCode.AddressError, SocketError.AddressNotAvailable, "Address is null.");
                return;
            }

            Connect(address.ToString(), port);
        }

        /// <summary>连接指定的 ws:// 或 wss:// 地址。结果通过 <see cref="NetworkConnectedEvent"/> / <see cref="NetworkErrorEvent"/> 通知。</summary>
        /// <param name="uri">地址。</param>
        public void Connect(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri || (uri.Scheme != "ws" && uri.Scheme != "wss"))
            {
                EnqueueError(NetworkErrorCode.AddressError, SocketError.AddressNotAvailable, $"Invalid WebSocket address: {uri}");
                return;
            }

            IWebSocketTransport transport;
            lock (_sync)
            {
                if (_state != NetworkChannelState.Disconnected)
                {
                    transport = null;
                }
                else
                {
                    transport = _transportFactory(this);
                    _transport = transport;
                    _state = NetworkChannelState.Connecting;
                    Uri = uri;
                }
            }

            if (transport == null)
            {
                EnqueueError(NetworkErrorCode.ConnectError, SocketError.IsConnected, "Channel is already connecting or connected.");
                return;
            }

            transport.Connect(uri);
        }

        /// <inheritdoc />
        public void Close()
        {
            if (DetachTransport() != null)
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
                IWebSocketTransport transport;
                lock (_sync)
                {
                    transport = _state == NetworkChannelState.Connected ? _transport : null;
                }

                if (transport == null)
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

                transport.Send(_serializeStream.ToArray());
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

        void IWebSocketTransportListener.OnTransportOpen(IWebSocketTransport transport)
        {
            lock (_sync)
            {
                if (transport != _transport)
                {
                    return;
                }

                _state = NetworkChannelState.Connected;
                _pending.Enqueue(new PendingDispatch { Kind = PendingKind.Connected });
            }
        }

        void IWebSocketTransportListener.OnTransportMessage(IWebSocketTransport transport, byte[] data, int length)
        {
            var headerLength = _helper.PacketHeaderLength;
            var bodyLength = length >= headerLength ? _helper.ParsePacketLength(data, 0) : -1;
            if (bodyLength < 0 || bodyLength > MaxPacketSize || headerLength + bodyLength != length)
            {
                FailTransport(transport, NetworkErrorCode.DeserializeError,
                    $"Frame length {length} does not match one packet (header {headerLength}, body {bodyLength}).");
                return;
            }

            var packet = _helper.DeserializePacket(data, headerLength, bodyLength);
            if (packet == null)
            {
                FailTransport(transport, NetworkErrorCode.DeserializeError, "Deserialize packet failed.");
                return;
            }

            lock (_sync)
            {
                if (transport == _transport)
                {
                    _pending.Enqueue(new PendingDispatch { Kind = PendingKind.Packet, Packet = packet });
                    return;
                }
            }

            global::Framework.MemoryPool.MemoryPool.Release(packet);
        }

        void IWebSocketTransportListener.OnTransportSent(IWebSocketTransport transport)
        {
            lock (_sync)
            {
                if (transport == _transport)
                {
                    _sentPacketCount++;
                }
            }
        }

        void IWebSocketTransportListener.OnTransportError(IWebSocketTransport transport, NetworkErrorCode errorCode, string message)
        {
            lock (_sync)
            {
                if (transport == _transport)
                {
                    _pending.Enqueue(NewError(errorCode, SocketError.SocketError, message));
                }
            }
        }

        void IWebSocketTransportListener.OnTransportClosed(IWebSocketTransport transport, string reason)
        {
            lock (_sync)
            {
                if (transport != _transport)
                {
                    return;
                }

                ResetConnection();
                _pending.Enqueue(new PendingDispatch { Kind = PendingKind.Closed, Message = reason });
            }
        }

        static IWebSocketTransport CreateDefaultTransport(IWebSocketTransportListener listener)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new BrowserWebSocketTransport(listener);
#else
            return new ClientWebSocketTransport(listener, MaxPacketSize + 64);
#endif
        }

        /// <summary>协议错误：关掉这条连接，并按「先错误后关闭」入队。</summary>
        void FailTransport(IWebSocketTransport transport, NetworkErrorCode errorCode, string message)
        {
            lock (_sync)
            {
                if (transport != _transport)
                {
                    return;
                }

                ResetConnection();
                _pending.Enqueue(NewError(errorCode, SocketError.SocketError, message));
                _pending.Enqueue(new PendingDispatch { Kind = PendingKind.Closed, Message = message });
            }

            transport.Close();
        }

        IWebSocketTransport DetachTransport()
        {
            IWebSocketTransport transport;
            lock (_sync)
            {
                transport = _transport;
                ResetConnection();
            }

            transport?.Close();
            return transport;
        }

        /// <summary>须持有 <see cref="_sync"/>。</summary>
        void ResetConnection()
        {
            _transport = null;
            _state = NetworkChannelState.Disconnected;
            _heartBeatElapse = 0f;
            _missHeartBeatCount = 0;
            _sentPacketCount = 0;
            _receivedPacketCount = 0;
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
                    GameLog.Info(LogCategories.Network, $"Channel {LogStyle.Name(Name)} {LogStyle.Ok("connected")} {Uri}");
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
