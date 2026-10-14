using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Logging;
using UnityEngine;
using UnityEngine.Networking;

namespace Framework.Network
{
    /// <summary>
    /// HTTP 短请求。不实现 <see cref="INetworkChannel"/>：没有长连接、心跳和消息号。
    /// 登录、拉配置、补丁清单用这里；对局收发仍走 TCP / WebSocket / KCP 频道。
    /// </summary>
    public static class HttpRequests
    {
        /// <summary>未指定超时时使用的秒数。</summary>
        public const float DefaultTimeoutSeconds = 30f;

        /// <summary>GET。</summary>
        /// <param name="url">绝对 http 或 https 地址。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>含状态码和正文的响应。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="url"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="url"/> 不是绝对的 http/https 地址。</exception>
        /// <exception cref="InvalidOperationException">连不上或传输失败。HTTP 4xx / 5xx 不抛，看 <see cref="HttpResponse.StatusCode"/>。</exception>
        /// <exception cref="OperationCanceledException">等待期间被取消。</exception>
        public static UniTask<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            return GetAsync(url, DefaultTimeoutSeconds, cancellationToken);
        }

        /// <summary>GET。</summary>
        /// <param name="url">绝对 http 或 https 地址。</param>
        /// <param name="timeoutSeconds">超时秒数，必须大于 0。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>含状态码和正文的响应。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="url"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutSeconds"/> 小于等于 0。</exception>
        /// <exception cref="ArgumentException"><paramref name="url"/> 不是绝对的 http/https 地址。</exception>
        /// <exception cref="InvalidOperationException">连不上或传输失败。</exception>
        /// <exception cref="OperationCanceledException">等待期间被取消。</exception>
        public static UniTask<HttpResponse> GetAsync(
            string url,
            float timeoutSeconds,
            CancellationToken cancellationToken = default)
        {
            return SendAsync(UnityWebRequest.kHttpVerbGET, url, null, null, timeoutSeconds, cancellationToken);
        }

        /// <summary>POST，Content-Type 为 application/octet-stream。</summary>
        /// <param name="url">绝对 http 或 https 地址。</param>
        /// <param name="body">请求正文；为 null 时发送空正文。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>含状态码和正文的响应。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="url"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="url"/> 不是绝对的 http/https 地址。</exception>
        /// <exception cref="InvalidOperationException">连不上或传输失败。</exception>
        /// <exception cref="OperationCanceledException">等待期间被取消。</exception>
        public static UniTask<HttpResponse> PostAsync(
            string url,
            byte[] body,
            CancellationToken cancellationToken = default)
        {
            return PostAsync(url, body, "application/octet-stream", DefaultTimeoutSeconds, cancellationToken);
        }

        /// <summary>POST。</summary>
        /// <param name="url">绝对 http 或 https 地址。</param>
        /// <param name="body">请求正文；为 null 时发送空正文。</param>
        /// <param name="contentType">Content-Type，不可为 null。</param>
        /// <param name="timeoutSeconds">超时秒数，必须大于 0。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>含状态码和正文的响应。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="url"/> 或 <paramref name="contentType"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutSeconds"/> 小于等于 0。</exception>
        /// <exception cref="ArgumentException"><paramref name="url"/> 不是绝对的 http/https 地址。</exception>
        /// <exception cref="InvalidOperationException">连不上或传输失败。</exception>
        /// <exception cref="OperationCanceledException">等待期间被取消。</exception>
        public static UniTask<HttpResponse> PostAsync(
            string url,
            byte[] body,
            string contentType,
            float timeoutSeconds,
            CancellationToken cancellationToken = default)
        {
            if (contentType == null)
            {
                throw new ArgumentNullException(nameof(contentType));
            }

            return SendAsync(
                UnityWebRequest.kHttpVerbPOST,
                url,
                body ?? Array.Empty<byte>(),
                contentType,
                timeoutSeconds,
                cancellationToken);
        }

        static async UniTask<HttpResponse> SendAsync(
            string method,
            string url,
            byte[] body,
            string contentType,
            float timeoutSeconds,
            CancellationToken cancellationToken)
        {
            Validate(url, timeoutSeconds);
            var timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));
            using (var request = new UnityWebRequest(url, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = timeout;
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.SetRequestHeader("Content-Type", contentType);
                }

                try
                {
                    await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }

                if (request.result == UnityWebRequest.Result.ConnectionError
                    || request.result == UnityWebRequest.Result.DataProcessingError)
                {
                    GameLog.Warning(LogCategories.Network, $"HTTP {method} {url} failed: {request.error}");
                    throw new InvalidOperationException(request.error ?? "HTTP request failed.");
                }

                var downloaded = request.downloadHandler.data;
                var copy = downloaded == null || downloaded.Length == 0
                    ? Array.Empty<byte>()
                    : (byte[])downloaded.Clone();
                return new HttpResponse(request.responseCode, copy, request.GetResponseHeader("Content-Type"));
            }
        }

        static void Validate(string url, float timeoutSeconds)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (timeoutSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("URL must be an absolute http or https address.", nameof(url));
            }
        }
    }
}
