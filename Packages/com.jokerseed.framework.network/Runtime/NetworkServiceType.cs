namespace Framework.Network
{
    /// <summary>网络服务类型。</summary>
    public enum NetworkServiceType
    {
        /// <summary>TCP 异步收发（SocketAsyncEventArgs）。</summary>
        Tcp = 0,

        /// <summary>WebSocket 二进制帧，一帧一包；WebGL / 小游戏走浏览器 WebSocket。</summary>
        WebSocket = 1,
    }
}
