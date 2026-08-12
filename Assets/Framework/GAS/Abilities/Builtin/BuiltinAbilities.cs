using Framework.Core;
using Framework.Core.Commands;
using Framework.GAS.Abilities;
using Framework.GAS.Events;
using UnityEngine;

namespace Framework.GAS.Abilities.Builtin
{
    /// <summary>发射弹道，写入命令缓冲由 GamePlay 在 Tick 中刷入 ECS。</summary>
    public sealed class ProjectileAbility : GameplayAbility
    {
        readonly float _speed;
        readonly float _radius;
        readonly float _lifetime;
        readonly float _damage;
        readonly int _teamId;

        /// <summary>构造弹道技能。</summary>
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

        /// <inheritdoc/>
        public override void Activate(
            AbilitySystemComponent owner,
            ActiveAbilityInstance instance,
            BattleContext battle)
        {
            var context = instance.Context;
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
        readonly Targeting.ITargetSelector _targetSelector;

        /// <summary>构造近战技能。</summary>
        public MeleeStrikeAbility(
            string abilityId,
            float cooldown,
            float damage,
            float range,
            Targeting.ITargetSelector targetSelector)
            : base(abilityId, cooldown)
        {
            _damage = damage;
            _range = range;
            _targetSelector = targetSelector;
        }

        /// <inheritdoc/>
        public override AbilityActivationResult CanActivate(
            AbilitySystemComponent owner,
            in AbilityActivationContext context,
            GameplayAbilitySpec spec = null)
        {
            var baseResult = base.CanActivate(owner, context, spec);
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

        /// <inheritdoc/>
        public override void Activate(
            AbilitySystemComponent owner,
            ActiveAbilityInstance instance,
            BattleContext battle)
        {
            var context = instance.Context;
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

            battle.Presentation.Publish(new GameplayCueEvent
            {
                Actor = owner.ActorId,
                CueTag = $"Cue.{AbilityId}.Cast",
                Position = context.Origin,
                Direction = context.Direction
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

    /// <summary>蓄力后发射弹道的示例技能（AbilityTask 演示）。</summary>
    public sealed class ChanneledProjectileAbility : GameplayAbility
    {
        readonly float _channelTime;
        readonly ProjectileAbility _projectile;

        /// <summary>构造蓄力弹道技能。</summary>
        public ChanneledProjectileAbility(string abilityId, float cooldown, float channelTime, ProjectileAbility projectile)
            : base(abilityId, cooldown)
        {
            _channelTime = channelTime;
            _projectile = projectile;
        }

        /// <inheritdoc/>
        public override void Activate(
            AbilitySystemComponent owner,
            ActiveAbilityInstance instance,
            BattleContext battle)
        {
            instance.TaskRunner.AddTask(instance, new Tasks.WaitDelayTask(_channelTime, () =>
            {
                _projectile.Activate(owner, instance, battle);
            }));
        }

        /// <inheritdoc/>
        public override bool AutoCommit => true;
    }
}
