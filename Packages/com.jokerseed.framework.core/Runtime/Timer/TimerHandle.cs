using System;

namespace Framework.Timer
{
    /// <summary>
    /// 定时器句柄。定时器结束或取消后句柄失效，再次操作不会影响复用同一槽位的新定时器。
    /// </summary>
    public readonly struct TimerHandle : IEquatable<TimerHandle>
    {
        internal readonly int Slot;
        internal readonly int Version;

        internal TimerHandle(int slot, int version)
        {
            Slot = slot;
            Version = version;
        }

        /// <summary>无效句柄。</summary>
        public static TimerHandle None => default;

        /// <summary>句柄是否曾被分配（不代表定时器仍在运行，运行状态查 <see cref="TimerManager.IsActive"/>）。</summary>
        public bool IsAssigned => Version != 0;

        /// <inheritdoc />
        public bool Equals(TimerHandle other) => Slot == other.Slot && Version == other.Version;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is TimerHandle other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (Slot * 397) ^ Version;
    }
}
