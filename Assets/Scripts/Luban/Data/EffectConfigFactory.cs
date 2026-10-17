using System;
using System.Collections.Generic;
using cfg;
using Framework.Config;
using Framework.Battle;
using Framework.FixedMath;
using Framework.GAS.Effects;
using Framework.GAS.Tags;

namespace Framework.GamePlay.Data
{
    /// <summary>从 Luban 效果表创建 <see cref="GameplayEffectDef"/>。表里的 float 一律经 <see cref="FixedConfig.FromConfig(float)"/> 量化。</summary>
    public static class EffectConfigFactory
    {
        /// <summary>创建效果定义。嵌套效果从同一张表解析，不读全局 <c>ConfigManager</c>。</summary>
        /// <param name="def">Luban 效果行。</param>
        /// <param name="tables">已加载的表；为 null 时不展开 <c>ApplyEffect</c>。</param>
        /// <returns>效果定义。</returns>
        /// <exception cref="InvalidOperationException">嵌套效果互相指向，形成环。</exception>
        public static GameplayEffectDef CreateDef(CfgEffectDef def, CfgTables tables)
        {
            return CreateDef(def, tables, new HashSet<string>());
        }

        static GameplayEffectDef CreateDef(CfgEffectDef def, CfgTables tables, HashSet<string> stack)
        {
            if (def == null)
            {
                throw new ArgumentNullException(nameof(def));
            }

            if (!stack.Add(def.Id))
            {
                throw new InvalidOperationException("Effect nesting cycle at '" + def.Id + "'.");
            }

            try
            {
                return CreateDefUnlocked(def, tables, stack);
            }
            finally
            {
                stack.Remove(def.Id);
            }
        }

        static GameplayEffectDef CreateDefUnlocked(CfgEffectDef def, CfgTables tables, HashSet<string> stack)
        {
            var modifiers = new List<EffectModifier>();
            if (!string.IsNullOrEmpty(def.ModAttribute))
            {
                modifiers.Add(new EffectModifier(
                    def.ModAttribute,
                    (EffectModifierOperation)def.ModOperation,
                    ModifierMagnitude.Constant(FixedConfig.FromConfig(def.ModMagnitude))));
            }

            if (def.ShieldValue > 0f)
            {
                modifiers.Add(new EffectModifier(
                    BattleConstants.Shield,
                    EffectModifierOperation.Add,
                    ModifierMagnitude.Constant(FixedConfig.FromConfig(def.ShieldValue))));
            }

            var executions = new List<GameplayEffectExecution>();
            switch (def.ExecutionType)
            {
                case CfgEffectExecutionType.Damage:
                    executions.Add(new DamageExecution());
                    break;
                case CfgEffectExecutionType.Heal:
                    executions.Add(new HealExecution(ModifierMagnitude.Constant(FixedConfig.FromConfig(def.ModMagnitude))));
                    break;
                case CfgEffectExecutionType.ApplyEffect:
                    if (!string.IsNullOrEmpty(def.ExecutionEffectId) &&
                        def.ExecutionEffectId != def.Id &&
                        tables != null &&
                        tables.CfgTbEffect.DataMap.TryGetValue(def.ExecutionEffectId, out var nested))
                    {
                        executions.Add(new ApplyGameplayEffectExecution(CreateDef(nested, tables, stack)));
                    }
                    break;
            }

            var cost = string.IsNullOrEmpty(def.CostAttribute) || def.CostAmount <= 0f
                ? null
                : new Dictionary<string, FP> { [def.CostAttribute] = FixedConfig.FromConfig(def.CostAmount) };

            return new GameplayEffectDef(
                def.Id,
                MapDuration(def.DurationType),
                FixedConfig.FromConfig(def.Duration),
                modifiers,
                ParseTags(def.GrantedTags),
                MapStacking(def.Stacking),
                ParseTags(def.RequiredTags),
                ParseTags(def.BlockedTags),
                ParseTags(def.ImmunityTags),
                executions: executions,
                period: FixedConfig.FromConfig(def.Period),
                cueTagsOnApply: ParseCue(def.CueApply),
                cueTagsOnRemove: ParseCue(def.CueRemove),
                costAttributes: cost,
                maxStacks: def.MaxStacks);
        }

        /// <summary>
        /// 创建效果定义。仍会在 <c>ConfigManager</c> 已加载时展开嵌套效果。
        /// 模拟路径请改用 <see cref="CreateDef(CfgEffectDef, CfgTables)"/>。
        /// </summary>
        /// <param name="def">Luban 效果行。</param>
        /// <returns>效果定义。</returns>
        [Obsolete("请改用 CreateDef(CfgEffectDef, CfgTables)，避免在模拟里读 ConfigManager。")]
        public static GameplayEffectDef CreateDef(CfgEffectDef def)
        {
            CfgTables tables = null;
            if (ConfigManager.HasInstance)
            {
                tables = ConfigManager.Instance.GetTables<CfgTables>();
            }

            return CreateDef(def, tables);
        }

        /// <summary>创建运行时 Spec（兼容旧 API）。不展开嵌套效果。</summary>
        /// <param name="def">Luban 效果行。</param>
        /// <returns>运行时 Spec。</returns>
        public static GameplayEffectSpec Create(CfgEffectDef def) => CreateDef(def, null).ToRuntimeSpec();

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
