using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.GAS.Attributes;
using Framework.GAS.Tags;

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
        Multiply
    }

    /// <summary>描述单条属性修改规则（目标属性、运算方式、幅度）。</summary>
    public readonly struct EffectModifier
    {
        /// <summary>被修改的属性名称（对应 <see cref="GameplayAttribute.Name"/>）。</summary>
        public string AttributeName { get; }

        /// <summary>修改运算方式（加法或乘法）。</summary>
        public EffectModifierOperation Operation { get; }

        /// <summary>修改幅度；加法时为加量，乘法时为系数。</summary>
        public float Magnitude { get; }

        /// <summary>构造属性修改规则。</summary>
        /// <param name="attributeName">目标属性名称。</param>
        /// <param name="operation">运算方式。</param>
        /// <param name="magnitude">修改幅度。</param>
        public EffectModifier(string attributeName, EffectModifierOperation operation, float magnitude)
        {
            AttributeName = attributeName;
            Operation = operation;
            Magnitude = magnitude;
        }
    }

    /// <summary>GameplayEffect 的不可变描述数据，包含持续时间策略、叠加策略、属性修改器与 Tag 列表。</summary>
    public sealed class GameplayEffectSpec
    {
        /// <summary>效果唯一标识符，用于叠加判断。</summary>
        public string EffectId { get; }

        /// <summary>持续时间策略（瞬时 / 持续 / 永久）。</summary>
        public EffectDurationPolicy DurationPolicy { get; }

        /// <summary>同 ID 效果的叠加策略。</summary>
        public EffectStackingPolicy StackingPolicy { get; }

        /// <summary>持续时长（秒）；仅 <see cref="EffectDurationPolicy.Duration"/> 模式下有效。</summary>
        public float Duration { get; }

        /// <summary>属性修改器列表；瞬时与持续效果均会应用。</summary>
        public IReadOnlyList<EffectModifier> Modifiers { get; }

        /// <summary>效果激活时授予目标的 Tag 列表；效果结束时移除。</summary>
        public IReadOnlyList<GameplayTag> GrantedTags { get; }

        /// <summary>应用前提标签：目标必须持有所有标签，否则拒绝应用。</summary>
        public IReadOnlyList<GameplayTag> ApplicationRequiredTags { get; }

        /// <summary>应用阻断标签：目标持有任意标签时拒绝应用。</summary>
        public IReadOnlyList<GameplayTag> ApplicationBlockedTags { get; }

        /// <summary>构造 GameplayEffect 描述数据。</summary>
        /// <param name="effectId">效果唯一 ID；不可为 null 或空。</param>
        /// <param name="durationPolicy">持续时间策略。</param>
        /// <param name="duration">持续时长（秒）；Instant/Infinite 可传 0。</param>
        /// <param name="modifiers">属性修改器列表；为 null 时视为空列表。</param>
        /// <param name="grantedTags">授予 Tag 列表；为 null 时视为空列表。</param>
        /// <param name="stackingPolicy">叠加策略；默认 <see cref="EffectStackingPolicy.None"/>。</param>
        /// <param name="applicationRequiredTags">应用前提标签；为 null 时视为空列表。</param>
        /// <param name="applicationBlockedTags">应用阻断标签；为 null 时视为空列表。</param>
        public GameplayEffectSpec(
            string effectId,
            EffectDurationPolicy durationPolicy,
            float duration,
            IReadOnlyList<EffectModifier> modifiers,
            IReadOnlyList<GameplayTag> grantedTags = null,
            EffectStackingPolicy stackingPolicy = EffectStackingPolicy.None,
            IReadOnlyList<GameplayTag> applicationRequiredTags = null,
            IReadOnlyList<GameplayTag> applicationBlockedTags = null)
        {
            EffectId = effectId;
            DurationPolicy = durationPolicy;
            Duration = duration;
            Modifiers = modifiers ?? Array.Empty<EffectModifier>();
            GrantedTags = grantedTags ?? Array.Empty<GameplayTag>();
            StackingPolicy = stackingPolicy;
            ApplicationRequiredTags = applicationRequiredTags ?? Array.Empty<GameplayTag>();
            ApplicationBlockedTags = applicationBlockedTags ?? Array.Empty<GameplayTag>();
        }
    }

    /// <summary>运行时活跃的 GameplayEffect 实例，跟踪剩余时间与叠加层数。</summary>
    public sealed class ActiveGameplayEffect
    {
        /// <summary>对应的效果描述数据。</summary>
        public GameplayEffectSpec Spec { get; }

        /// <summary>施加该效果的来源单位标识符。</summary>
        public ActorId Source { get; }

        /// <summary>剩余持续时间（秒）；每帧由 ASC Tick 递减。</summary>
        public float RemainingTime { get; set; }

        /// <summary>当前叠加层数；<see cref="EffectStackingPolicy.StackCount"/> 模式下递增。</summary>
        public int StackCount { get; set; } = 1;

        /// <summary>创建活跃效果实例。</summary>
        /// <param name="spec">效果描述数据；不可为 null。</param>
        /// <param name="source">来源单位标识符。</param>
        public ActiveGameplayEffect(GameplayEffectSpec spec, ActorId source)
        {
            Spec = spec;
            Source = source;
            RemainingTime = spec.Duration;
        }

        /// <summary>效果是否已过期（仅限 <see cref="EffectDurationPolicy.Duration"/> 策略）。</summary>
        public bool IsExpired =>
            Spec.DurationPolicy == EffectDurationPolicy.Duration && RemainingTime <= 0f;
    }
}
