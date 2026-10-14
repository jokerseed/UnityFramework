namespace Framework.Network
{
    /// <summary>按消息号处理收包。</summary>
    public interface INetworkPacketHandler
    {
        /// <summary>处理的消息号。</summary>
        ushort MessageId { get; }

        /// <summary>处理一条已解析的消息包。</summary>
        /// <param name="channel">来源频道。</param>
        /// <param name="packet">消息包；调用结束后由频道归还内存池，勿缓存引用。</param>
        void Handle(INetworkChannel channel, NetworkPacket packet);
    }
}
