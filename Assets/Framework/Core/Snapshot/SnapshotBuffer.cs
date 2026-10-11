using System;
using System.Text;
using Framework.FixedMath;

namespace Framework.Core.Snapshot
{
    /// <summary>
    /// 可写入可复用缓冲的模拟状态。
    /// 预测回滚不在本接口的职责里；调用方自己决定何时 Save / Restore。
    /// </summary>
    public interface ISnapshotable
    {
        /// <summary>把当前状态追加到写入器。缓冲由调用方复用。</summary>
        /// <param name="writer">写入器。</param>
        void Save(SnapshotWriter writer);

        /// <summary>从读取器恢复。字节序须与对应的 <see cref="Save"/> 一致。</summary>
        /// <param name="reader">读取器。</param>
        void Restore(SnapshotReader reader);
    }

    /// <summary>可复用的字节缓冲。容量增长后会保留，避免每次快照重新分配。</summary>
    public sealed class SnapshotBuffer
    {
        byte[] _bytes = new byte[256];
        int _length;

        /// <summary>当前有效字节数。</summary>
        public int Length => _length;

        /// <summary>清空长度，保留已分配容量。</summary>
        public void Clear() => _length = 0;

        /// <summary>开始一次写入，并清空已有内容。</summary>
        /// <returns>写入器。</returns>
        public SnapshotWriter BeginWrite()
        {
            _length = 0;
            return new SnapshotWriter(this);
        }

        /// <summary>从当前内容开始读取。</summary>
        /// <returns>读取器。</returns>
        public SnapshotReader BeginRead() => new SnapshotReader(_bytes, _length);

        internal byte[] Bytes => _bytes;

        internal void Ensure(int capacity)
        {
            if (_bytes.Length >= capacity)
            {
                return;
            }

            var size = _bytes.Length;
            while (size < capacity)
            {
                size *= 2;
            }

            var grown = new byte[size];
            Array.Copy(_bytes, grown, _length);
            _bytes = grown;
        }

        internal int Advance(int count)
        {
            var offset = _length;
            Ensure(_length + count);
            _length += count;
            return offset;
        }
    }

    /// <summary>小端写入。结构体只持有缓冲引用，复制写入器不会拆成两份缓冲。</summary>
    public struct SnapshotWriter
    {
        readonly SnapshotBuffer _buffer;

        /// <summary>绑定缓冲。</summary>
        /// <param name="buffer">目标缓冲；不可为 null。</param>
        public SnapshotWriter(SnapshotBuffer buffer)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        /// <summary>写入 32 位整数。</summary>
        /// <param name="value">值。</param>
        public void WriteInt(int value) => WriteUInt(unchecked((uint)value));

        /// <summary>写入无符号 32 位整数。</summary>
        /// <param name="value">值。</param>
        public void WriteUInt(uint value)
        {
            var offset = _buffer.Advance(4);
            var bytes = _buffer.Bytes;
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        /// <summary>写入 64 位整数。</summary>
        /// <param name="value">值。</param>
        public void WriteLong(long value)
        {
            WriteUInt(unchecked((uint)value));
            WriteUInt(unchecked((uint)(value >> 32)));
        }

        /// <summary>写入布尔值，1 为 true。</summary>
        /// <param name="value">值。</param>
        public void WriteBool(bool value)
        {
            var offset = _buffer.Advance(1);
            _buffer.Bytes[offset] = value ? (byte)1 : (byte)0;
        }

        /// <summary>写入定点原始值。</summary>
        /// <param name="value">定点值。</param>
        public void WriteFp(FP value) => WriteLong(value.RawValue);

        /// <summary>写入 UTF-8 字符串。null 记为长度 -1。</summary>
        /// <param name="value">字符串；可为 null。</param>
        public void WriteString(string value)
        {
            if (value == null)
            {
                WriteInt(-1);
                return;
            }

            var encoded = Encoding.UTF8.GetBytes(value);
            WriteInt(encoded.Length);
            if (encoded.Length == 0)
            {
                return;
            }

            var offset = _buffer.Advance(encoded.Length);
            Array.Copy(encoded, 0, _buffer.Bytes, offset, encoded.Length);
        }
    }

    /// <summary>小端读取。</summary>
    public struct SnapshotReader
    {
        readonly byte[] _bytes;
        readonly int _length;
        int _offset;

        /// <summary>绑定一段已有字节。</summary>
        /// <param name="bytes">字节数组。</param>
        /// <param name="length">有效长度。</param>
        public SnapshotReader(byte[] bytes, int length)
        {
            _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
            _length = length;
            _offset = 0;
        }

        /// <summary>尚未读取的字节数。</summary>
        public int Remaining => _length - _offset;

        /// <summary>读取 32 位整数。</summary>
        /// <returns>值。</returns>
        public int ReadInt() => unchecked((int)ReadUInt());

        /// <summary>读取无符号 32 位整数。</summary>
        /// <returns>值。</returns>
        /// <exception cref="InvalidOperationException">缓冲被截断。</exception>
        public uint ReadUInt()
        {
            if (_offset + 4 > _length)
            {
                throw new InvalidOperationException("Snapshot truncated.");
            }

            var bytes = _bytes;
            var value = (uint)bytes[_offset]
                        | ((uint)bytes[_offset + 1] << 8)
                        | ((uint)bytes[_offset + 2] << 16)
                        | ((uint)bytes[_offset + 3] << 24);
            _offset += 4;
            return value;
        }

        /// <summary>读取 64 位整数。</summary>
        /// <returns>值。</returns>
        public long ReadLong()
        {
            var low = ReadUInt();
            var high = ReadUInt();
            return (long)low | ((long)high << 32);
        }

        /// <summary>读取布尔值。</summary>
        /// <returns>值。</returns>
        /// <exception cref="InvalidOperationException">缓冲被截断。</exception>
        public bool ReadBool()
        {
            if (_offset >= _length)
            {
                throw new InvalidOperationException("Snapshot truncated.");
            }

            var value = _bytes[_offset] != 0;
            _offset++;
            return value;
        }

        /// <summary>读取定点值。</summary>
        /// <returns>定点值。</returns>
        public FP ReadFp() => FP.FromRaw(ReadLong());

        /// <summary>读取 UTF-8 字符串。</summary>
        /// <returns>字符串；写入时为 null 则返回 null。</returns>
        /// <exception cref="InvalidOperationException">缓冲被截断。</exception>
        public string ReadString()
        {
            var count = ReadInt();
            if (count < 0)
            {
                return null;
            }

            if (count == 0)
            {
                return string.Empty;
            }

            if (_offset + count > _length)
            {
                throw new InvalidOperationException("Snapshot truncated.");
            }

            var value = Encoding.UTF8.GetString(_bytes, _offset, count);
            _offset += count;
            return value;
        }
    }
}
