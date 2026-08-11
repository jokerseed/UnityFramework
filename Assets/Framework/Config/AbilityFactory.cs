using System.Collections.Generic;
using cfg;
using Framework.Core;
using Framework.GAS.Abilities;
using Framework.GAS.Abilities.Builtin;
using Framework.GAS.Effects;
using Framework.GAS.Tags;

namespace Framework.Config
{
    public sealed class AbilityFactory
    {
        readonly System.Func<ActorId, UnityEngine.Vector3, float, ActorId> _queryNearestEnemy;

        public AbilityFactory(System.Func<ActorId, UnityEngine.Vector3, float, ActorId> queryNearestEnemy)
        {
            _queryNearestEnemy = queryNearestEnemy;
        }

        public GameplayAbility Create(AbilityDef def, int teamId)
        {
            switch (def.Type)
            {
                case AbilityType.Projectile:
                    return new ProjectileAbility(
                        def.Id,
                        def.Cooldown,
                        def.Speed,
                        def.Radius,
                        def.Lifetime,
                        def.Damage,
                        teamId);
                case AbilityType.Melee:
                    return new MeleeStrikeAbility(
                        def.Id,
                        def.Cooldown,
                        def.Damage,
                        def.Range,
                        new GAS.Targeting.MeleeTargetSelector(_queryNearestEnemy));
                default:
                    throw new System.NotSupportedException($"Unsupported ability type: {def.Type} ({def.Id})");
            }
        }
    }

    public static class EffectFactory
    {
        public static GameplayEffectSpec Create(EffectDef def)
        {
            var modifiers = new List<EffectModifier>();
            if (!string.IsNullOrEmpty(def.ModAttribute))
            {
                modifiers.Add(new EffectModifier(
                    def.ModAttribute,
                    (EffectModifierOperation)def.ModOperation,
                    def.ModMagnitude));
            }

            return new GameplayEffectSpec(
                def.Id,
                MapDuration(def.DurationType),
                def.Duration,
                modifiers,
                grantedTags: null,
                stackingPolicy: MapStacking(def.Stacking));
        }

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
