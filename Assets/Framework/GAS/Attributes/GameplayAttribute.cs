using System;
using System.Collections.Generic;

namespace Framework.GAS.Attributes
{
    /// <summary>单个战斗属性（生命值、攻击力等），维护基础值与经修改器计算后的当前值。</summary>
    public sealed class GameplayAttribute
    {
        /// <summary>属性名称（对应 <see cref="Core.BattleConstants"/> 中的常量）。</summary>
        public string Name { get; }

        /// <summary>基础值，未经任何修改器影响。</summary>
        public float BaseValue { get; private set; }

        /// <summary>当前值，基础值经加法/乘法修改器计算后的结果。</summary>
        public float CurrentValue { get; private set; }

        /// <summary>创建属性实例，基础值与当前值均初始化为 <paramref name="baseValue"/>。</summary>
        /// <param name="name">属性名称。</param>
        /// <param name="baseValue">初始基础值。</param>
        public GameplayAttribute(string name, float baseValue)
        {
            Name = name;
            BaseValue = baseValue;
            CurrentValue = baseValue;
        }

        /// <summary>设置基础值并立即以零偏移、单位乘数重新计算当前值。</summary>
        /// <param name="value">新基础值。</param>
        public void SetBaseValue(float value)
        {
            BaseValue = value;
            Recalculate(0f, 1f);
        }

        /// <summary>直接覆写当前值（用于受到伤害、治疗等即时改变场景）。</summary>
        /// <param name="value">新当前值。</param>
        public void SetCurrentValue(float value) => CurrentValue = value;

        /// <summary>用加法偏移和乘法系数重新计算当前值：<c>CurrentValue = (BaseValue + additive) * multiplier</c>。</summary>
        /// <param name="additive">加法修改量。</param>
        /// <param name="multiplier">乘法系数。</param>
        public void Recalculate(float additive, float multiplier)
        {
            CurrentValue = (BaseValue + additive) * multiplier;
        }
    }

    /// <summary>在一次属性重算中汇总所有活跃效果的修改量，然后批量应用到 <see cref="GameplayAttributeSet"/>。</summary>
    public sealed class AttributeModifierAggregator
    {
        readonly Dictionary<string, float> _additive = new Dictionary<string, float>();
        readonly Dictionary<string, float> _multiplier = new Dictionary<string, float>();
        readonly Dictionary<string, float> _override = new Dictionary<string, float>();

        /// <summary>清空本轮汇总数据，以便下一次重算前重新填入。</summary>
        public void Clear()
        {
            _additive.Clear();
            _multiplier.Clear();
            _override.Clear();
        }

        /// <summary>将单个修改器的贡献量累加到对应属性桶中。</summary>
        public void Add(string attributeName, float magnitude, Effects.EffectModifierOperation operation, int stackCount = 1)
        {
            var scaled = magnitude * stackCount;
            switch (operation)
            {
                case Effects.EffectModifierOperation.Add:
                    _additive[attributeName] = _additive.TryGetValue(attributeName, out var add) ? add + scaled : scaled;
                    break;
                case Effects.EffectModifierOperation.Multiply:
                    _multiplier[attributeName] = _multiplier.TryGetValue(attributeName, out var mul) ? mul * scaled : scaled;
                    break;
                case Effects.EffectModifierOperation.Override:
                    _override[attributeName] = scaled;
                    break;
            }
        }

        /// <summary>将汇总后的修改量应用到属性集合。</summary>
        public void ApplyTo(GameplayAttributeSet attributes)
        {
            var keys = new HashSet<string>();
            foreach (var key in _additive.Keys) keys.Add(key);
            foreach (var key in _multiplier.Keys) keys.Add(key);
            foreach (var key in _override.Keys) keys.Add(key);

            foreach (var key in keys)
            {
                var attribute = attributes.GetOrCreate(key);
                if (_override.TryGetValue(key, out var overrideValue))
                {
                    attribute.SetCurrentValue(overrideValue);
                    continue;
                }

                var add = _additive.TryGetValue(key, out var a) ? a : 0f;
                var mul = _multiplier.TryGetValue(key, out var m) ? m : 1f;
                attribute.Recalculate(add, mul);
            }
        }
    }

    /// <summary>属性集合，以属性名为键存储并按需创建 <see cref="GameplayAttribute"/> 实例。</summary>
    public sealed class GameplayAttributeSet
    {
        readonly Dictionary<string, GameplayAttribute> _attributes = new Dictionary<string, GameplayAttribute>();

        /// <summary>获取指定名称的属性；不存在时以 <paramref name="defaultValue"/> 为基础值创建并存入。</summary>
        /// <param name="name">属性名称。</param>
        /// <param name="defaultValue">首次创建时的基础值；默认为 0。</param>
        /// <returns>对应属性实例（不会返回 null）。</returns>
        public GameplayAttribute GetOrCreate(string name, float defaultValue = 0f)
        {
            if (!_attributes.TryGetValue(name, out var attribute))
            {
                attribute = new GameplayAttribute(name, defaultValue);
                _attributes[name] = attribute;
            }

            return attribute;
        }

        /// <summary>尝试获取指定属性。</summary>
        /// <param name="name">属性名称。</param>
        /// <param name="attribute">找到时输出属性实例；否则为 null。</param>
        /// <returns>属性存在返回 true。</returns>
        public bool TryGet(string name, out GameplayAttribute attribute) =>
            _attributes.TryGetValue(name, out attribute);

        /// <summary>获取指定属性的当前值；属性不存在时返回 0。</summary>
        /// <param name="name">属性名称。</param>
        /// <returns>当前值；不存在时返回 0。</returns>
        public float GetCurrentValue(string name) =>
            _attributes.TryGetValue(name, out var attribute) ? attribute.CurrentValue : 0f;

        /// <summary>获取指定属性的基础值；属性不存在时返回 0。</summary>
        /// <param name="name">属性名称。</param>
        /// <returns>基础值；不存在时返回 0。</returns>
        public float GetBaseValue(string name) =>
            _attributes.TryGetValue(name, out var attribute) ? attribute.BaseValue : 0f;

        /// <summary>将所有属性重置为"无修改器"状态（加量 0、系数 1），通常在重算前调用。</summary>
        public void RecalculateAll()
        {
            foreach (var attribute in _attributes.Values)
            {
                attribute.Recalculate(0f, 1f);
            }
        }

        /// <summary>枚举所有已创建属性。</summary>
        /// <returns>属性名 → 属性实例。</returns>
        public IEnumerable<KeyValuePair<string, GameplayAttribute>> GetAllAttributes() => _attributes;
    }
}
