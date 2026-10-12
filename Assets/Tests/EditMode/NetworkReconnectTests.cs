using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Framework.Network;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>关掉后立刻重连，旧的收包回调不能拆掉新连接。</summary>
    public sealed class NetworkReconnectTests
    {
        [Test]
        public void CloseThenConnectKeepsTheNewSocket()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            TcpNetworkChannel channel = null;
            Socket first = null;
            Socket second = null;
            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                channel = new TcpNetworkChannel("reconnect", new DefaultNetworkChannelHelper());
                channel.Connect(IPAddress.Loopback, port);
                first = AcceptOrFail(listener, "first accept");
                Assert.IsTrue(WaitUntil(() => channel.Connected), "first connect");

                var previousReceive = ReceiveArgs(channel);
                channel.Close();
                channel.Update(0f);
                channel.Connect(IPAddress.Loopback, port);
                second = AcceptOrFail(listener, "second accept");
                Assert.IsTrue(WaitUntil(() => channel.Connected), "second connect");
                var current = CurrentSession(channel);

                // 直接把上一套连接的接收完成送进来，不靠线程池什么时候跑到。
                CompleteReceive(channel, previousReceive);
                channel.Update(0f);
                Assert.IsTrue(channel.Connected);
                Assert.AreSame(current, CurrentSession(channel));
            }
            finally
            {
                if (first != null)
                {
                    first.Close();
                }

                if (second != null)
                {
                    second.Close();
                }

                channel?.Shutdown();
                listener.Stop();
            }
        }

        static object CurrentSession(TcpNetworkChannel channel)
        {
            return typeof(TcpNetworkChannel).GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(channel);
        }

        static SocketAsyncEventArgs ReceiveArgs(TcpNetworkChannel channel)
        {
            var session = CurrentSession(channel);
            Assert.IsNotNull(session);
            return (SocketAsyncEventArgs)session.GetType().GetField("ReceiveArgs").GetValue(session);
        }

        static void CompleteReceive(TcpNetworkChannel channel, SocketAsyncEventArgs args)
        {
            var method = typeof(TcpNetworkChannel).GetMethod(
                "OnReceiveCompleted",
                BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(channel, new object[] { args });
        }

        static Socket AcceptOrFail(TcpListener listener, string message)
        {
            var pending = listener.BeginAcceptSocket(null, null);
            if (!pending.AsyncWaitHandle.WaitOne(2000))
            {
                Assert.Fail(message);
            }

            return listener.EndAcceptSocket(pending);
        }

        static bool WaitUntil(System.Func<bool> condition)
        {
            for (var i = 0; i < 50; i++)
            {
                if (condition())
                {
                    return true;
                }

                Thread.Sleep(10);
            }

            return condition();
        }
    }
}
