using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.FixedMath;
using Framework.GAS.Abilities;
using Framework.GAS.Tags;
using AbilitySystemComponent = Framework.GAS.AbilitySystemComponent;

namespace Framework.GAS.Effects
{
    /// <summary>GameplayEffect 持续时间策略。</summary>
    public enum EffectDurationPolicy
    {
        /// <summary>立即生效并结束，不保留运行时实例。</summary>
        Instant,

        /// <summary>在指定时长内持续生效。</summary>
        Duration,

        /// <summary>永久生效，直到被显式移除。</summary>
        Infinite
    }

    /// <summary>属性修改器的运算方式。</summary>
    public enum EffectModifierOperation
    {
        /// <summary>在当前值上加上修改量。</summary>
        Add,

        /// <summary>在当前值上乘以修改系数。</summary>
        Multiply,

        /// <summary>覆盖当前值。</summary>
        Override
    }

    /// <summary>描述单条属性修改规则（目标属性、运算方式、幅度）。</summary>
    public readonly struct EffectModifier
    {
        /// <summary>被修改的属性名称。</summary>
        public string AttributeName { get; }

        /// <summary>修改运算方式。</summary>
        public EffectModifierOperation Operation { get; }

        /// <summary>修改幅度。</summary>
        public ModifierMagnitude Magnitude { get; }

        /// <summary>构造属性修改规则（常量幅度，兼容旧 API）。</summary>
        public EffectModifier(string attributeName, EffectModifierOperation operation, float magnitude)
        {
            AttributeName = attributeName;
            Operation = operation;
            Magnitude = ModifierMagnitude.Constant(magnitude);
        }

        /// <summary>构造属性修改规则。</summary>
        public EffectModifier(string attributeName, EffectModifierOperation operation, ModifierMagnitude magnitude)
        {
            AttributeName = attributeName;
            Operation = operation;
            Magnitude = magnitude;
        }
    }

    /// <summary>GameplayEffect 运行时 Spec。</summary>
    public sealed class GameplayEffectSpec
    {
        static int s_nextHandle = 1;

        /// <summary>运行时 Spec 句柄（Apply 时分配）。</summary>
        public int RuntimeId { get; }

        /// <summary>效果唯一标识符。</summary>
        public string EffectId { get; }

        /// <summary>持续时间策略。</summary>
        public EffectDurationPolicy DurationPolicy { get; }

        /// <summary>叠加策略。</summary>
        public EffectStackingPolicy StackingPolicy { get; }

        /// <summary>持续时长（秒）。</summary>
        public FP Duration { get; }

        /// <summary>周期（秒）。</summary>
        public FP Period { get; }

        /// <summary>属性修改器列表。</summary>
        public IReadOnlyList<EffectModifier> Modifiers { get; }

        /// <summary>授予 Tag 列表。</summary>
        public IReadOnlyList<GameplayTag> GrantedTags { get; }

        /// <summary>应用前提 Tag。</summary>
        public IReadOnlyList<GameplayTag> ApplicationRequiredTags { get; }

        /// <summary>应用阻断 Tag。</summary>
        public IReadOnlyList<GameplayTag> ApplicationBlockedTags { get; }

        /// <summary>免疫 Tag。</summary>
        public IReadOnlyList<GameplayTag> ImmunityTags { get; }

        /// <summary>授予技能定义。</summary>
        public IReadOnlyList<GameplayAbilityDef> GrantedAbilityDefs { get; }

        /// <summary>Execution 列表。</summary>
        public IReadOnlyList<GameplayEffectExecution> Executions { get; }

        /// <summary>施加 Cue Tag。</summary>
        public IReadOnlyList<string> CueTagsOnApply { get; }

        /// <summary>移除 Cue Tag。</summary>
        public IReadOnlyList<string> CueTagsOnRemove { get; }

        /// <summary>Cost 属性消耗。</summary>
        public IReadOnlyDictionary<string, FP> CostAttributes { get; }

        /// <summary>Apply 时的 SetByCaller。</summary>
        public IReadOnlyDictionary<string, FP> SetByCaller { get; }

        /// <summary>叠层上限；≤0 表示不限制。</summary>
        public int MaxStacks { get; }

        /// <summary>从 Def 构造运行时 Spec（兼容旧构造函数）。</summary>
        public GameplayEffectSpec(
            string effectId,
            EffectDurationPolicy durationPolicy,
            FP duration,
            IReadOnlyList<EffectModifier> modifiers,
            IReadOnlyList<GameplayTag> grantedTags = null,
            EffectStackingPolicy stackingPolicy = EffectStackingPolicy.None,
            IReadOnlyList<GameplayTag> applicationRequiredTags = null,
            IReadOnlyList<GameplayTag> applicationBlockedTags = null)
            : this(new GameplayEffectDef(
                effectId,
                durationPolicy,
                duration,
                modifiers,
                grantedTags,
                stackingPolicy,
                applicationRequiredTags,
                applicationBlockedTags))
        {
        }

        /// <summary>从 Def 构造运行时 Spec。</summary>
        public GameplayEffectSpec(GameplayEffectDef def, IReadOnlyDictionary<string, FP> setByCaller = null)
        {
            RuntimeId = s_nextHandle++;
            EffectId = def.EffectId;
            DurationPolicy = def.DurationPolicy;
            Duration = def.Duration;
            StackingPolicy = def.StackingPolicy;
            Period = def.Period;
            Modifiers = def.Modifiers;
            GrantedTags = def.GrantedTags;
            ApplicationRequiredTags = def.ApplicationRequiredTags;
            ApplicationBlockedTags = def.ApplicationBlockedTags;
            ImmunityTags = def.ImmunityTags;
            GrantedAbilityDefs = def.GrantedAbilityDefs;
            Executions = def.Executions;
            CueTagsOnApply = def.CueTagsOnApply;
            CueTagsOnRemove = def.CueTagsOnRemove;
            CostAttributes = def.CostAttributes;
            SetByCaller = setByCaller;
            MaxStacks = def.MaxStacks;
        }
    }

    /// <summary>运行时活跃的 GameplayEffect 实例。</summary>
    public sealed class ActiveGameplayEffect
    {
        static int s_nextHandle = 1;

        /// <summary>活跃效果句柄。</summary>
        public GameplayEffectHandle Handle { get; }

        /// <summary>效果 Spec。</summary>
        public GameplayEffectSpec Spec { get; }

        /// <summary>来源 Actor。</summary>
        public ActorId Source { get; }

        /// <summary>剩余持续时间。</summary>
        public FP RemainingTime { get; set; }

        /// <summary>周期计时器。</summary>
        public FP PeriodTimer { get; set; }

        /// <summary>叠加层数。</summary>
        public int StackCount { get; set; } = 1;

        /// <summary>本效果授予的技能 Spec 句柄。</summary>
        public List<GameplayAbilitySpecHandle> GrantedAbilityHandles { get; } = new List<GameplayAbilitySpecHandle>();

        /// <summary>施加时的来源 ASC；Periodic Execution 用其攻击力。可为 null。</summary>
        public AbilitySystemComponent SourceAsc { get; set; }

        /// <summary>创建活跃效果。</summary>
        public ActiveGameplayEffect(GameplayEffectSpec spec, ActorId source)
        {
            Handle = new GameplayEffectHandle(s_nextHandle++);
            Spec = spec;
            Source = source;
            RemainingTime = spec.Duration;
            PeriodTimer = spec.Period;
        }

        /// <summary>是否已过期。</summary>
        public bool IsExpired =>
            Spec.DurationPolicy == EffectDurationPolicy.Duration && RemainingTime <= FP.Zero;
    }
}
