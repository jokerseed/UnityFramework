using System;

namespace Framework.Network
{
    /// <summary>一次 HTTP 短请求的结果。4xx / 5xx 仍返回正文，不抛异常。</summary>
    public readonly struct HttpResponse
    {
        /// <summary>构造响应。</summary>
        /// <param name="statusCode">HTTP 状态码。</param>
        /// <param name="body">响应正文；为 null 时当作空数组。</param>
        /// <param name="contentType">Content-Type；可为 null。</param>
        public HttpResponse(long statusCode, byte[] body, string contentType)
        {
            StatusCode = statusCode;
            Body = body ?? Array.Empty<byte>();
            ContentType = contentType;
        }

        /// <summary>HTTP 状态码。</summary>
        public long StatusCode { get; }

        /// <summary>响应正文。没有正文时为空数组。</summary>
        public byte[] Body { get; }

        /// <summary>响应的 Content-Type；没有该头时为 null。</summary>
        public string ContentType { get; }

        /// <summary>状态码是否在 200–299。</summary>
        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
    }
}
