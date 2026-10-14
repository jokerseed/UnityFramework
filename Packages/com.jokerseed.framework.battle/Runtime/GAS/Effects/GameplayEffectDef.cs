using System;
using System.Collections.Generic;
using Framework.FixedMath;
using Framework.GAS.Abilities;
using Framework.GAS.Tags;

namespace Framework.GAS.Effects
{
    /// <summary>GameplayEffect 不可变定义（对应 UE UGameplayEffect 数据部分）。</summary>
    public sealed class GameplayEffectDef
    {
        /// <summary>效果 ID。</summary>
        public string EffectId { get; }

        /// <summary>持续时间策略。</summary>
        public EffectDurationPolicy DurationPolicy { get; }

        /// <summary>叠加策略。</summary>
        public EffectStackingPolicy StackingPolicy { get; }

        /// <summary>持续时长（秒）。</summary>
        public FP Duration { get; }

        /// <summary>周期（秒）；&gt; 0 时按 Periodic 触发 Execution。</summary>
        public FP Period { get; }

        /// <summary>属性修改器。</summary>
        public IReadOnlyList<EffectModifier> Modifiers { get; }

        /// <summary>授予 Tag。</summary>
        public IReadOnlyList<GameplayTag> GrantedTags { get; }

        /// <summary>应用前提 Tag。</summary>
        public IReadOnlyList<GameplayTag> ApplicationRequiredTags { get; }

        /// <summary>应用阻断 Tag。</summary>
        public IReadOnlyList<GameplayTag> ApplicationBlockedTags { get; }

        /// <summary>免疫 Tag：持有者拒绝被施加。</summary>
        public IReadOnlyList<GameplayTag> ImmunityTags { get; }

        /// <summary>授予技能定义。</summary>
        public IReadOnlyList<GameplayAbilityDef> GrantedAbilityDefs { get; }

        /// <summary>Execution 列表。</summary>
        public IReadOnlyList<GameplayEffectExecution> Executions { get; }

        /// <summary>激活 Cue Tag。</summary>
        public IReadOnlyList<string> CueTagsOnApply { get; }

        /// <summary>移除 Cue Tag。</summary>
        public IReadOnlyList<string> CueTagsOnRemove { get; }

        /// <summary>作为技能 Cost 的属性消耗（属性名 → 量）。</summary>
        public IReadOnlyDictionary<string, FP> CostAttributes { get; }

        /// <summary>叠层上限；≤0 表示不限制。</summary>
        public int MaxStacks { get; }

        /// <summary>构造效果定义。</summary>
        public GameplayEffectDef(
            string effectId,
            EffectDurationPolicy durationPolicy,
            FP duration,
            IReadOnlyList<EffectModifier> modifiers = null,
            IReadOnlyList<GameplayTag> grantedTags = null,
            EffectStackingPolicy stackingPolicy = EffectStackingPolicy.None,
            IReadOnlyList<GameplayTag> applicationRequiredTags = null,
            IReadOnlyList<GameplayTag> applicationBlockedTags = null,
            IReadOnlyList<GameplayTag> immunityTags = null,
            IReadOnlyList<GameplayAbilityDef> grantedAbilityDefs = null,
            IReadOnlyList<GameplayEffectExecution> executions = null,
            FP period = default,
            IReadOnlyList<string> cueTagsOnApply = null,
            IReadOnlyList<string> cueTagsOnRemove = null,
            IReadOnlyDictionary<string, FP> costAttributes = null,
            int maxStacks = 0)
        {
            EffectId = effectId;
            DurationPolicy = durationPolicy;
            Duration = duration;
            StackingPolicy = stackingPolicy;
            Period = period;
            Modifiers = modifiers ?? Array.Empty<EffectModifier>();
            GrantedTags = grantedTags ?? Array.Empty<GameplayTag>();
            ApplicationRequiredTags = applicationRequiredTags ?? Array.Empty<GameplayTag>();
            ApplicationBlockedTags = applicationBlockedTags ?? Array.Empty<GameplayTag>();
            ImmunityTags = immunityTags ?? Array.Empty<GameplayTag>();
            GrantedAbilityDefs = grantedAbilityDefs ?? Array.Empty<GameplayAbilityDef>();
            Executions = executions ?? Array.Empty<GameplayEffectExecution>();
            CueTagsOnApply = cueTagsOnApply ?? Array.Empty<string>();
            CueTagsOnRemove = cueTagsOnRemove ?? Array.Empty<string>();
            CostAttributes = costAttributes ?? new Dictionary<string, FP>();
            MaxStacks = maxStacks;
        }

        /// <summary>从现有 Spec 包装为 Def（兼容旧 API）。</summary>
        /// <param name="spec">运行时 Spec。</param>
        public GameplayEffectDef(GameplayEffectSpec spec)
        {
            EffectId = spec.EffectId;
            DurationPolicy = spec.DurationPolicy;
            Duration = spec.Duration;
            StackingPolicy = spec.StackingPolicy;
            Period = spec.Period;
            Modifiers = spec.Modifiers;
            GrantedTags = spec.GrantedTags;
            ApplicationRequiredTags = spec.ApplicationRequiredTags;
            ApplicationBlockedTags = spec.ApplicationBlockedTags;
            ImmunityTags = spec.ImmunityTags;
            GrantedAbilityDefs = spec.GrantedAbilityDefs;
            Executions = spec.Executions;
            CueTagsOnApply = spec.CueTagsOnApply;
            CueTagsOnRemove = spec.CueTagsOnRemove;
            CostAttributes = spec.CostAttributes;
            MaxStacks = spec.MaxStacks;
        }

        /// <summary>转为运行时 Spec。</summary>
        /// <param name="setByCaller">SetByCaller；可为 null。</param>
        /// <returns>运行时 Spec。</returns>
        public GameplayEffectSpec ToRuntimeSpec(IReadOnlyDictionary<string, FP> setByCaller = null) =>
            new GameplayEffectSpec(this, setByCaller);
    }
}
