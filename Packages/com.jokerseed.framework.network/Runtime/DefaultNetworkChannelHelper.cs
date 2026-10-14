using System;
using System.IO;

namespace Framework.Network
{
    /// <summary>
    /// 默认二进制协议：小端 <c>int32 包体长度 + ushort 消息号 + payload</c>。
    /// 消息号 0 为心跳。
    /// </summary>
    public sealed class DefaultNetworkChannelHelper : INetworkChannelHelper
    {
        /// <summary>心跳消息号。</summary>
        public const ushort HeartBeatMessageId = 0;

        /// <inheritdoc />
        public int PacketHeaderLength => 4;

        /// <inheritdoc />
        public bool SerializePacket(NetworkPacket packet, Stream destination)
        {
            if (packet == null || destination == null)
            {
                return false;
            }

            var payloadLength = Math.Max(0, packet.Length);
            var bodyLength = 2 + payloadLength;
            WriteInt32LittleEndian(destination, bodyLength);
            WriteUInt16LittleEndian(destination, packet.MessageId);
            if (payloadLength > 0 && packet.Payload != null)
            {
                destination.Write(packet.Payload, packet.Offset, payloadLength);
            }

            return true;
        }

        /// <inheritdoc />
        public int ParsePacketLength(byte[] header, int offset)
        {
            if (header == null || offset < 0 || offset + 4 > header.Length)
            {
                return -1;
            }

            return ReadInt32LittleEndian(header, offset);
        }

        /// <inheritdoc />
        public NetworkPacket DeserializePacket(byte[] buffer, int offset, int length)
        {
            if (buffer == null || length < 2 || offset < 0 || offset + length > buffer.Length)
            {
                return null;
            }

            var messageId = ReadUInt16LittleEndian(buffer, offset);
            var payloadLength = length - 2;
            byte[] payload = null;
            if (payloadLength > 0)
            {
                payload = new byte[payloadLength];
                Buffer.BlockCopy(buffer, offset + 2, payload, 0, payloadLength);
            }

            return NetworkPacket.Create(messageId, payload, 0, payloadLength);
        }

        /// <inheritdoc />
        public bool TrySendHeartBeat(INetworkChannel channel)
        {
            if (channel == null || !channel.Connected)
            {
                return false;
            }

            channel.Send(HeartBeatMessageId, null);
            return true;
        }

        static void WriteInt32LittleEndian(Stream stream, int value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 24));
        }

        static void WriteUInt16LittleEndian(Stream stream, ushort value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
        }

        static int ReadInt32LittleEndian(byte[] buffer, int offset)
        {
            return buffer[offset]
                   | (buffer[offset + 1] << 8)
                   | (buffer[offset + 2] << 16)
                   | (buffer[offset + 3] << 24);
        }

        static ushort ReadUInt16LittleEndian(byte[] buffer, int offset)
        {
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }
    }
}
