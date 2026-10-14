namespace Framework.Network
{
    /// <summary><see cref="NetworkManager"/> 驱动频道用的主线程入口。</summary>
    internal interface INetworkChannelDriver : INetworkChannel
    {
        /// <summary>主线程轮询：分发收包、心跳。</summary>
        /// <param name="elapseSeconds">真实流逝时间（秒）。</param>
        void Update(float elapseSeconds);

        /// <summary>关闭频道并释放资源。</summary>
        void Shutdown();
    }
}
