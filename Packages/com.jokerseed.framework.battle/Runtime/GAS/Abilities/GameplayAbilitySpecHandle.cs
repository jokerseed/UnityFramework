using System;

namespace Framework.GAS.Abilities
{
    /// <summary>运行时授予技能的稳定句柄。</summary>
    public readonly struct GameplayAbilitySpecHandle : IEquatable<GameplayAbilitySpecHandle>
    {
        /// <summary>无效句柄。</summary>
        public static readonly GameplayAbilitySpecHandle Invalid = default;

        /// <summary>句柄数值；大于 0 为有效。</summary>
        public int Value { get; }

        /// <summary>句柄是否有效。</summary>
        public bool IsValid => Value > 0;

        /// <summary>构造技能 Spec 句柄。</summary>
        /// <param name="value">句柄数值。</param>
        public GameplayAbilitySpecHandle(int value) => Value = value;

        /// <inheritdoc/>
        public bool Equals(GameplayAbilitySpecHandle other) => Value == other.Value;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is GameplayAbilitySpecHandle other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Value;

        /// <inheritdoc/>
        public override string ToString() => IsValid ? $"AbilitySpec({Value})" : "AbilitySpec(Invalid)";

        /// <summary>判断两个句柄是否相等。</summary>
        public static bool operator ==(GameplayAbilitySpecHandle left, GameplayAbilitySpecHandle right) => left.Equals(right);

        /// <summary>判断两个句柄是否不等。</summary>
        public static bool operator !=(GameplayAbilitySpecHandle left, GameplayAbilitySpecHandle right) => !left.Equals(right);
    }
}
