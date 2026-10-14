#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Net;
using System.Net.Sockets;

namespace Framework.Network
{
    /// <summary>用 <see cref="UdpClient"/> 收发 KCP 数据报。完成回调只通知监听器，不解析协议。</summary>
    public sealed class UdpKcpDatagramTransport : IKcpDatagramTransport
    {
        readonly IKcpDatagramListener _listener;
        readonly object _sync = new object();
        UdpClient _client;
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

            var client = new UdpClient(remote.AddressFamily);
            client.Connect(remote);
            lock (_sync)
            {
                if (_closed)
                {
                    client.Close();
                    return;
                }

                _client = client;
            }

            BeginReceive(client);
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

            UdpClient client;
            lock (_sync)
            {
                client = _closed ? null : _client;
            }

            if (client == null)
            {
                return;
            }

            try
            {
                if (offset == 0)
                {
                    client.Send(datagram, count);
                    return;
                }

                var payload = new byte[count];
                Buffer.BlockCopy(datagram, offset, payload, 0, count);
                client.Send(payload, payload.Length);
            }
            catch (Exception ex)
            {
                if (IsCurrent(client))
                {
                    _listener.OnTransportError(ex.Message);
                }
            }
        }

        /// <inheritdoc />
        public void Close()
        {
            UdpClient client;
            lock (_sync)
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                client = _client;
                _client = null;
            }

            try
            {
                client?.Close();
            }
            catch (SocketException)
            {
            }
        }

        void BeginReceive(UdpClient client)
        {
            try
            {
                client.BeginReceive(ar => OnReceive(client, ar), null);
            }
            catch (Exception ex)
            {
                if (IsCurrent(client))
                {
                    _listener.OnTransportError(ex.Message);
                }
            }
        }

        void OnReceive(UdpClient client, IAsyncResult ar)
        {
            if (!IsCurrent(client))
            {
                return;
            }

            try
            {
                IPEndPoint remote = null;
                var data = client.EndReceive(ar, ref remote);
                if (!IsCurrent(client))
                {
                    return;
                }

                if (data != null && data.Length > 0)
                {
                    _listener.OnDatagram(data, 0, data.Length);
                }

                if (IsCurrent(client))
                {
                    BeginReceive(client);
                }
            }
            catch (Exception ex)
            {
                if (IsCurrent(client))
                {
                    _listener.OnTransportError(ex.Message);
                }
            }
        }

        bool IsCurrent(UdpClient client)
        {
            lock (_sync)
            {
                return !_closed && _client == client;
            }
        }
    }
}
#endif
