using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Events;
using Framework.Logging;

namespace Framework.Network
{
    /// <summary>
    /// 请求-响应 RPC：在载荷前加 4 字节小端请求号，服务端把同一请求号放在响应载荷前面回发。
    /// 支持超时、取消；频道关闭时所有等待中的调用以 <see cref="InvalidOperationException"/> 结束。
    /// 主线程使用；用完须 <see cref="Dispose"/>，以注销响应处理器和事件监听。
    /// </summary>
    public sealed class NetworkRpcClient : IDisposable
    {
        /// <summary>请求号占用的字节数。</summary>
        public const int RequestIdSize = 4;

        readonly INetworkChannel _channel;
        readonly Dictionary<uint, UniTaskCompletionSource<byte[]>> _pending = new Dictionary<uint, UniTaskCompletionSource<byte[]>>();
        readonly HashSet<ushort> _responseIds = new HashSet<ushort>();
        readonly Action<INetworkChannel, NetworkPacket> _onResponse;
        uint _lastRequestId;
        bool _disposed;

        /// <summary>创建 RPC 客户端。</summary>
        /// <param name="channel">承载 RPC 的频道。</param>
        /// <param name="defaultTimeoutSeconds">默认超时（秒）；小于等于 0 表示不超时。</param>
        public NetworkRpcClient(INetworkChannel channel, float defaultTimeoutSeconds = 10f)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            DefaultTimeoutSeconds = defaultTimeoutSeconds;
            _onResponse = OnResponse;
            GameEvent.Listen<NetworkClosedEvent>(OnChannelClosed);
        }

        /// <summary>默认超时（秒）；小于等于 0 表示不超时。</summary>
        public float DefaultTimeoutSeconds { get; set; }

        /// <summary>等待响应中的调用数。</summary>
        public int PendingCount => _pending.Count;

        /// <summary>
        /// 发送请求并等待响应。首次使用某个响应消息号时会在频道上注册它的处理器（覆盖已有处理器）。
        /// </summary>
        /// <param name="requestMessageId">请求消息号。</param>
        /// <param name="responseMessageId">响应消息号。</param>
        /// <param name="payload">请求载荷；可为 null。</param>
        /// <param name="timeoutSeconds">超时（秒）；为 null 时用 <see cref="DefaultTimeoutSeconds"/>，小于等于 0 表示不超时。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>响应载荷（不含请求号）。</returns>
        /// <exception cref="InvalidOperationException">频道未连接，或等待期间频道关闭。</exception>
        /// <exception cref="TimeoutException">超时未收到响应。</exception>
        /// <exception cref="OperationCanceledException">已取消。</exception>
        public async UniTask<byte[]> CallAsync(
            ushort requestMessageId,
            ushort responseMessageId,
            byte[] payload,
            float? timeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetworkRpcClient));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!_channel.Connected)
            {
                throw new InvalidOperationException($"[Network] RPC channel '{_channel.Name}' is not connected.");
            }

            if (_responseIds.Add(responseMessageId))
            {
                _channel.RegisterHandler(responseMessageId, _onResponse);
            }

            var requestId = NextRequestId();
            var source = new UniTaskCompletionSource<byte[]>();
            _pending.Add(requestId, source);
            var timeout = timeoutSeconds ?? DefaultTimeoutSeconds;
            var timeoutCts = new CancellationTokenSource();
            try
            {
                _channel.Send(requestMessageId, Frame(requestId, payload));
                if (timeout > 0f)
                {
                    WatchTimeout(source, requestId, timeout, timeoutCts.Token).Forget();
                }

                using (cancellationToken.Register(() => source.TrySetCanceled(cancellationToken)))
                {
                    return await source.Task;
                }
            }
            finally
            {
                _pending.Remove(requestId);
                timeoutCts.Cancel();
                timeoutCts.Dispose();
            }
        }

        /// <summary>注销响应处理器与事件监听；等待中的调用以 <see cref="ObjectDisposedException"/> 结束。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            GameEvent.RemoveListener<NetworkClosedEvent>(OnChannelClosed);
            foreach (var id in _responseIds)
            {
                _channel.UnregisterHandler(id);
            }

            _responseIds.Clear();
            FailAll(new ObjectDisposedException(nameof(NetworkRpcClient)));
        }

        /// <summary>在载荷前写入小端请求号。</summary>
        /// <param name="requestId">请求号。</param>
        /// <param name="payload">载荷；可为 null。</param>
        /// <returns>新数组。</returns>
        public static byte[] Frame(uint requestId, byte[] payload)
        {
            var length = payload?.Length ?? 0;
            var bytes = new byte[RequestIdSize + length];
            bytes[0] = (byte)requestId;
            bytes[1] = (byte)(requestId >> 8);
            bytes[2] = (byte)(requestId >> 16);
            bytes[3] = (byte)(requestId >> 24);
            if (length > 0)
            {
                Buffer.BlockCopy(payload, 0, bytes, RequestIdSize, length);
            }

            return bytes;
        }

        /// <summary>读取载荷开头的小端请求号。</summary>
        /// <param name="buffer">缓冲。</param>
        /// <param name="offset">起始偏移，须至少有 <see cref="RequestIdSize"/> 字节。</param>
        /// <returns>请求号。</returns>
        public static uint ReadRequestId(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24));
        }

        uint NextRequestId()
        {
            do
            {
                _lastRequestId++;
            }
            while (_lastRequestId == 0 || _pending.ContainsKey(_lastRequestId));

            return _lastRequestId;
        }

        void OnResponse(INetworkChannel channel, NetworkPacket packet)
        {
            if (packet.Payload == null || packet.Length < RequestIdSize)
            {
                GameLog.Error(LogCategories.Network, $"RPC response {LogStyle.Value(packet.MessageId)} has no request id.");
                return;
            }

            var requestId = ReadRequestId(packet.Payload, packet.Offset);
            if (!_pending.TryGetValue(requestId, out var source))
            {
                GameLog.Debug(LogCategories.Network, $"RPC response {LogStyle.Value(requestId)} arrived after timeout or cancel.");
                return;
            }

            var body = new byte[packet.Length - RequestIdSize];
            Buffer.BlockCopy(packet.Payload, packet.Offset + RequestIdSize, body, 0, body.Length);
            source.TrySetResult(body);
        }

        void OnChannelClosed(NetworkClosedEvent e)
        {
            if (e.ChannelName == _channel.Name)
            {
                FailAll(new InvalidOperationException($"[Network] RPC channel '{_channel.Name}' closed."));
            }
        }

        void FailAll(Exception exception)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            var sources = new List<UniTaskCompletionSource<byte[]>>(_pending.Values);
            for (var i = 0; i < sources.Count; i++)
            {
                sources[i].TrySetException(exception);
            }
        }

        static async UniTaskVoid WatchTimeout(UniTaskCompletionSource<byte[]> source, uint requestId, float seconds, CancellationToken token)
        {
            var cancelled = await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime, PlayerLoopTiming.Update, token)
                .SuppressCancellationThrow();
            if (!cancelled)
            {
                source.TrySetException(new TimeoutException($"[Network] RPC {requestId} timed out after {seconds}s."));
            }
        }
    }
}
