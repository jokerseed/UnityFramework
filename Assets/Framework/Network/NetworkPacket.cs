using Framework.MemoryPool;

namespace Framework.Network
{
    /// <summary>一条可池化的网络消息包。</summary>
    public sealed class NetworkPacket : IMemory
    {
        /// <summary>消息号；0 保留给心跳。</summary>
        public ushort MessageId { get; set; }

        /// <summary>消息体；可为 null 表示无载荷。</summary>
        public byte[] Payload { get; set; }

        /// <summary>载荷有效起始偏移。</summary>
        public int Offset { get; set; }

        /// <summary>载荷有效长度。</summary>
        public int Length { get; set; }

        /// <summary>从内存池取出并填充一条消息包。</summary>
        /// <param name="messageId">消息号。</param>
        /// <param name="payload">消息体；可为 null。</param>
        /// <param name="offset">有效起始偏移。</param>
        /// <param name="length">有效长度；小于 0 时使用剩余全部字节。</param>
        /// <returns>已填充的消息包。</returns>
        public static NetworkPacket Create(ushort messageId, byte[] payload, int offset = 0, int length = -1)
        {
            var packet = global::Framework.MemoryPool.MemoryPool.Acquire<NetworkPacket>();
            packet.MessageId = messageId;
            packet.Payload = payload;
            packet.Offset = offset;
            packet.Length = length < 0 ? (payload != null ? payload.Length - offset : 0) : length;
            return packet;
        }

        /// <inheritdoc />
        public void Clear()
        {
            MessageId = 0;
            Payload = null;
            Offset = 0;
            Length = 0;
        }
    }
}
