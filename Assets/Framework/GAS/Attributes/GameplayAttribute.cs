using System;
using System.Collections.Generic;

namespace Framework.GAS.Attributes
{
    public sealed class GameplayAttribute
    {
        public string Name { get; }
        public float BaseValue { get; private set; }
        public float CurrentValue { get; private set; }

        public GameplayAttribute(string name, float baseValue)
        {
            Name = name;
            BaseValue = baseValue;
            CurrentValue = baseValue;
        }

        public void SetBaseValue(float value)
        {
            BaseValue = value;
            Recalculate(0f, 1f);
        }

        public void SetCurrentValue(float value) => CurrentValue = value;

        public void Recalculate(float additive, float multiplier)
        {
            CurrentValue = (BaseValue + additive) * multiplier;
        }
    }

    public sealed class AttributeModifierAggregator
    {
        readonly Dictionary<string, float> _additive = new Dictionary<string, float>();
        readonly Dictionary<string, float> _multiplier = new Dictionary<string, float>();

        public void Clear()
        {
            _additive.Clear();
            _multiplier.Clear();
        }

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
            }
        }

        public void ApplyTo(GameplayAttributeSet attributes)
        {
            foreach (var pair in _additive)
            {
                var attribute = attributes.GetOrCreate(pair.Key);
                var mul = _multiplier.TryGetValue(pair.Key, out var value) ? value : 1f;
                attribute.Recalculate(pair.Value, mul);
            }

            foreach (var pair in _multiplier)
            {
                if (_additive.ContainsKey(pair.Key))
                {
                    continue;
                }

                var attribute = attributes.GetOrCreate(pair.Key);
                attribute.Recalculate(0f, pair.Value);
            }
        }
    }

    public sealed class GameplayAttributeSet
    {
        readonly Dictionary<string, GameplayAttribute> _attributes = new Dictionary<string, GameplayAttribute>();

        public GameplayAttribute GetOrCreate(string name, float defaultValue = 0f)
        {
            if (!_attributes.TryGetValue(name, out var attribute))
            {
                attribute = new GameplayAttribute(name, defaultValue);
                _attributes[name] = attribute;
            }

            return attribute;
        }

        public bool TryGet(string name, out GameplayAttribute attribute) =>
            _attributes.TryGetValue(name, out attribute);

        public float GetCurrentValue(string name) =>
            _attributes.TryGetValue(name, out var attribute) ? attribute.CurrentValue : 0f;

        public float GetBaseValue(string name) =>
            _attributes.TryGetValue(name, out var attribute) ? attribute.BaseValue : 0f;

        public void RecalculateAll()
        {
            foreach (var attribute in _attributes.Values)
            {
                attribute.Recalculate(0f, 1f);
            }
        }
    }
}
