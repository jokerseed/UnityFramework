using System;
using System.Collections.Generic;
using Framework.GAS.Abilities;

namespace Framework.GAS.Effects
{
    /// <summary>修改器幅度计算方式。</summary>
    public enum ModifierMagnitudeType
    {
        /// <summary>固定常量。</summary>
        Constant,

        /// <summary>读取来源或目标 ASC 属性。</summary>
        AttributeBased,

        /// <summary>从 SetByCaller 字典读取。</summary>
        SetByCaller
    }

    /// <summary>描述 GameplayEffect 修改器幅度的计算方式。</summary>
    public readonly struct ModifierMagnitude
    {
        /// <summary>幅度类型。</summary>
        public ModifierMagnitudeType Type { get; }

        /// <summary>常量值或 AttributeBased 系数。</summary>
        public float Value { get; }

        /// <summary>AttributeBased 时读取的属性名。</summary>
        public string AttributeName { get; }

        /// <summary>SetByCaller 键名。</summary>
        public string SetByCallerKey { get; }

        ModifierMagnitude(ModifierMagnitudeType type, float value, string attributeName, string setByCallerKey)
        {
            Type = type;
            Value = value;
            AttributeName = attributeName;
            SetByCallerKey = setByCallerKey;
        }

        /// <summary>固定幅度。</summary>
        /// <param name="value">常量值。</param>
        public static ModifierMagnitude Constant(float value) =>
            new ModifierMagnitude(ModifierMagnitudeType.Constant, value, null, null);

        /// <summary>基于属性幅度：<c>source.Attribute * coefficient</c>。</summary>
        /// <param name="attributeName">属性名。</param>
        /// <param name="coefficient">系数。</param>
        public static ModifierMagnitude FromAttribute(string attributeName, float coefficient = 1f) =>
            new ModifierMagnitude(ModifierMagnitudeType.AttributeBased, coefficient, attributeName, null);

        /// <summary>从 SetByCaller 读取；不存在时返回 0。</summary>
        /// <param name="key">SetByCaller 键。</param>
        public static ModifierMagnitude FromSetByCaller(string key) =>
            new ModifierMagnitude(ModifierMagnitudeType.SetByCaller, 0f, null, key);

        /// <summary>计算最终幅度。</summary>
        /// <param name="source">来源 ASC。</param>
        /// <param name="target">目标 ASC。</param>
        /// <param name="setByCaller">SetByCaller 字典。</param>
        /// <returns>计算后的幅度。</returns>
        public float Evaluate(
            AbilitySystemComponent source,
            AbilitySystemComponent target,
            IReadOnlyDictionary<string, float> setByCaller)
        {
            switch (Type)
            {
                case ModifierMagnitudeType.Constant:
                    return Value;
                case ModifierMagnitudeType.AttributeBased:
                    var asc = source ?? target;
                    if (asc == null || string.IsNullOrEmpty(AttributeName))
                    {
                        return 0f;
                    }

                    return asc.Attributes.GetCurrentValue(AttributeName) * Value;
                case ModifierMagnitudeType.SetByCaller:
                    if (setByCaller != null && setByCaller.TryGetValue(SetByCallerKey, out var v))
                    {
                        return v;
                    }

                    return 0f;
                default:
                    return 0f;
            }
        }
    }
}
