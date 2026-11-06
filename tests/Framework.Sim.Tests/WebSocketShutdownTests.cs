using System;
using System.Reflection;
using System.Threading.Tasks;
using Framework.Network;
using NUnit.Framework;

namespace Framework.Tests
{
    public sealed class WebSocketShutdownTests
    {
        [Test]
        public async Task CloseDoesNotWaitForeverForStalledSend()
        {
            var listener = new Listener();
            var transport = new ClientWebSocketTransport(listener, 1024);
            var chain = typeof(ClientWebSocketTransport).GetField("_sendChain", BindingFlags.Instance | BindingFlags.NonPublic);
            var stalled = new TaskCompletionSource<bool>();
            chain.SetValue(transport, stalled.Task);
            transport.Close();
            var closed = (Task)chain.GetValue(transport);
            Assert.AreSame(closed, await Task.WhenAny(closed, Task.Delay(5000)));
            await closed;
            transport.Close();
            Assert.AreEqual(0, listener.Callbacks);
            stalled.SetResult(true);
        }

        [Test]
        public async Task CloseCleansUpEvenWhenSendChainFaulted()
        {
            var transport = new ClientWebSocketTransport(new Listener(), 1024);
            var chain = typeof(ClientWebSocketTransport).GetField("_sendChain", BindingFlags.Instance | BindingFlags.NonPublic);
            chain.SetValue(transport, Task.FromException(new InvalidOperationException("send failed")));
            transport.Close();
            await (Task)chain.GetValue(transport);
            var cancellation = (System.Threading.CancellationTokenSource)typeof(ClientWebSocketTransport)
                .GetField("_cts", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(transport);
            Assert.IsTrue(cancellation.IsCancellationRequested);
        }

        sealed class Listener : IWebSocketTransportListener
        {
            public int Callbacks;
            public void OnTransportOpen(IWebSocketTransport transport) => Callbacks++;
            public void OnTransportMessage(IWebSocketTransport transport, byte[] data, int length) => Callbacks++;
            public void OnTransportSent(IWebSocketTransport transport) => Callbacks++;
            public void OnTransportError(IWebSocketTransport transport, NetworkErrorCode errorCode, string message) => Callbacks++;
            public void OnTransportClosed(IWebSocketTransport transport, string reason) => Callbacks++;
        }
    }
}
