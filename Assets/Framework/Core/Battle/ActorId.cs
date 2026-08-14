namespace Framework.Core
{
    /// <summary>战斗 Actor 的唯一标识符（值类型，0 为无效值）。</summary>
    public readonly struct ActorId : System.IEquatable<ActorId>
    {
        /// <summary>表示无效 Actor 的标识符（Value == 0）。</summary>
        public static readonly ActorId Invalid = new ActorId(0);

        /// <summary>标识符的原始数值；0 表示无效。</summary>
        public uint Value { get; }

        /// <summary>获取该标识符是否有效（Value != 0）。</summary>
        public bool IsValid => Value != 0;

        /// <summary>使用指定数值构造 <see cref="ActorId"/>。</summary>
        /// <param name="value">原始数值；传 0 则等同于 <see cref="Invalid"/>。</param>
        public ActorId(uint value) => Value = value;

        /// <summary>判断当前实例与另一个 <see cref="ActorId"/> 是否相等。</summary>
        /// <param name="other">用于比较的另一个标识符。</param>
        /// <returns>两者 Value 相同时返回 <see langword="true"/>。</returns>
        public bool Equals(ActorId other) => Value == other.Value;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is ActorId other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Value.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => $"Actor({Value})";

        /// <summary>判断两个 <see cref="ActorId"/> 是否相等。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>相等时返回 <see langword="true"/>。</returns>
        public static bool operator ==(ActorId left, ActorId right) => left.Equals(right);

        /// <summary>判断两个 <see cref="ActorId"/> 是否不相等。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>不相等时返回 <see langword="true"/>。</returns>
        public static bool operator !=(ActorId left, ActorId right) => !left.Equals(right);
    }
}
