using System.IO;

namespace Framework.Network
{
    /// <summary>频道协议辅助器：负责包头长度、序列化、反序列化与心跳。</summary>
    public interface INetworkChannelHelper
    {
        /// <summary>包头字节数（用于先收包头再收包体）。</summary>
        int PacketHeaderLength { get; }

        /// <summary>将消息包写入发送流（含包头）。</summary>
        /// <param name="packet">待发送消息包。</param>
        /// <param name="destination">目标流。</param>
        /// <returns>序列化成功返回 true。</returns>
        bool SerializePacket(NetworkPacket packet, Stream destination);

        /// <summary>从包头解析包体长度（不含包头本身）。</summary>
        /// <param name="header">包头缓冲。</param>
        /// <param name="offset">起始偏移。</param>
        /// <returns>包体长度；非法时返回负数。</returns>
        int ParsePacketLength(byte[] header, int offset);

        /// <summary>从包体字节反序列化为消息包。</summary>
        /// <param name="buffer">含包体的缓冲。</param>
        /// <param name="offset">包体起始偏移。</param>
        /// <param name="length">包体长度。</param>
        /// <returns>消息包；失败返回 null。</returns>
        NetworkPacket DeserializePacket(byte[] buffer, int offset, int length);

        /// <summary>尝试发送心跳包。</summary>
        /// <param name="channel">目标频道。</param>
        /// <returns>已发出心跳返回 true。</returns>
        bool TrySendHeartBeat(INetworkChannel channel);
    }
}
