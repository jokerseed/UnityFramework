using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Events;
using Framework.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Framework.Tests
{
    /// <summary>WebSocket 频道的帧解析/生命周期，以及 RPC 的匹配、超时、取消、断线。</summary>
    public sealed class NetworkWebSocketRpcTests
    {
        [Test]
        public void WebSocketFrameIsDispatchedOnUpdate()
        {
            var transport = new FakeTransport();
            var channel = new WebSocketNetworkChannel("ws-test", new DefaultNetworkChannelHelper(), l => transport.Bind(l));
            try
            {
                string received = null;
                channel.RegisterHandler(7, (c, p) => received = Encoding.UTF8.GetString(p.Payload, p.Offset, p.Length));
                channel.Connect(new Uri("ws://127.0.0.1:9000/game"));
                Assert.AreEqual(NetworkChannelState.Connecting, channel.State);

                transport.Open();
                Assert.IsTrue(channel.Connected);

                transport.Receive(Serialize(7, "hi"));
                Assert.IsNull(received, "handler must wait for Update");
                channel.Update(0f);
                Assert.AreEqual("hi", received);
                Assert.AreEqual(1, channel.ReceivedPacketCount);

                channel.Send(8, Encoding.UTF8.GetBytes("out"));
                Assert.AreEqual(1, transport.Sent.Count);
                CollectionAssert.AreEqual(Serialize(8, "out"), transport.Sent[0]);
                Assert.AreEqual(1, channel.SentPacketCount);
            }
            finally
            {
                channel.Shutdown();
            }
        }

        [Test]
        public void MalformedFrameClosesConnectionOnce()
        {
            var transport = new FakeTransport();
            var channel = new WebSocketNetworkChannel("ws-bad", new DefaultNetworkChannelHelper(), l => transport.Bind(l));
            var closed = 0;
            Action<NetworkClosedEvent> onClosed = e =>
            {
                if (e.ChannelName == "ws-bad")
                {
                    closed++;
                }
            };
            GameEvent.Listen(onClosed);
            try
            {
                channel.Connect("127.0.0.1", 9000);
                transport.Open();
                var frame = Serialize(7, "hi");
                transport.Receive(frame, frame.Length - 1);

                Assert.AreEqual(NetworkChannelState.Disconnected, channel.State);
                Assert.IsTrue(transport.Closed);
                LogAssert.Expect(LogType.Error, new Regex("DeserializeError"));
                channel.Update(0f);
                Assert.AreEqual(1, closed);

                // 已被替换的传输再回调，不影响频道。
                transport.RaiseClosed();
                channel.Update(0f);
                Assert.AreEqual(1, closed);
            }
            finally
            {
                GameEvent.RemoveListener(onClosed);
                channel.Shutdown();
            }
        }

        [Test]
        public void ConnectWithIpAddressBuildsWsUri()
        {
            var transport = new FakeTransport();
            var channel = new WebSocketNetworkChannel("ws-ip", new DefaultNetworkChannelHelper(), l => transport.Bind(l));
            try
            {
                channel.Connect(IPAddress.Loopback, 8080);
                Assert.AreEqual(new Uri("ws://127.0.0.1:8080/"), transport.ConnectedUri);
            }
            finally
            {
                channel.Shutdown();
            }
        }

        [UnityTest]
        public IEnumerator RpcMatchesResponseByRequestId() => UniTask.ToCoroutine(async () =>
        {
            var channel = new FakeChannel("rpc-ok");
            using (var rpc = new NetworkRpcClient(channel))
            {
                var call = rpc.CallAsync(100, 101, Encoding.UTF8.GetBytes("ping"));
                Assert.AreEqual(1, channel.Sent.Count);
                var request = channel.Sent[0];
                Assert.AreEqual(100, request.MessageId);
                var requestId = NetworkRpcClient.ReadRequestId(request.Payload, 0);
                Assert.AreEqual("ping", Encoding.UTF8.GetString(request.Payload, NetworkRpcClient.RequestIdSize, 4));

                channel.Deliver(101, NetworkRpcClient.Frame(requestId + 1000, Encoding.UTF8.GetBytes("stale")));
                channel.Deliver(101, NetworkRpcClient.Frame(requestId, Encoding.UTF8.GetBytes("pong")));
                var response = await call;
                Assert.AreEqual("pong", Encoding.UTF8.GetString(response));
                Assert.AreEqual(0, rpc.PendingCount);
            }

            Assert.IsFalse(channel.HasHandler(101), "Dispose must unregister response handler");
        });

        [UnityTest]
        public IEnumerator RpcTimesOut() => UniTask.ToCoroutine(async () =>
        {
            var channel = new FakeChannel("rpc-timeout");
            using (var rpc = new NetworkRpcClient(channel))
            {
                var timedOut = false;
                try
                {
                    await rpc.CallAsync(100, 101, null, timeoutSeconds: 0.05f);
                }
                catch (TimeoutException)
                {
                    timedOut = true;
                }

                Assert.IsTrue(timedOut);
                Assert.AreEqual(0, rpc.PendingCount);
            }
        });

        [UnityTest]
        public IEnumerator RpcCancelAndChannelClose() => UniTask.ToCoroutine(async () =>
        {
            var channel = new FakeChannel("rpc-close");
            using (var rpc = new NetworkRpcClient(channel, defaultTimeoutSeconds: 0f))
            using (var cts = new CancellationTokenSource())
            {
                var cancelledCall = rpc.CallAsync(100, 101, null, cancellationToken: cts.Token);
                cts.Cancel();
                var cancelled = false;
                try
                {
                    await cancelledCall;
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }

                Assert.IsTrue(cancelled);

                var closedCall = rpc.CallAsync(100, 101, null);
                GameEvent.Send(new NetworkClosedEvent { ChannelName = "rpc-close" });
                var failed = false;
                try
                {
                    await closedCall;
                }
                catch (InvalidOperationException)
                {
                    failed = true;
                }

                Assert.IsTrue(failed);
                Assert.AreEqual(0, rpc.PendingCount);
            }
        });

        static byte[] Serialize(ushort messageId, string text)
        {
            var stream = new System.IO.MemoryStream();
            var payload = Encoding.UTF8.GetBytes(text);
            new DefaultNetworkChannelHelper().SerializePacket(NetworkPacket.Create(messageId, payload), stream);
            return stream.ToArray();
        }

        sealed class FakeTransport : IWebSocketTransport
        {
            IWebSocketTransportListener _listener;
            public readonly List<byte[]> Sent = new List<byte[]>();
            public Uri ConnectedUri;
            public bool Closed;

            public IWebSocketTransport Bind(IWebSocketTransportListener listener)
            {
                _listener = listener;
                return this;
            }

            public void Connect(Uri uri) => ConnectedUri = uri;

            public void Send(byte[] data)
            {
                Sent.Add(data);
                _listener.OnTransportSent(this);
            }

            public void Close() => Closed = true;

            public void Open() => _listener.OnTransportOpen(this);

            public void Receive(byte[] frame) => Receive(frame, frame.Length);

            public void Receive(byte[] frame, int length) => _listener.OnTransportMessage(this, frame, length);

            public void RaiseClosed() => _listener.OnTransportClosed(this, "late");
        }

        sealed class FakeChannel : INetworkChannel
        {
            readonly Dictionary<ushort, Action<INetworkChannel, NetworkPacket>> _handlers =
                new Dictionary<ushort, Action<INetworkChannel, NetworkPacket>>();

            public FakeChannel(string name)
            {
                Name = name;
            }

            public readonly List<(ushort MessageId, byte[] Payload)> Sent = new List<(ushort, byte[])>();
            public string Name { get; }
            public NetworkChannelState State => NetworkChannelState.Connected;
            public bool Connected => true;
            public NetworkServiceType ServiceType => NetworkServiceType.WebSocket;
            public float HeartBeatInterval { get; set; }
            public bool ResetHeartBeatElapseSecondsWhenReceivePacket { get; set; }
            public int MissHeartBeatCount => 0;
            public int MaxMissHeartBeatCount { get; set; }
            public int SentPacketCount => Sent.Count;
            public int ReceivedPacketCount => 0;

            public void Connect(string host, int port) => throw new NotSupportedException();

            public void Connect(IPAddress address, int port) => throw new NotSupportedException();

            public void Close()
            {
            }

            public void Send(NetworkPacket packet) => throw new NotSupportedException();

            public void Send(ushort messageId, byte[] payload) => Sent.Add((messageId, payload));

            public void RegisterHandler(INetworkPacketHandler handler) => throw new NotSupportedException();

            public void RegisterHandler(ushort messageId, Action<INetworkChannel, NetworkPacket> handler) => _handlers[messageId] = handler;

            public bool UnregisterHandler(ushort messageId) => _handlers.Remove(messageId);

            public void SetDefaultHandler(Action<INetworkChannel, NetworkPacket> handler)
            {
            }

            public bool HasHandler(ushort messageId) => _handlers.ContainsKey(messageId);

            public void Deliver(ushort messageId, byte[] payload)
            {
                var packet = NetworkPacket.Create(messageId, payload);
                try
                {
                    _handlers[messageId](this, packet);
                }
                finally
                {
                    MemoryPool.MemoryPool.Release(packet);
                }
            }
        }
    }
}
