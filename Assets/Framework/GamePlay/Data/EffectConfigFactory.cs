using System.Collections.Generic;
using cfg;
using Framework.GAS.Effects;

namespace Framework.GamePlay.Data
{
    /// <summary>从 Luban 效果表创建 <see cref="GameplayEffectDef"/>。</summary>
    public static class EffectConfigFactory
    {
        /// <summary>创建效果定义。</summary>
        /// <param name="def">Luban 效果行。</param>
        /// <returns>效果定义。</returns>
        public static GameplayEffectDef CreateDef(EffectDef def)
        {
            var modifiers = new List<EffectModifier>();
            if (!string.IsNullOrEmpty(def.ModAttribute))
            {
                modifiers.Add(new EffectModifier(
                    def.ModAttribute,
                    (EffectModifierOperation)def.ModOperation,
                    def.ModMagnitude));
            }

            return new GameplayEffectDef(
                def.Id,
                MapDuration(def.DurationType),
                def.Duration,
                modifiers,
                stackingPolicy: MapStacking(def.Stacking));
        }

        /// <summary>创建运行时 Spec（兼容旧 API）。</summary>
        /// <param name="def">Luban 效果行。</param>
        /// <returns>运行时 Spec。</returns>
        public static GameplayEffectSpec Create(EffectDef def) => CreateDef(def).ToRuntimeSpec();

        static EffectDurationPolicy MapDuration(EffectDurationType type)
        {
            switch (type)
            {
                case EffectDurationType.Instant: return EffectDurationPolicy.Instant;
                case EffectDurationType.Duration: return EffectDurationPolicy.Duration;
                case EffectDurationType.Infinite: return EffectDurationPolicy.Infinite;
                default: throw new System.NotSupportedException($"Unknown duration type: {type}");
            }
        }

        static EffectStackingPolicy MapStacking(EffectStackingType type)
        {
            switch (type)
            {
                case EffectStackingType.None: return EffectStackingPolicy.None;
                case EffectStackingType.RefreshDuration: return EffectStackingPolicy.RefreshDuration;
                case EffectStackingType.StackCount: return EffectStackingPolicy.StackCount;
                case EffectStackingType.AggregateBySource: return EffectStackingPolicy.AggregateBySource;
                default: throw new System.NotSupportedException($"Unknown stacking type: {type}");
            }
        }
    }
}
