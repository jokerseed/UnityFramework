using Framework.Core;
using Framework.Core.Commands;
using Framework.Core.Events;
using Framework.GAS.Abilities;
using Framework.GAS.Targeting;
using UnityEngine;

namespace Framework.GAS.Abilities.Builtin
{
    /// <summary>发射弹道，写入命令缓冲由 Bridge 在 Tick 中刷入 ECS。</summary>
    public sealed class ProjectileAbility : GameplayAbility
    {
        readonly float _speed;
        readonly float _radius;
        readonly float _lifetime;
        readonly float _damage;
        readonly int _teamId;

        public ProjectileAbility(
            string abilityId,
            float cooldown,
            float speed,
            float radius,
            float lifetime,
            float damage,
            int teamId = 0)
            : base(abilityId, cooldown)
        {
            _speed = speed;
            _radius = radius;
            _lifetime = lifetime;
            _damage = damage;
            _teamId = teamId;
        }

        public override void Activate(AbilitySystemComponent owner, in AbilityActivationContext context, BattleContext battle)
        {
            battle.Presentation.Publish(new AbilityActivatedEvent
            {
                Caster = owner.ActorId,
                AbilityId = AbilityId,
                Origin = context.Origin,
                Direction = context.Direction,
                PrimaryTarget = context.PrimaryTarget
            });

            battle.Presentation.Publish(new GameplayCueEvent
            {
                Actor = owner.ActorId,
                CueTag = $"Cue.{AbilityId}.Cast",
                Position = context.Origin,
                Direction = context.Direction
            });

            battle.Commands.EnqueueSpawnProjectile(new SpawnProjectileCommand
            {
                Owner = owner.ActorId,
                AbilityId = AbilityId,
                Position = context.Origin,
                Direction = context.Direction,
                Speed = _speed,
                Radius = _radius,
                Lifetime = _lifetime,
                Damage = _damage,
                TeamId = _teamId
            });
        }
    }

    /// <summary>近战即时伤害，通过 TargetSelector 选择目标。</summary>
    public sealed class MeleeStrikeAbility : GameplayAbility
    {
        readonly float _damage;
        readonly float _range;
        readonly ITargetSelector _targetSelector;

        public MeleeStrikeAbility(
            string abilityId,
            float cooldown,
            float damage,
            float range,
            ITargetSelector targetSelector)
            : base(abilityId, cooldown)
        {
            _damage = damage;
            _range = range;
            _targetSelector = targetSelector;
        }

        public override AbilityActivationResult CanActivate(AbilitySystemComponent owner, in AbilityActivationContext context)
        {
            var baseResult = base.CanActivate(owner, context);
            if (!baseResult.Success)
            {
                return baseResult;
            }

            var queryContext = context.Range > 0f
                ? context
                : new AbilityActivationContext(context.Origin, context.Direction, context.PrimaryTarget, _range);

            return _targetSelector.TrySelectPrimary(owner, queryContext, out _)
                ? AbilityActivationResult.Succeeded()
                : AbilityActivationResult.Failed(AbilityActivationFailureReason.InvalidTarget);
        }

        public override void Activate(AbilitySystemComponent owner, in AbilityActivationContext context, BattleContext battle)
        {
            var queryContext = context.Range > 0f
                ? context
                : new AbilityActivationContext(context.Origin, context.Direction, context.PrimaryTarget, _range);

            if (!_targetSelector.TrySelectPrimary(owner, queryContext, out var target))
            {
                return;
            }

            battle.Presentation.Publish(new AbilityActivatedEvent
            {
                Caster = owner.ActorId,
                AbilityId = AbilityId,
                Origin = context.Origin,
                Direction = context.Direction,
                PrimaryTarget = target
            });

            battle.Commands.EnqueueApplyDamage(new ApplyDamageCommand
            {
                Source = owner.ActorId,
                Target = target,
                Damage = _damage,
                AbilityId = AbilityId
            });
        }
    }
}
