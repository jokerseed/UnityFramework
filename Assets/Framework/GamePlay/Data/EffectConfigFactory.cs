using System;
using System.Collections.Generic;
using cfg;
using Framework.Config;
using Framework.Core;
using Framework.GAS.Effects;
using Framework.GAS.Tags;

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

            if (def.ShieldValue > 0f)
            {
                modifiers.Add(new EffectModifier(
                    BattleConstants.Shield,
                    EffectModifierOperation.Add,
                    def.ShieldValue));
            }

            var executions = new List<GameplayEffectExecution>();
            switch (def.ExecutionType)
            {
                case CfgEffectExecutionType.Damage:
                    executions.Add(new DamageExecution());
                    break;
                case CfgEffectExecutionType.Heal:
                    executions.Add(new HealExecution(ModifierMagnitude.Constant(def.ModMagnitude)));
                    break;
                case CfgEffectExecutionType.ApplyEffect:
                    if (!string.IsNullOrEmpty(def.ExecutionEffectId) &&
                        def.ExecutionEffectId != def.Id &&
                        ConfigManager.HasInstance)
                    {
                        var tables = ConfigManager.Instance.GetTables();
                        if (tables != null &&
                            tables.CfgTbEffect.DataMap.TryGetValue(def.ExecutionEffectId, out var nested))
                        {
                            executions.Add(new ApplyGameplayEffectExecution(CreateDef(nested)));
                        }
                    }
                    break;
            }

            var cost = string.IsNullOrEmpty(def.CostAttribute) || def.CostAmount <= 0f
                ? null
                : new Dictionary<string, float> { [def.CostAttribute] = def.CostAmount };

            return new GameplayEffectDef(
                def.Id,
                MapDuration(def.DurationType),
                def.Duration,
                modifiers,
                ParseTags(def.GrantedTags),
                MapStacking(def.Stacking),
                ParseTags(def.RequiredTags),
                ParseTags(def.BlockedTags),
                ParseTags(def.ImmunityTags),
                executions: executions,
                period: def.Period,
                cueTagsOnApply: ParseCue(def.CueApply),
                cueTagsOnRemove: ParseCue(def.CueRemove),
                costAttributes: cost,
                maxStacks: def.MaxStacks);
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
                default: throw new NotSupportedException($"Unknown duration type: {type}");
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
                default: throw new NotSupportedException($"Unknown stacking type: {type}");
            }
        }

        static IReadOnlyList<GameplayTag> ParseTags(string csv)
        {
            if (string.IsNullOrEmpty(csv))
            {
                return Array.Empty<GameplayTag>();
            }

            var parts = csv.Split(',');
            var list = new List<GameplayTag>(parts.Length);
            for (var i = 0; i < parts.Length; i++)
            {
                var name = parts[i].Trim();
                if (name.Length > 0)
                {
                    list.Add(new GameplayTag(name));
                }
            }

            return list;
        }

        static IReadOnlyList<string> ParseCue(string cue)
        {
            if (string.IsNullOrEmpty(cue))
            {
                return Array.Empty<string>();
            }

            return new[] { cue };
        }
    }
}
