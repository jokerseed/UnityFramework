#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Net;
using System.Net.Sockets;

namespace Framework.Network
{
    /// <summary>
    /// 用已连接的 UDP <see cref="Socket"/> 收发 KCP 数据报。完成回调只通知监听器，不解析协议。
    /// 接收用一块固定缓冲和 <see cref="SocketAsyncEventArgs"/> 循环投递，不为每个数据报分配数组。
    /// Windows 上收到 ICMP 端口不可达后，下一次接收会报 ConnectionReset（10054）：这里关掉该行为并忽略这个错误，
    /// 否则服务端短暂重启就会让 PC 客户端和编辑器断线。
    /// </summary>
    public sealed class UdpKcpDatagramTransport : IKcpDatagramTransport
    {
        const int ReceiveBufferSize = 64 * 1024;

        /// <summary>Windows 的 SIO_UDP_CONNRESET 控制码。</summary>
        const int SioUdpConnReset = -1744830452;

        readonly IKcpDatagramListener _listener;
        readonly object _sync = new object();
        Socket _socket;
        bool _closed;

        /// <summary>创建传输。</summary>
        /// <param name="listener">数据报与错误回调，不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="listener"/> 为 null。</exception>
        public UdpKcpDatagramTransport(IKcpDatagramListener listener)
        {
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        }

        /// <inheritdoc />
        public void Start(IPEndPoint remote)
        {
            if (remote == null)
            {
                throw new ArgumentNullException(nameof(remote));
            }

            var socket = new Socket(remote.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            DisableConnectionReset(socket);
            socket.Connect(remote);
            var args = new SocketAsyncEventArgs();
            args.SetBuffer(new byte[ReceiveBufferSize], 0, ReceiveBufferSize);
            args.UserToken = socket;
            args.Completed += OnReceiveCompleted;
            lock (_sync)
            {
                if (_closed)
                {
                    socket.Close();
                    return;
                }

                _socket = socket;
            }

            BeginReceive(socket, args);
        }

        /// <inheritdoc />
        public void Send(byte[] datagram, int offset, int count)
        {
            if (datagram == null)
            {
                throw new ArgumentNullException(nameof(datagram));
            }

            if (count <= 0 || offset < 0 || offset + count > datagram.Length)
            {
                return;
            }

            Socket socket;
            lock (_sync)
            {
                socket = _closed ? null : _socket;
            }

            if (socket == null)
            {
                return;
            }

            try
            {
                socket.Send(datagram, offset, count, SocketFlags.None);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                // 同接收：ICMP 不可达不是致命错误，KCP 会重传。
            }
            catch (Exception ex)
            {
                if (IsCurrent(socket))
                {
                    _listener.OnTransportError(ex.Message);
                }
            }
        }

        /// <inheritdoc />
        public void Close()
        {
            Socket socket;
            lock (_sync)
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                socket = _socket;
                _socket = null;
            }

            try
            {
                socket?.Close();
            }
            catch (SocketException)
            {
            }
        }

        static void DisableConnectionReset(Socket socket)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            try
            {
                socket.IOControl(SioUdpConnReset, new byte[4], null);
            }
            catch (Exception)
            {
                // 个别运行时不支持该控制码；接收侧仍会忽略 ConnectionReset。
            }
        }

        void BeginReceive(Socket socket, SocketAsyncEventArgs args)
        {
            while (true)
            {
                bool pending;
                try
                {
                    pending = socket.ReceiveAsync(args);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (IsCurrent(socket))
                    {
                        _listener.OnTransportError(ex.Message);
                    }

                    return;
                }

                if (pending || !HandleReceive(socket, args))
                {
                    return;
                }
            }
        }

        void OnReceiveCompleted(object sender, SocketAsyncEventArgs args)
        {
            var socket = (Socket)args.UserToken;
            if (HandleReceive(socket, args))
            {
                BeginReceive(socket, args);
            }
        }

        /// <returns>应继续接收时返回 true。</returns>
        bool HandleReceive(Socket socket, SocketAsyncEventArgs args)
        {
            if (!IsCurrent(socket))
            {
                return false;
            }

            if (args.SocketError == SocketError.ConnectionReset)
            {
                return true;
            }

            if (args.SocketError != SocketError.Success)
            {
                _listener.OnTransportError(args.SocketError.ToString());
                return false;
            }

            if (args.BytesTransferred > 0)
            {
                _listener.OnDatagram(args.Buffer, args.Offset, args.BytesTransferred);
            }

            return true;
        }

        bool IsCurrent(Socket socket)
        {
            lock (_sync)
            {
                return !_closed && _socket == socket;
            }
        }
    }
}
#endif
