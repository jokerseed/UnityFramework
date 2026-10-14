using System;

namespace Framework.GAS.Effects
{
    /// <summary>活跃 GameplayEffect 的稳定句柄。</summary>
    public readonly struct GameplayEffectHandle : IEquatable<GameplayEffectHandle>
    {
        /// <summary>无效句柄。</summary>
        public static readonly GameplayEffectHandle Invalid = default;

        /// <summary>句柄数值。</summary>
        public int Value { get; }

        /// <summary>是否有效。</summary>
        public bool IsValid => Value > 0;

        /// <summary>构造句柄。</summary>
        /// <param name="value">数值。</param>
        public GameplayEffectHandle(int value) => Value = value;

        /// <inheritdoc/>
        public bool Equals(GameplayEffectHandle other) => Value == other.Value;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is GameplayEffectHandle other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Value;

        /// <inheritdoc/>
        public override string ToString() => IsValid ? $"GEHandle({Value})" : "GEHandle(Invalid)";

        /// <summary>相等比较。</summary>
        public static bool operator ==(GameplayEffectHandle left, GameplayEffectHandle right) => left.Equals(right);

        /// <summary>不等比较。</summary>
        public static bool operator !=(GameplayEffectHandle left, GameplayEffectHandle right) => !left.Equals(right);
    }
}
