using System;
using Framework.FixedMath;

namespace Framework.Core.Snapshot
{
    /// <summary>
    /// 把 <see cref="TSRandom"/> 的 MT 状态写入快照缓冲。
    /// 不消耗随机序列。战斗路径应使用带种子的 <see cref="TSRandom.New(int)"/>。
    /// </summary>
    public sealed class DeterministicRandomSnapshot : ISnapshotable
    {
        const int Version = 1;

        readonly TSRandom _random;
        readonly uint[] _state = new uint[TSRandom.StateLength];

        /// <summary>绑定要保存的随机源。</summary>
        /// <param name="random">随机源；不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="random"/> 为 null。</exception>
        public DeterministicRandomSnapshot(TSRandom random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <inheritdoc />
        public void Save(SnapshotWriter writer)
        {
            _random.CopyState(_state, out var index);
            writer.WriteInt(Version);
            writer.WriteInt(index);
            writer.WriteInt(_state.Length);
            for (var i = 0; i < _state.Length; i++)
            {
                writer.WriteUInt(_state[i]);
            }
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">版本或状态长度不匹配。</exception>
        public void Restore(SnapshotReader reader)
        {
            var version = reader.ReadInt();
            if (version != Version)
            {
                throw new InvalidOperationException("Unsupported random snapshot version " + version);
            }

            var index = reader.ReadInt();
            var length = reader.ReadInt();
            if (length != TSRandom.StateLength)
            {
                throw new InvalidOperationException("Unexpected random state length " + length);
            }

            for (var i = 0; i < length; i++)
            {
                _state[i] = reader.ReadUInt();
            }

            _random.LoadState(_state, index);
        }
    }
}
