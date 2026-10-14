using System;

namespace Framework.Network
{
    /// <summary>
    /// WebSocket 底层传输。<see cref="WebSocketNetworkChannel"/> 每次连接新建一个实例，用完即弃。
    /// 默认实现：非 WebGL 用 <c>System.Net.WebSockets.ClientWebSocket</c>，WebGL 用浏览器 WebSocket；
    /// 需要平台 SDK（如微信小游戏 <c>WX.ConnectSocket</c>）时自行实现并传给频道构造函数。
    /// </summary>
    public interface IWebSocketTransport
    {
        /// <summary>开始连接；结果通过 <see cref="IWebSocketTransportListener"/> 回调。</summary>
        /// <param name="uri">ws:// 或 wss:// 地址。</param>
        void Connect(Uri uri);

        /// <summary>发送一条二进制消息；连接已关闭时丢弃。</summary>
        /// <param name="data">完整消息字节，调用后不再修改。</param>
        void Send(byte[] data);

        /// <summary>主动关闭。之后不再回调 <see cref="IWebSocketTransportListener.OnTransportClosed"/>。</summary>
        void Close();
    }

    /// <summary>
    /// 传输层回调，可能在任意线程触发。连接异常断开时先 <see cref="OnTransportError"/> 再 <see cref="OnTransportClosed"/>；
    /// 每个传输实例最多触发一次 <see cref="OnTransportClosed"/>。
    /// </summary>
    public interface IWebSocketTransportListener
    {
        /// <summary>连接已建立。</summary>
        /// <param name="transport">触发回调的传输。</param>
        void OnTransportOpen(IWebSocketTransport transport);

        /// <summary>收到一条完整的二进制消息；回调返回后 <paramref name="data"/> 可能被复用。</summary>
        /// <param name="transport">触发回调的传输。</param>
        /// <param name="data">消息缓冲。</param>
        /// <param name="length">有效字节数。</param>
        void OnTransportMessage(IWebSocketTransport transport, byte[] data, int length);

        /// <summary>一条消息已写出。</summary>
        /// <param name="transport">触发回调的传输。</param>
        void OnTransportSent(IWebSocketTransport transport);

        /// <summary>连接、收发出错。</summary>
        /// <param name="transport">触发回调的传输。</param>
        /// <param name="errorCode">错误分类。</param>
        /// <param name="message">错误描述。</param>
        void OnTransportError(IWebSocketTransport transport, NetworkErrorCode errorCode, string message);

        /// <summary>连接已断开（对端关闭或出错）。</summary>
        /// <param name="transport">触发回调的传输。</param>
        /// <param name="reason">断开原因。</param>
        void OnTransportClosed(IWebSocketTransport transport, string reason);
    }
}
