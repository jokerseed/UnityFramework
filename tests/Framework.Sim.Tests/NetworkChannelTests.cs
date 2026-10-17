using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Framework.Events;
using Framework.Network;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    /// <summary>网络频道的本机回环测试：TCP 走真实 socket，KCP 用内存数据报传输。</summary>
    public sealed class NetworkChannelTests
    {
        const int WaitMilliseconds = 5000;

        readonly List<NetworkErrorEvent> _errors = new List<NetworkErrorEvent>();
        readonly List<NetworkClosedEvent> _closed = new List<NetworkClosedEvent>();
        IDisposable _errorSubscription;
        IDisposable _closedSubscription;

        [SetUp]
        public void SetUp()
        {
            _errors.Clear();
            _closed.Clear();
            _errorSubscription = GameEvent.Subscribe<NetworkErrorEvent>(_errors.Add);
            _closedSubscription = GameEvent.Subscribe<NetworkClosedEvent>(_closed.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _errorSubscription.Dispose();
            _closedSubscription.Dispose();
        }

        [Test]
        public void Tcp_SendAndReceive_RoundTrip()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var channel = new TcpNetworkChannel("tcp-echo", new DefaultNetworkChannelHelper());
                channel.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
                using (var server = listener.AcceptTcpClient())
                {
                    PumpUntil(channel, () => channel.Connected);
                    byte[] received = null;
                    channel.RegisterHandler(7, (_, packet) =>
                    {
                        received = new byte[packet.Length];
                        Buffer.BlockCopy(packet.Payload, packet.Offset, received, 0, packet.Length);
                    });

                    channel.Send(7, new byte[] { 1, 2, 3 });
                    var stream = server.GetStream();
                    var frame = ReadFrame(stream);
                    stream.Write(frame, 0, frame.Length);

                    PumpUntil(channel, () => received != null);
                    CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, received);
                }

                channel.Shutdown();
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void Tcp_ConnectByHostName_ResolvesWithoutBlocking()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var channel = new TcpNetworkChannel("tcp-dns", new DefaultNetworkChannelHelper());
                channel.Connect("localhost", ((IPEndPoint)listener.LocalEndpoint).Port);
                Assert.AreEqual(NetworkChannelState.Connecting, channel.State, "解析在后台进行，调用返回时应处于 Connecting");
                using (listener.AcceptTcpClient())
                {
                    PumpUntil(channel, () => channel.Connected);
                }

                channel.Shutdown();
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void Tcp_MissedHeartbeats_CloseChannel()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var channel = new TcpNetworkChannel("tcp-heartbeat", new DefaultNetworkChannelHelper())
                {
                    HeartBeatInterval = 0.1f,
                    MaxMissHeartBeatCount = 2,
                };
                channel.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
                using (listener.AcceptTcpClient())
                {
                    PumpUntil(channel, () => channel.Connected);

                    // 服务端从不回包：两次心跳间隔之后频道应主动断开。
                    for (var i = 0; i < 4 && channel.Connected; i++)
                    {
                        channel.Update(0.11f);
                    }

                    channel.Update(0f);
                    Assert.IsFalse(channel.Connected);
                    Assert.IsTrue(_errors.Exists(e => e.ChannelName == "tcp-heartbeat" && e.ErrorCode == NetworkErrorCode.HeartBeatTimeout));
                    Assert.IsTrue(_closed.Exists(e => e.ChannelName == "tcp-heartbeat"));
                }

                channel.Shutdown();
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void Tcp_ConnectTimeout_ReportsErrorAndCloses()
        {
            var channel = new TcpNetworkChannel("tcp-timeout", new DefaultNetworkChannelHelper())
            {
                ConnectTimeoutSeconds = 0.2f,
            };

            // 不可路由地址：要么一直挂起直到超时，要么系统立刻拒绝；两种情况都必须回到 Disconnected 并报错。
            channel.Connect(IPAddress.Parse("10.255.255.1"), 9);
            channel.Update(0.3f);
            PumpUntil(channel, () => channel.State == NetworkChannelState.Disconnected);
            channel.Update(0f);
            Assert.IsTrue(_errors.Exists(e => e.ChannelName == "tcp-timeout" && e.ErrorCode == NetworkErrorCode.ConnectError));
            channel.Shutdown();
        }

        [Test]
        public void Kcp_LoopbackPair_RoundTrip()
        {
            var link = new LoopbackLink();
            var client = new KcpNetworkChannel("kcp-a", new DefaultNetworkChannelHelper(), link.CreateA, 1);
            var server = new KcpNetworkChannel("kcp-b", new DefaultNetworkChannelHelper(), link.CreateB, 1);
            client.Connect(IPAddress.Loopback, 9000);
            server.Connect(IPAddress.Loopback, 9001);
            byte[] received = null;
            server.RegisterHandler(9, (_, packet) =>
            {
                received = new byte[packet.Length];
                Buffer.BlockCopy(packet.Payload, packet.Offset, received, 0, packet.Length);
            });

            client.Send(9, new byte[] { 4, 5, 6, 7 });
            var deadline = Environment.TickCount + WaitMilliseconds;
            while (received == null && Environment.TickCount < deadline)
            {
                client.Update(0.01f);
                server.Update(0.01f);
                Thread.Sleep(5);
            }

            CollectionAssert.AreEqual(new byte[] { 4, 5, 6, 7 }, received);
            client.Shutdown();
            server.Shutdown();
        }

        [Test]
        public void Kcp_NoResponse_ClosesAfterResponseTimeout()
        {
            var link = new LoopbackLink();
            var client = new KcpNetworkChannel("kcp-silent", new DefaultNetworkChannelHelper(), link.CreateA, 1)
            {
                ResponseTimeoutSeconds = 0.5f,
            };

            client.Connect(IPAddress.Loopback, 9000);
            Assert.IsTrue(client.Connected);
            client.Update(0.3f);
            Assert.IsTrue(client.Connected);
            client.Update(0.3f);
            client.Update(0f);
            Assert.IsFalse(client.Connected);
            Assert.IsTrue(_errors.Exists(e => e.ChannelName == "kcp-silent" && e.ErrorCode == NetworkErrorCode.HeartBeatTimeout));
            client.Shutdown();
        }

        static void PumpUntil(INetworkChannelDriver channel, Func<bool> condition)
        {
            var deadline = Environment.TickCount + WaitMilliseconds;
            while (!condition())
            {
                if (Environment.TickCount > deadline)
                {
                    Assert.Fail("Timed out waiting for channel.");
                }

                channel.Update(0.01f);
                Thread.Sleep(5);
            }
        }

        static byte[] ReadFrame(NetworkStream stream)
        {
            var header = ReadExactly(stream, 4);
            var body = BitConverter.ToInt32(header, 0);
            var payload = ReadExactly(stream, body);
            var frame = new byte[4 + body];
            Buffer.BlockCopy(header, 0, frame, 0, 4);
            Buffer.BlockCopy(payload, 0, frame, 4, body);
            return frame;
        }

        static byte[] ReadExactly(NetworkStream stream, int count)
        {
            var buffer = new byte[count];
            var read = 0;
            stream.ReadTimeout = WaitMilliseconds;
            while (read < count)
            {
                var n = stream.Read(buffer, read, count - read);
                if (n <= 0)
                {
                    throw new InvalidOperationException("Connection closed.");
                }

                read += n;
            }

            return buffer;
        }

        /// <summary>两端内存数据报：A 发出的交给 B 的监听器，反之亦然。未设置对端时数据报被丢弃。</summary>
        sealed class LoopbackLink
        {
            IKcpDatagramListener _a;
            IKcpDatagramListener _b;

            public IKcpDatagramTransport CreateA(IKcpDatagramListener listener)
            {
                _a = listener;
                return new Endpoint(() => _b);
            }

            public IKcpDatagramTransport CreateB(IKcpDatagramListener listener)
            {
                _b = listener;
                return new Endpoint(() => _a);
            }

            sealed class Endpoint : IKcpDatagramTransport
            {
                readonly Func<IKcpDatagramListener> _peer;

                public Endpoint(Func<IKcpDatagramListener> peer)
                {
                    _peer = peer;
                }

                public void Start(IPEndPoint remote)
                {
                }

                public void Send(byte[] datagram, int offset, int count)
                {
                    // 对端监听器会自行拷贝；这里传进去后 KCP 会立刻复用缓冲。
                    _peer()?.OnDatagram(datagram, offset, count);
                }

                public void Close()
                {
                }
            }
        }
    }
}
