using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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

        IoSession _session;
        bool _closed = true;
        Action<INetworkChannel, NetworkPacket> _defaultHandler;
        float _heartBeatElapse;
        int _generation;
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
                    var socket = _session == null ? null : _session.Socket;
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
                    return _session == null ? null : _session.Socket;
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
                var socket = _session == null ? null : _session.Socket;
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
            var session = e.UserToken as IoSession;
            if (session == null)
            {
                return;
            }

            var accepted = false;
            lock (_sync)
            {
                accepted = !_closed && ReferenceEquals(_session, session);
            }

            if (!accepted)
            {
                session.DisposeConnectArgs();
                TryStartQueuedConnect();
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                EnqueueError(NetworkErrorCode.ConnectError, e.SocketError, e.SocketError.ToString());
                if (CloseSocket(session.Generation))
                {
                    Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                }

                session.DisposeConnectArgs();
                return;
            }

            lock (_sync)
            {
                accepted = !_closed && ReferenceEquals(_session, session);
                if (accepted)
                {
                    State = NetworkChannelState.Connected;
                }
            }

            session.DisposeConnectArgs();
            if (!accepted)
            {
                TryStartQueuedConnect();
                return;
            }

            Enqueue(new PendingDispatch { Kind = PendingKind.Connected });
            BeginReceive();
        }

        void BeginReceive()
        {
            IoSession session;
            Socket socket;
            SocketAsyncEventArgs args;
            lock (_sync)
            {
                session = _session;
                if (_closed || session == null || session.ReceiveInFlight)
                {
                    return;
                }

                var remaining = session.ReceiveBuffer.Length - session.ReceiveOffset;
                if (remaining <= 0)
                {
                    session.GrowReceiveBuffer(session.ReceiveBuffer.Length * 2);
                    remaining = session.ReceiveBuffer.Length - session.ReceiveOffset;
                }

                session.ReceiveArgs.SetBuffer(session.ReceiveBuffer, session.ReceiveOffset, remaining);
                session.ReceiveInFlight = true;
                session.ReceivePosted = true;
                socket = session.Socket;
                args = session.ReceiveArgs;
            }

            try
            {
                if (!socket.ReceiveAsync(args))
                {
                    OnReceiveCompleted(args);
                }
            }
            catch (Exception ex)
            {
                var generation = session.Generation;
                lock (_sync)
                {
                    if (ReferenceEquals(_session, session))
                    {
                        session.ReceiveInFlight = false;
                    }
                }

                EnqueueError(NetworkErrorCode.ReceiveError, SocketError.SocketError, ex.Message);
                if (CloseSocket(generation))
                {
                    Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                }

                session.DisposeReceiveArgs();
            }
        }

        void OnReceiveCompleted(SocketAsyncEventArgs e)
        {
            var session = e.UserToken as IoSession;
            if (session == null)
            {
                return;
            }

            var current = false;
            var fatal = false;
            var reportError = false;
            var socketError = SocketError.Success;
            byte[] buffer = null;
            var length = 0;
            var bodyLength = -1;
            lock (_sync)
            {
                current = !_closed && ReferenceEquals(_session, session);
                if (current)
                {
                    session.ReceiveInFlight = false;
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
                        session.ReceiveOffset += e.BytesTransferred;
                        length = session.ReceiveOffset;
                        bodyLength = session.PacketBodyLength;
                        buffer = session.ReceiveBuffer;
                    }
                }
            }

            if (!current)
            {
                session.DisposeReceiveArgs();
                TryStartQueuedConnect();
                return;
            }

            if (fatal)
            {
                if (reportError)
                {
                    EnqueueError(NetworkErrorCode.ReceiveError, socketError, socketError.ToString());
                }

                if (CloseSocket(session.Generation))
                {
                    Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                }

                session.DisposeReceiveArgs();
                return;
            }

            var parsed = ParseReceive(buffer, length, bodyLength);
            if (!parsed.Ok)
            {
                ReleasePackets(parsed.Packets);
                EnqueueError(NetworkErrorCode.DeserializeError, SocketError.SocketError, parsed.Error);
                if (CloseSocket(session.Generation))
                {
                    Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                }

                session.DisposeReceiveArgs();
                return;
            }

            var stale = false;
            lock (_sync)
            {
                if (_closed || !ReferenceEquals(_session, session))
                {
                    stale = true;
                }
                else
                {
                    session.PacketBodyLength = parsed.BodyLength;
                    if (parsed.Remain > 0)
                    {
                        if (session.ReceiveBuffer.Length < parsed.Remain)
                        {
                            var grown = new byte[session.NextBufferSize(parsed.Remain)];
                            Buffer.BlockCopy(buffer, parsed.Consumed, grown, 0, parsed.Remain);
                            session.ReceiveBuffer = grown;
                        }
                        else
                        {
                            Buffer.BlockCopy(buffer, parsed.Consumed, session.ReceiveBuffer, 0, parsed.Remain);
                        }
                    }

                    session.ReceiveOffset = parsed.Remain;
                    if (parsed.RequiredCapacity > session.ReceiveBuffer.Length)
                    {
                        session.GrowReceiveBuffer(session.NextBufferSize(parsed.RequiredCapacity));
                    }
                }
            }

            if (stale)
            {
                ReleasePackets(parsed.Packets);
                session.DisposeReceiveArgs();
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

            result.Consumed = readOffset;
            result.Remain = length - readOffset;
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

        void TrySend()
        {
            IoSession session;
            Socket socket;
            SocketAsyncEventArgs args;
            lock (_sync)
            {
                session = _session;
                if (session == null || session.Sending || _sendQueue.Count == 0 || _closed)
                {
                    return;
                }

                var data = _sendQueue.Dequeue();
                session.Sending = true;
                session.SendPosted = true;
                session.SendArgs.SetBuffer(data, 0, data.Length);
                socket = session.Socket;
                args = session.SendArgs;
            }

            try
            {
                if (!socket.SendAsync(args))
                {
                    OnSendCompleted(args);
                }
            }
            catch (Exception ex)
            {
                var generation = session.Generation;
                lock (_sync)
                {
                    if (ReferenceEquals(_session, session))
                    {
                        session.Sending = false;
                    }
                }

                EnqueueError(NetworkErrorCode.SendError, SocketError.SocketError, ex.Message);
                if (CloseSocket(generation))
                {
                    Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                }

                session.DisposeSendArgs();
            }
        }

        void OnSendCompleted(SocketAsyncEventArgs e)
        {
            var session = e.UserToken as IoSession;
            if (session == null)
            {
                return;
            }

            var current = false;
            lock (_sync)
            {
                current = !_closed && ReferenceEquals(_session, session);
            }

            if (!current)
            {
                session.DisposeSendArgs();
                TryStartQueuedConnect();
                return;
            }

            if (e.SocketError != SocketError.Success || e.BytesTransferred <= 0)
            {
                var error = e.SocketError != SocketError.Success ? e.SocketError : SocketError.ConnectionReset;
                EnqueueError(NetworkErrorCode.SendError, error, error.ToString());
                if (CloseSocket(session.Generation))
                {
                    Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                }

                session.DisposeSendArgs();
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
                    stillCurrent = !_closed && ReferenceEquals(_session, session);
                    if (stillCurrent)
                    {
                        session.SendArgs.SetBuffer(offset, remain);
                        socket = session.Socket;
                    }
                }

                if (!stillCurrent)
                {
                    session.DisposeSendArgs();
                    TryStartQueuedConnect();
                    return;
                }

                try
                {
                    if (!socket.SendAsync(session.SendArgs))
                    {
                        OnSendCompleted(session.SendArgs);
                    }
                }
                catch (Exception ex)
                {
                    var generation = session.Generation;
                    lock (_sync)
                    {
                        if (ReferenceEquals(_session, session))
                        {
                            session.Sending = false;
                        }
                    }

                    EnqueueError(NetworkErrorCode.SendError, SocketError.SocketError, ex.Message);
                    if (CloseSocket(generation))
                    {
                        Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                    }

                    session.DisposeSendArgs();
                }

                return;
            }

            lock (_sync)
            {
                if (ReferenceEquals(_session, session))
                {
                    SentPacketCount++;
                    session.Sending = false;
                }
            }

            TrySend();
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
            IoSession session;
            bool closedNow;
            lock (_sync)
            {
                if (matchGeneration && generation != _generation)
                {
                    return false;
                }

                closedNow = _session != null || State != NetworkChannelState.Disconnected;
                _generation++;
                _closed = true;
                State = NetworkChannelState.Disconnected;
                _queuedConnect = false;
                _queuedAddress = null;
                _queuedPort = 0;
                _heartBeatElapse = 0f;
                MissHeartBeatCount = 0;
                SentPacketCount = 0;
                ReceivedPacketCount = 0;
                _sendQueue.Clear();
                session = _session;
                _session = null;
            }

            if (session != null)
            {
                session.Release();
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
            var generation = 0;
            lock (_sync)
            {
                if (!_queuedConnect)
                {
                    return;
                }

                address = _queuedAddress;
                port = _queuedPort;
                generation = _generation;
                _queuedAddress = null;
                _queuedConnect = false;
                if (address == null)
                {
                    return;
                }
            }

            BeginConnect(address, port, generation);
        }

        void BeginConnect(IPAddress address, int port, int generation)
        {
            if (address == null)
            {
                return;
            }

            IoSession session;
            lock (_sync)
            {
                if (generation != _generation)
                {
                    return;
                }

                _closed = false;
                State = NetworkChannelState.Connecting;
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                };
                session = new IoSession(generation, socket, OnSocketCompleted);
                session.ConnectArgs.RemoteEndPoint = new IPEndPoint(address, port);
                session.ConnectPosted = true;
                _session = session;
            }

            try
            {
                if (!session.Socket.ConnectAsync(session.ConnectArgs))
                {
                    OnConnectCompleted(session.ConnectArgs);
                }
            }
            catch (Exception ex)
            {
                EnqueueError(NetworkErrorCode.ConnectError, SocketError.SocketError, ex.Message);
                if (CloseSocket(generation))
                {
                    Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                }

                session.DisposeConnectArgs();
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

        /// <summary>一次连接自带的 socket 和三套异步参数。回调只读这份代号。</summary>
        sealed class IoSession
        {
            int _connectDisposed;
            int _receiveDisposed;
            int _sendDisposed;
            int _released;

            /// <summary>发起这次连接时的代号。</summary>
            public readonly int Generation;

            /// <summary>这次连接的套接字。</summary>
            public readonly Socket Socket;

            /// <summary>连接完成参数。</summary>
            public readonly SocketAsyncEventArgs ConnectArgs;

            /// <summary>接收完成参数。</summary>
            public readonly SocketAsyncEventArgs ReceiveArgs;

            /// <summary>发送完成参数。</summary>
            public readonly SocketAsyncEventArgs SendArgs;

            /// <summary>这次连接自己的接收缓冲。</summary>
            public byte[] ReceiveBuffer;

            /// <summary>缓冲里尚未解析完的字节数。</summary>
            public int ReceiveOffset;

            /// <summary>当前包还缺的包体长度；-1 表示还在等包头。</summary>
            public int PacketBodyLength = -1;

            /// <summary>有一包还没发完。</summary>
            public bool Sending;

            /// <summary>接收操作正在进行。</summary>
            public bool ReceiveInFlight;

            /// <summary>已经把连接操作交给套接字。</summary>
            public bool ConnectPosted;

            /// <summary>已经把接收操作交给套接字。</summary>
            public bool ReceivePosted;

            /// <summary>已经把发送操作交给套接字。</summary>
            public bool SendPosted;

            /// <summary>创建一套只属于这次连接的异步参数。</summary>
            /// <param name="generation">连接代号。</param>
            /// <param name="socket">套接字。</param>
            /// <param name="completed">完成回调。</param>
            public IoSession(int generation, Socket socket, EventHandler<SocketAsyncEventArgs> completed)
            {
                Generation = generation;
                Socket = socket;
                ConnectArgs = CreateArgs(completed);
                ReceiveArgs = CreateArgs(completed);
                SendArgs = CreateArgs(completed);
                ReceiveBuffer = new byte[ReceiveBufferSize];
                ReceiveArgs.SetBuffer(ReceiveBuffer, 0, ReceiveBuffer.Length);
            }

            SocketAsyncEventArgs CreateArgs(EventHandler<SocketAsyncEventArgs> completed)
            {
                var args = new SocketAsyncEventArgs();
                args.UserToken = this;
                args.Completed += completed;
                return args;
            }

            /// <summary>扩大接收缓冲，并保留已经收到、还没解析完的字节。</summary>
            /// <param name="size">新容量。</param>
            public void GrowReceiveBuffer(int size)
            {
                var next = new byte[size];
                if (ReceiveOffset > 0)
                {
                    Buffer.BlockCopy(ReceiveBuffer, 0, next, 0, ReceiveOffset);
                }

                ReceiveBuffer = next;
            }

            /// <summary>不小于 <paramref name="required"/> 的缓冲容量。</summary>
            /// <param name="required">至少要容纳的字节数。</param>
            /// <returns>容量。</returns>
            public int NextBufferSize(int required)
            {
                var size = ReceiveBufferSize;
                while (size < required)
                {
                    size *= 2;
                }

                return size;
            }

            /// <summary>关掉套接字。不在进行中的接收和发送参数立刻释放；还在套接字里的那一次，等回调结束再释放。连接参数在连接回调里释放。</summary>
            public void Release()
            {
                if (Interlocked.Exchange(ref _released, 1) != 0)
                {
                    return;
                }

                try
                {
                    Socket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception)
                {
                    // ignored
                }

                try
                {
                    Socket.Close();
                }
                catch (Exception)
                {
                    // ignored
                }

                if (!ConnectPosted)
                {
                    DisposeConnectArgs();
                }

                if (!ReceiveInFlight)
                {
                    DisposeReceiveArgs();
                }

                if (!Sending)
                {
                    DisposeSendArgs();
                }
            }

            /// <summary>释放连接参数。重复调用无效果。</summary>
            public void DisposeConnectArgs()
            {
                DisposeOnce(ConnectArgs, ref _connectDisposed);
            }

            /// <summary>释放接收参数。重复调用无效果。</summary>
            public void DisposeReceiveArgs()
            {
                DisposeOnce(ReceiveArgs, ref _receiveDisposed);
            }

            /// <summary>释放发送参数。重复调用无效果。</summary>
            public void DisposeSendArgs()
            {
                DisposeOnce(SendArgs, ref _sendDisposed);
            }

            static void DisposeOnce(SocketAsyncEventArgs args, ref int flag)
            {
                if (Interlocked.Exchange(ref flag, 1) != 0)
                {
                    return;
                }

                try
                {
                    args.Dispose();
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        struct ParsedReceive
        {
            public bool Ok;
            public string Error;
            public int BodyLength;
            public int Remain;
            public int Consumed;
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
