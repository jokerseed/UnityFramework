using System.Net;

namespace Framework.Network
{
    /// <summary>KCP 底层的 UDP 数据报。实现只负责收发数据报，不解析 KCP。</summary>
    public interface IKcpDatagramTransport
    {
        /// <summary>开始把数据报发往 <paramref name="remote"/>，并接收对端数据报。</summary>
        /// <param name="remote">远端地址，不可为 null。</param>
        void Start(IPEndPoint remote);

        /// <summary>发送一个 UDP 数据报。调用返回前必须把字节拷走，缓冲随后会被复用。</summary>
        /// <param name="datagram">数据报字节，不可为 null。</param>
        /// <param name="offset">起始偏移。</param>
        /// <param name="count">字节数。</param>
        void Send(byte[] datagram, int offset, int count);

        /// <summary>关闭套接字。可重复调用。</summary>
        void Close();
    }

    /// <summary>
    /// <see cref="IKcpDatagramTransport"/> 的回调。可能从 IO 线程进入，实现只能入队。
    /// </summary>
    public interface IKcpDatagramListener
    {
        /// <summary>收到一个数据报。</summary>
        /// <param name="data">数据报字节。</param>
        /// <param name="offset">起始偏移。</param>
        /// <param name="count">字节数。</param>
        void OnDatagram(byte[] data, int offset, int count);

        /// <summary>套接字失败。不要在此回调里关闭传输，频道会在主线程处理。</summary>
        /// <param name="message">错误描述。</param>
        void OnTransportError(string message);
    }
}
