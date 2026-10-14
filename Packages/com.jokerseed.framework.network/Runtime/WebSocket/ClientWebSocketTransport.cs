#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.Network
{
    /// <summary>基于 <see cref="ClientWebSocket"/> 的传输：接收循环和发送链都在线程池上，回调由频道转回主线程。</summary>
    internal sealed class ClientWebSocketTransport : IWebSocketTransport
    {
        const int ReceiveChunkSize = 8192;
        static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

        readonly IWebSocketTransportListener _listener;
        readonly int _maxMessageSize;
        readonly ClientWebSocket _socket = new ClientWebSocket();
        readonly CancellationTokenSource _cts = new CancellationTokenSource();
        readonly object _sendLock = new object();
        Task _sendChain = Task.CompletedTask;
        int _closed;

        public ClientWebSocketTransport(IWebSocketTransportListener listener, int maxMessageSize)
        {
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
            _maxMessageSize = maxMessageSize;
        }

        public void Connect(Uri uri)
        {
            _ = RunAsync(uri);
        }

        public void Send(byte[] data)
        {
            lock (_sendLock)
            {
                _sendChain = SendAfterAsync(_sendChain, data);
            }
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
            {
                return;
            }

            lock (_sendLock)
            {
                _sendChain = CloseAfterAsync(_sendChain);
            }
        }

        async Task RunAsync(Uri uri)
        {
            try
            {
                await _socket.ConnectAsync(uri, _cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Fail(NetworkErrorCode.ConnectError, ex.Message);
                return;
            }

            if (Volatile.Read(ref _closed) != 0)
            {
                return;
            }

            _listener.OnTransportOpen(this);
            var chunk = new byte[ReceiveChunkSize];
            var message = new MemoryStream(ReceiveChunkSize);
            try
            {
                while (true)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(chunk), _cts.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Finish($"closed by remote ({result.CloseStatus} {result.CloseStatusDescription})");
                        return;
                    }

                    if (result.MessageType != WebSocketMessageType.Binary)
                    {
                        Fail(NetworkErrorCode.DeserializeError, "Text frames are not supported; send binary frames.");
                        return;
                    }

                    message.Write(chunk, 0, result.Count);
                    if (message.Length > _maxMessageSize)
                    {
                        Fail(NetworkErrorCode.DeserializeError, $"Message exceeds {_maxMessageSize} bytes.");
                        return;
                    }

                    if (result.EndOfMessage)
                    {
                        _listener.OnTransportMessage(this, message.GetBuffer(), (int)message.Length);
                        message.SetLength(0);
                    }
                }
            }
            catch (Exception ex)
            {
                Fail(NetworkErrorCode.ReceiveError, ex.Message);
            }
        }

        async Task SendAfterAsync(Task previous, byte[] data)
        {
            await previous.ConfigureAwait(false);
            if (Volatile.Read(ref _closed) != 0)
            {
                return;
            }

            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, _cts.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Fail(NetworkErrorCode.SendError, ex.Message);
                return;
            }

            _listener.OnTransportSent(this);
        }

        async Task CloseAfterAsync(Task previous)
        {
            await previous.ConfigureAwait(false);
            try
            {
                if (_socket.State == WebSocketState.Open)
                {
                    using (var timeout = new CancellationTokenSource(CloseTimeout))
                    {
                        await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, timeout.Token)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex) when (ex is WebSocketException || ex is OperationCanceledException)
            {
                // 对端已断开或关闭握手超时：本端照常释放，主动关闭不再回调。
            }
            finally
            {
                _cts.Cancel();
                _socket.Dispose();
            }
        }

        void Fail(NetworkErrorCode errorCode, string message)
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
            {
                return;
            }

            _cts.Cancel();
            _socket.Abort();
            _socket.Dispose();
            _listener.OnTransportError(this, errorCode, message);
            _listener.OnTransportClosed(this, message);
        }

        void Finish(string reason)
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
            {
                return;
            }

            _cts.Cancel();
            _socket.Dispose();
            _listener.OnTransportClosed(this, reason);
        }
    }
}
#endif
