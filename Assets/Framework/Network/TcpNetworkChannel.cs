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
        public bool Connected => State == NetworkChannelState.Connected && _socket != null && _socket.Connected;

        /// <inheritdoc />
        public Socket Socket => _socket;

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

            if (State == NetworkChannelState.Connecting || State == NetworkChannelState.Connected)
            {
                EnqueueError(NetworkErrorCode.ConnectError, SocketError.IsConnected, "Channel is already connecting or connected.");
                return;
            }

            CloseSocket();
            _closed = false;
            State = NetworkChannelState.Connecting;
            _socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };

            _connectArgs.RemoteEndPoint = new IPEndPoint(address, port);
            try
            {
                if (!_socket.ConnectAsync(_connectArgs))
                {
                    OnConnectCompleted(_connectArgs);
                }
            }
            catch (Exception ex)
            {
                EnqueueError(NetworkErrorCode.ConnectError, SocketError.SocketError, ex.Message);
                CloseSocket();
            }
        }

        /// <inheritdoc />
        public void Close()
        {
            var wasConnected = State == NetworkChannelState.Connected || State == NetworkChannelState.Connecting;
            CloseSocket();
            if (wasConnected)
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

            _handlers[handler.MessageId] = handler.Handle;
        }

        /// <inheritdoc />
        public void RegisterHandler(ushort messageId, Action<INetworkChannel, NetworkPacket> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _handlers[messageId] = handler;
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
            lock (_sync)
            {
                while (_pending.Count > 0)
                {
                    var item = _pending.Dequeue();
                    if (item.Packet != null)
                    {
                        global::Framework.MemoryPool.MemoryPool.Release(item.Packet);
                    }
                }

                _sendQueue.Clear();
            }

            _handlers.Clear();
            _defaultHandler = null;
        }

        void UpdateHeartBeat(float elapseSeconds)
        {
            if (!Connected || HeartBeatInterval <= 0f)
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
            Enqueue(new PendingDispatch { Kind = PendingKind.MissHeartBeat, MissCount = MissHeartBeatCount });
            _helper.TrySendHeartBeat(this);
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
        }

        void DispatchPacket(NetworkPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            try
            {
                ReceivedPacketCount++;
                if (ResetHeartBeatElapseSecondsWhenReceivePacket)
                {
                    _heartBeatElapse = 0f;
                    MissHeartBeatCount = 0;
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
            if (_closed)
            {
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                EnqueueError(NetworkErrorCode.ConnectError, e.SocketError, e.SocketError.ToString());
                CloseSocket();
                return;
            }

            State = NetworkChannelState.Connected;
            Enqueue(new PendingDispatch { Kind = PendingKind.Connected });
            BeginReceive();
        }

        void BeginReceive()
        {
            if (_closed || _socket == null)
            {
                return;
            }

            try
            {
                var remaining = _receiveBuffer.Length - _receiveOffset;
                if (remaining <= 0)
                {
                    GrowReceiveBuffer(_receiveBuffer.Length * 2);
                    remaining = _receiveBuffer.Length - _receiveOffset;
                }

                _receiveArgs.SetBuffer(_receiveBuffer, _receiveOffset, remaining);
                if (!_socket.ReceiveAsync(_receiveArgs))
                {
                    OnReceiveCompleted(_receiveArgs);
                }
            }
            catch (Exception ex)
            {
                EnqueueError(NetworkErrorCode.ReceiveError, SocketError.SocketError, ex.Message);
                CloseSocket();
                Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
            }
        }

        void OnReceiveCompleted(SocketAsyncEventArgs e)
        {
            if (_closed)
            {
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                EnqueueError(NetworkErrorCode.ReceiveError, e.SocketError, e.SocketError.ToString());
                CloseSocket();
                Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                return;
            }

            if (e.BytesTransferred <= 0)
            {
                CloseSocket();
                Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                return;
            }

            _receiveOffset += e.BytesTransferred;
            if (!TryExtractPackets())
            {
                CloseSocket();
                Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                return;
            }

            BeginReceive();
        }

        bool TryExtractPackets()
        {
            var headerLength = _helper.PacketHeaderLength;
            var readOffset = 0;
            while (true)
            {
                var available = _receiveOffset - readOffset;
                if (_packetBodyLength < 0)
                {
                    if (available < headerLength)
                    {
                        break;
                    }

                    _packetBodyLength = _helper.ParsePacketLength(_receiveBuffer, readOffset);
                    if (_packetBodyLength < 0 || _packetBodyLength > MaxPacketSize)
                    {
                        EnqueueError(NetworkErrorCode.DeserializeError, SocketError.SocketError,
                            $"Invalid packet length: {_packetBodyLength}");
                        return false;
                    }
                }

                if (_receiveOffset - readOffset < headerLength + _packetBodyLength)
                {
                    EnsureReceiveCapacity(headerLength + _packetBodyLength);
                    break;
                }

                var packet = _helper.DeserializePacket(_receiveBuffer, readOffset + headerLength, _packetBodyLength);
                if (packet == null)
                {
                    EnqueueError(NetworkErrorCode.DeserializeError, SocketError.SocketError, "Deserialize packet failed.");
                    return false;
                }

                Enqueue(new PendingDispatch { Kind = PendingKind.Packet, Packet = packet });
                readOffset += headerLength + _packetBodyLength;
                _packetBodyLength = -1;
            }

            if (readOffset > 0)
            {
                var remain = _receiveOffset - readOffset;
                if (remain > 0)
                {
                    Buffer.BlockCopy(_receiveBuffer, readOffset, _receiveBuffer, 0, remain);
                }

                _receiveOffset = remain;
            }

            return true;
        }

        void TrySend()
        {
            lock (_sync)
            {
                if (_sending || _sendQueue.Count == 0 || _socket == null || _closed)
                {
                    return;
                }

                var data = _sendQueue.Dequeue();
                _sending = true;
                _sendArgs.SetBuffer(data, 0, data.Length);
            }

            try
            {
                if (!_socket.SendAsync(_sendArgs))
                {
                    OnSendCompleted(_sendArgs);
                }
            }
            catch (Exception ex)
            {
                _sending = false;
                EnqueueError(NetworkErrorCode.SendError, SocketError.SocketError, ex.Message);
                CloseSocket();
                Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
            }
        }

        void OnSendCompleted(SocketAsyncEventArgs e)
        {
            if (_closed)
            {
                lock (_sync)
                {
                    _sending = false;
                }

                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                lock (_sync)
                {
                    _sending = false;
                }

                EnqueueError(NetworkErrorCode.SendError, e.SocketError, e.SocketError.ToString());
                CloseSocket();
                Enqueue(new PendingDispatch { Kind = PendingKind.Closed });
                return;
            }

            SentPacketCount++;
            lock (_sync)
            {
                _sending = false;
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

        void CloseSocket()
        {
            _closed = true;
            State = NetworkChannelState.Disconnected;
            _sending = false;
            _receiveOffset = 0;
            _packetBodyLength = -1;
            _heartBeatElapse = 0f;

            lock (_sync)
            {
                _sendQueue.Clear();
            }

            if (_socket == null)
            {
                return;
            }

            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                _socket.Close();
            }
            catch (Exception)
            {
                // ignored
            }

            _socket = null;
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
