namespace Framework.Network
{
    /// <summary>网络频道连接状态。</summary>
    public enum NetworkChannelState
    {
        /// <summary>未连接。</summary>
        Disconnected = 0,

        /// <summary>正在连接。</summary>
        Connecting = 1,

        /// <summary>已连接。</summary>
        Connected = 2,
    }
}
