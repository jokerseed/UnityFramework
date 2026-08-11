using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Events;
using Framework.GAS.Tags;
using UnityEngine;

namespace Framework.GAS.Abilities
{
    public abstract class GameplayAbility
    {
        public string AbilityId { get; }
        public float Cooldown { get; }
        public IReadOnlyList<GameplayTag> RequiredTags { get; }
        public IReadOnlyList<GameplayTag> BlockedTags { get; }

        protected GameplayAbility(
            string abilityId,
            float cooldown,
            IReadOnlyList<GameplayTag> requiredTags = null,
            IReadOnlyList<GameplayTag> blockedTags = null)
        {
            AbilityId = abilityId;
            Cooldown = cooldown;
            RequiredTags = requiredTags ?? System.Array.Empty<GameplayTag>();
            BlockedTags = blockedTags ?? System.Array.Empty<GameplayTag>();
        }

        public virtual AbilityActivationResult CanActivate(AbilitySystemComponent owner, in AbilityActivationContext context)
        {
            if (owner.CooldownRemaining(AbilityId) > 0f)
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.OnCooldown);
            }

            if (!owner.Tags.HasAll(RequiredTags))
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.MissingRequiredTags);
            }

            if (owner.Tags.HasAny(BlockedTags))
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.HasBlockingTags);
            }

            return AbilityActivationResult.Succeeded();
        }

        public abstract void Activate(AbilitySystemComponent owner, in AbilityActivationContext context, BattleContext battle);

        public virtual void End(AbilitySystemComponent owner) { }
    }

    public readonly struct AbilityActivationContext
    {
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }
        public ActorId PrimaryTarget { get; }
        public float Range { get; }

        public AbilityActivationContext(
            Vector3 origin,
            Vector3 direction,
            ActorId primaryTarget = default,
            float range = 0f)
        {
            Origin = origin;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            PrimaryTarget = primaryTarget;
            Range = range;
        }
    }
}
