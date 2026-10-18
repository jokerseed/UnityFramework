using System;
using System.Collections.Generic;
using Framework.Battle;
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

        /// <summary>构造属性修改规则（常量幅度，兼容旧 API）。幅度按 <see cref="FixedConfig.FromConfig(float)"/> 量化到万分之一。</summary>
        public EffectModifier(string attributeName, EffectModifierOperation operation, float magnitude) // determinism-allow: 配置边界
        {
            AttributeName = attributeName;
            Operation = operation;
            Magnitude = ModifierMagnitude.Constant(FixedConfig.FromConfig(magnitude));
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
        /// <summary>运行时 Spec 句柄。0 表示尚未由 ASC 分配；施加时按该 ASC 的计数写入。</summary>
        public int RuntimeId { get; private set; }

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

        /// <summary>重复施加时是否重置周期计时；见 <see cref="GameplayEffectDef.ResetPeriodOnRefresh"/>。</summary>
        public bool ResetPeriodOnRefresh { get; }

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

        /// <summary>从 Def 构造运行时 Spec。RuntimeId 先为 0，由接受它的 ASC 分配。</summary>
        public GameplayEffectSpec(GameplayEffectDef def, IReadOnlyDictionary<string, FP> setByCaller = null)
            : this(def, setByCaller, 0)
        {
        }

        /// <summary>把尚未分配的 RuntimeId 记到所属 ASC。已有正数句柄时不改。</summary>
        /// <param name="id">该 ASC 分配的句柄，必须大于 0。</param>
        internal void AssignRuntimeId(int id)
        {
            if (RuntimeId > 0 || id <= 0)
            {
                return;
            }

            RuntimeId = id;
        }

        /// <summary>用显式 RuntimeId 构造 Spec，不推进静态计数。快照恢复使用。</summary>
        /// <param name="def">效果定义。</param>
        /// <param name="setByCaller">SetByCaller；可为 null。</param>
        /// <param name="runtimeId">已保存的 RuntimeId。</param>
        internal GameplayEffectSpec(GameplayEffectDef def, IReadOnlyDictionary<string, FP> setByCaller, int runtimeId)
        {
            RuntimeId = runtimeId;
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
            ResetPeriodOnRefresh = def.ResetPeriodOnRefresh;
        }
    }

    /// <summary>运行时活跃的 GameplayEffect 实例。</summary>
    public sealed class ActiveGameplayEffect
    {
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
        /// <param name="spec">效果 Spec。</param>
        /// <param name="source">来源 Actor。</param>
        /// <param name="handle">ASC 会话内计数或快照给出的句柄；必须大于 0。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="handle"/> 不大于 0。</exception>
        public ActiveGameplayEffect(GameplayEffectSpec spec, ActorId source, int handle)
        {
            if (handle <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(handle), handle, "Effect handles are allocated by the owning ASC and must be positive.");
            }

            Handle = new GameplayEffectHandle(handle);
            Spec = spec;
            Source = source;
            RemainingTime = spec.Duration;
            PeriodTimer = spec.Period;
        }

        /// <summary>快照里来源 ASC 引用是否存在。恢复后由外部重新绑定。</summary>
        internal bool SnapshotHadSourceAsc { get; set; }

        /// <summary>是否已过期。</summary>
        public bool IsExpired =>
            Spec.DurationPolicy == EffectDurationPolicy.Duration && RemainingTime <= FP.Zero;
    }
}
