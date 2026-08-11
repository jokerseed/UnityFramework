using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.GAS.Attributes;
using Framework.GAS.Tags;

namespace Framework.GAS.Effects
{
    public enum EffectDurationPolicy
    {
        Instant,
        Duration,
        Infinite
    }

    public enum EffectModifierOperation
    {
        Add,
        Multiply
    }

    public readonly struct EffectModifier
    {
        public string AttributeName { get; }
        public EffectModifierOperation Operation { get; }
        public float Magnitude { get; }

        public EffectModifier(string attributeName, EffectModifierOperation operation, float magnitude)
        {
            AttributeName = attributeName;
            Operation = operation;
            Magnitude = magnitude;
        }
    }

    public sealed class GameplayEffectSpec
    {
        public string EffectId { get; }
        public EffectDurationPolicy DurationPolicy { get; }
        public EffectStackingPolicy StackingPolicy { get; }
        public float Duration { get; }
        public IReadOnlyList<EffectModifier> Modifiers { get; }
        public IReadOnlyList<GameplayTag> GrantedTags { get; }
        public IReadOnlyList<GameplayTag> ApplicationRequiredTags { get; }
        public IReadOnlyList<GameplayTag> ApplicationBlockedTags { get; }

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

    public sealed class ActiveGameplayEffect
    {
        public GameplayEffectSpec Spec { get; }
        public ActorId Source { get; }
        public float RemainingTime { get; set; }
        public int StackCount { get; set; } = 1;

        public ActiveGameplayEffect(GameplayEffectSpec spec, ActorId source)
        {
            Spec = spec;
            Source = source;
            RemainingTime = spec.Duration;
        }

        public bool IsExpired =>
            Spec.DurationPolicy == EffectDurationPolicy.Duration && RemainingTime <= 0f;
    }
}
