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
        public static GameplayEffectDef CreateDef(CfgEffectDef def)
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
        public static GameplayEffectSpec Create(CfgEffectDef def) => CreateDef(def).ToRuntimeSpec();

        static EffectDurationPolicy MapDuration(CfgEffectDurationType type)
        {
            switch (type)
            {
                case CfgEffectDurationType.Instant: return EffectDurationPolicy.Instant;
                case CfgEffectDurationType.Duration: return EffectDurationPolicy.Duration;
                case CfgEffectDurationType.Infinite: return EffectDurationPolicy.Infinite;
                default: throw new System.NotSupportedException($"Unknown duration type: {type}");
            }
        }

        static EffectStackingPolicy MapStacking(CfgEffectStackingType type)
        {
            switch (type)
            {
                case CfgEffectStackingType.None: return EffectStackingPolicy.None;
                case CfgEffectStackingType.RefreshDuration: return EffectStackingPolicy.RefreshDuration;
                case CfgEffectStackingType.StackCount: return EffectStackingPolicy.StackCount;
                case CfgEffectStackingType.AggregateBySource: return EffectStackingPolicy.AggregateBySource;
                default: throw new System.NotSupportedException($"Unknown stacking type: {type}");
            }
        }
    }
}
