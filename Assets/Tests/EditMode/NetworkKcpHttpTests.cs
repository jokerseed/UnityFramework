using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Framework.Events;
using Framework.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Framework.Tests
{
    /// <summary>KCP 频道的收发与坏包关闭，以及 HTTP 短请求。</summary>
    public sealed class NetworkKcpHttpTests
    {
        [Test]
        public void KcpMessageIsDeliveredOnUpdate()
        {
            QueueTransport transportA = null;
            QueueTransport transportB = null;
            var channelA = new KcpNetworkChannel("kcp-a", new DefaultNetworkChannelHelper(), listener => transportA = new QueueTransport(listener), 7);
            var channelB = new KcpNetworkChannel("kcp-b", new DefaultNetworkChannelHelper(), listener => transportB = new QueueTransport(listener), 7);
            try
            {
                string received = null;
                channelB.RegisterHandler(7, (c, p) => received = Encoding.UTF8.GetString(p.Payload, p.Offset, p.Length));
                channelA.Connect(IPAddress.Loopback, 9000);
                channelB.Connect(IPAddress.Loopback, 9000);
                Assert.IsTrue(channelA.Connected);
                Assert.AreEqual(NetworkServiceType.Kcp, channelA.ServiceType);

                channelA.Send(7, Encoding.UTF8.GetBytes("hi"));
                Assert.AreEqual(1, channelA.SentPacketCount);
                Assert.IsNull(received, "handler must wait until the peer pumps");

                for (var i = 0; i < 4 && received == null; i++)
                {
                    transportA.DeliverTo(transportB);
                    channelB.Update(0.02f);
                    transportB.DeliverTo(transportA);
                    channelA.Update(0.02f);
                }

                Assert.AreEqual("hi", received);
                Assert.AreEqual(1, channelB.ReceivedPacketCount);
            }
            finally
            {
                channelA.Shutdown();
                channelB.Shutdown();
            }
        }

        [Test]
        public void MalformedDatagramClosesOnce()
        {
            QueueTransport transport = null;
            var channel = new KcpNetworkChannel("kcp-bad", new DefaultNetworkChannelHelper(), listener => transport = new QueueTransport(listener), 1);
            var closed = 0;
            Action<NetworkClosedEvent> onClosed = e =>
            {
                if (e.ChannelName == "kcp-bad")
                {
                    closed++;
                }
            };
            GameEvent.Listen(onClosed);
            try
            {
                channel.Connect(IPAddress.Loopback, 9000);
                transport.Push(new byte[] { 1, 2, 3, 4 });
                LogAssert.Expect(LogType.Error, new Regex("ReceiveError"));
                channel.Update(0.02f);
                Assert.AreEqual(NetworkChannelState.Disconnected, channel.State);
                Assert.AreEqual(1, closed);

                channel.Update(0.02f);
                Assert.AreEqual(1, closed);
            }
            finally
            {
                GameEvent.RemoveListener(onClosed);
                channel.Shutdown();
            }
        }

        [UnityTest]
        public IEnumerator HttpRejectsNonHttpUrl() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                await HttpRequests.GetAsync("ftp://127.0.0.1/file");
                Assert.Fail("expected ArgumentException");
            }
            catch (ArgumentException)
            {
            }
        });

        [UnityTest]
        public IEnumerator HttpGetAndPostRoundTrip() => UniTask.ToCoroutine(async () =>
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task serve = Task.Run(() => ServeOne(listener, "pong"));
            try
            {
                var got = await HttpRequests.GetAsync($"http://127.0.0.1:{port}/ping");
                Assert.IsTrue(got.IsSuccess, "status " + got.StatusCode);
                Assert.AreEqual("pong", Encoding.UTF8.GetString(got.Body));
                await serve;
            }
            finally
            {
                listener.Stop();
            }

            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
            byte[] posted = null;
            serve = Task.Run(() =>
            {
                posted = ServeOne(listener, "ok");
            });
            try
            {
                var body = Encoding.UTF8.GetBytes("payload");
                var got = await HttpRequests.PostAsync($"http://127.0.0.1:{port}/submit", body, "text/plain", 5f);
                Assert.IsTrue(got.IsSuccess, "status " + got.StatusCode);
                Assert.AreEqual("ok", Encoding.UTF8.GetString(got.Body));
                await serve;
                var request = Encoding.UTF8.GetString(posted);
                StringAssert.Contains("POST /submit", request);
                StringAssert.Contains("payload", request);
            }
            finally
            {
                listener.Stop();
            }
        });

        static byte[] ServeOne(TcpListener listener, string responseBody)
        {
            using (var client = listener.AcceptTcpClient())
            using (var stream = client.GetStream())
            {
                stream.ReadTimeout = 3000;
                var request = ReadRequest(stream);
                WriteResponse(stream, responseBody);
                return request;
            }
        }

        static byte[] ReadRequest(NetworkStream stream)
        {
            var buffer = new MemoryStream();
            var chunk = new byte[1024];
            while (buffer.Length < 8192)
            {
                var n = stream.Read(chunk, 0, chunk.Length);
                if (n <= 0)
                {
                    break;
                }

                buffer.Write(chunk, 0, n);
                var text = Encoding.ASCII.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
                var headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerEnd < 0)
                {
                    continue;
                }

                var bodyLength = 0;
                const string marker = "Content-Length:";
                var lengthAt = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (lengthAt >= 0 && lengthAt < headerEnd)
                {
                    var lineEnd = text.IndexOf("\r\n", lengthAt, StringComparison.Ordinal);
                    var digits = text.Substring(lengthAt + marker.Length, lineEnd - (lengthAt + marker.Length)).Trim();
                    int.TryParse(digits, out bodyLength);
                }

                if (buffer.Length >= headerEnd + 4 + bodyLength)
                {
                    break;
                }
            }

            return buffer.ToArray();
        }

        static void WriteResponse(NetworkStream stream, string body)
        {
            var payload = Encoding.UTF8.GetBytes(body);
            var header = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
            stream.Write(header, 0, header.Length);
            stream.Write(payload, 0, payload.Length);
        }

        sealed class QueueTransport : IKcpDatagramTransport
        {
            readonly List<byte[]> _outgoing = new List<byte[]>();
            readonly IKcpDatagramListener _listener;

            public QueueTransport(IKcpDatagramListener listener)
            {
                _listener = listener;
            }

            public void Start(IPEndPoint remote)
            {
            }

            public void Send(byte[] datagram, int offset, int count)
            {
                var copy = new byte[count];
                Buffer.BlockCopy(datagram, offset, copy, 0, count);
                _outgoing.Add(copy);
            }

            public void Close()
            {
            }

            public void Push(byte[] datagram)
            {
                _listener.OnDatagram(datagram, 0, datagram.Length);
            }

            public void DeliverTo(QueueTransport peer)
            {
                for (var i = 0; i < _outgoing.Count; i++)
                {
                    peer.Push(_outgoing[i]);
                }

                _outgoing.Clear();
            }
        }
    }
}
