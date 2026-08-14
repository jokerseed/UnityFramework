using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.GAS.Abilities;
using Framework.GAS.Events;
using Framework.GAS.Tags;
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
        readonly int _pierceCount;
        readonly float _explodeRadius;
        readonly string _hitEffectId;
        readonly BattleDamageType _damageType;
        readonly IReadOnlyDictionary<string, float> _cost;

        /// <inheritdoc/>
        public override IReadOnlyDictionary<string, float> CostAttributes => _cost;

        /// <summary>构造弹道技能。</summary>
        public ProjectileAbility(
            string abilityId,
            float cooldown,
            float speed,
            float radius,
            float lifetime,
            float damage,
            int teamId = 0,
            int pierceCount = 0,
            float explodeRadius = 0f,
            string hitEffectId = null,
            BattleDamageType damageType = BattleDamageType.Physical,
            IReadOnlyList<GameplayTag> requiredTags = null,
            IReadOnlyList<GameplayTag> blockedTags = null,
            IReadOnlyDictionary<string, float> costAttributes = null)
            : base(abilityId, cooldown, requiredTags, blockedTags)
        {
            _speed = speed;
            _radius = radius;
            _lifetime = lifetime;
            _damage = damage;
            _teamId = teamId;
            _pierceCount = pierceCount;
            _explodeRadius = explodeRadius;
            _hitEffectId = hitEffectId;
            _damageType = damageType;
            _cost = costAttributes ?? new Dictionary<string, float>();
        }

        /// <inheritdoc/>
        public override void Activate(
            AbilitySystemComponent owner,
            ActiveAbilityInstance instance,
            BattleContext battle)
        {
            var context = instance.Context;
            PublishCast(owner, instance, battle, context.PrimaryTarget);
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
                TeamId = _teamId,
                PierceCount = _pierceCount,
                HitEffectId = _hitEffectId,
                ExplodeRadius = _explodeRadius,
                DamageType = _damageType
            });
        }

        internal static void PublishCast(
            AbilitySystemComponent owner,
            ActiveAbilityInstance instance,
            BattleContext battle,
            ActorId primaryTarget)
        {
            var context = instance.Context;
            battle.Presentation.Publish(new AbilityActivatedEvent
            {
                Caster = owner.ActorId,
                AbilityId = instance.Spec.Def.AbilityId,
                Origin = context.Origin,
                Direction = context.Direction,
                PrimaryTarget = primaryTarget
            });

            battle.Presentation.Publish(new GameplayCueEvent
            {
                Actor = owner.ActorId,
                CueTag = $"Cue.{instance.Spec.Def.AbilityId}.Cast",
                Position = context.Origin,
                Direction = context.Direction,
                Action = GameplayCueAction.Execute
            });
        }
    }

    /// <summary>近战即时伤害，通过 TargetSelector 选择目标。</summary>
    public sealed class MeleeStrikeAbility : GameplayAbility
    {
        readonly float _damage;
        readonly float _range;
        readonly Targeting.ITargetSelector _targetSelector;
        readonly IReadOnlyDictionary<string, float> _cost;
        readonly BattleDamageType _damageType;

        /// <inheritdoc/>
        public override IReadOnlyDictionary<string, float> CostAttributes => _cost;

        /// <summary>构造近战技能。</summary>
        public MeleeStrikeAbility(
            string abilityId,
            float cooldown,
            float damage,
            float range,
            Targeting.ITargetSelector targetSelector,
            BattleDamageType damageType = BattleDamageType.Physical,
            IReadOnlyList<GameplayTag> requiredTags = null,
            IReadOnlyList<GameplayTag> blockedTags = null,
            IReadOnlyDictionary<string, float> costAttributes = null)
            : base(abilityId, cooldown, requiredTags, blockedTags)
        {
            _damage = damage;
            _range = range;
            _targetSelector = targetSelector;
            _damageType = damageType;
            _cost = costAttributes ?? new Dictionary<string, float>();
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

            ProjectileAbility.PublishCast(owner, instance, battle, target);
            battle.Commands.EnqueueApplyDamage(new ApplyDamageCommand
            {
                Source = owner.ActorId,
                Target = target,
                Damage = _damage,
                AbilityId = AbilityId,
                DamageType = _damageType
            });
        }
    }

    /// <summary>圆形范围伤害。</summary>
    public sealed class CircleAoeAbility : GameplayAbility
    {
        readonly float _damage;
        readonly float _radius;
        readonly int _teamId;
        readonly string _hitEffectId;
        readonly BattleDamageType _damageType;
        readonly IReadOnlyDictionary<string, float> _cost;

        /// <inheritdoc/>
        public override IReadOnlyDictionary<string, float> CostAttributes => _cost;

        /// <summary>构造圆形 AOE。</summary>
        public CircleAoeAbility(
            string abilityId,
            float cooldown,
            float damage,
            float radius,
            int teamId,
            string hitEffectId = null,
            BattleDamageType damageType = BattleDamageType.Physical,
            IReadOnlyList<GameplayTag> requiredTags = null,
            IReadOnlyList<GameplayTag> blockedTags = null,
            IReadOnlyDictionary<string, float> costAttributes = null)
            : base(abilityId, cooldown, requiredTags, blockedTags)
        {
            _damage = damage;
            _radius = radius;
            _teamId = teamId;
            _hitEffectId = hitEffectId;
            _damageType = damageType;
            _cost = costAttributes ?? new Dictionary<string, float>();
        }

        /// <inheritdoc/>
        public override void Activate(
            AbilitySystemComponent owner,
            ActiveAbilityInstance instance,
            BattleContext battle)
        {
            ProjectileAbility.PublishCast(owner, instance, battle, instance.Context.PrimaryTarget);
            battle.Commands.EnqueueApplyAreaEffect(new ApplyAreaEffectCommand
            {
                Source = owner.ActorId,
                Origin = instance.Context.Origin,
                Radius = _radius,
                Damage = _damage,
                AbilityId = AbilityId,
                EffectId = _hitEffectId,
                TeamId = _teamId,
                DamageType = _damageType
            });
        }
    }

    /// <summary>扇形范围伤害。</summary>
    public sealed class ConeAoeAbility : GameplayAbility
    {
        readonly float _damage;
        readonly float _range;
        readonly float _halfAngleDegrees;
        readonly int _teamId;
        readonly string _hitEffectId;
        readonly BattleDamageType _damageType;
        readonly IReadOnlyDictionary<string, float> _cost;

        /// <inheritdoc/>
        public override IReadOnlyDictionary<string, float> CostAttributes => _cost;

        /// <summary>构造扇形 AOE。</summary>
        public ConeAoeAbility(
            string abilityId,
            float cooldown,
            float damage,
            float range,
            float halfAngleDegrees,
            int teamId,
            string hitEffectId = null,
            BattleDamageType damageType = BattleDamageType.Physical,
            IReadOnlyList<GameplayTag> requiredTags = null,
            IReadOnlyList<GameplayTag> blockedTags = null,
            IReadOnlyDictionary<string, float> costAttributes = null)
            : base(abilityId, cooldown, requiredTags, blockedTags)
        {
            _damage = damage;
            _range = range;
            _halfAngleDegrees = halfAngleDegrees;
            _teamId = teamId;
            _hitEffectId = hitEffectId;
            _damageType = damageType;
            _cost = costAttributes ?? new Dictionary<string, float>();
        }

        /// <inheritdoc/>
        public override void Activate(
            AbilitySystemComponent owner,
            ActiveAbilityInstance instance,
            BattleContext battle)
        {
            ProjectileAbility.PublishCast(owner, instance, battle, instance.Context.PrimaryTarget);
            battle.Commands.EnqueueApplyAreaEffect(new ApplyAreaEffectCommand
            {
                Source = owner.ActorId,
                Origin = instance.Context.Origin,
                Radius = _range,
                Damage = _damage,
                AbilityId = AbilityId,
                EffectId = _hitEffectId,
                TeamId = _teamId,
                DamageType = _damageType,
                HalfAngleDegrees = _halfAngleDegrees,
                Direction = instance.Context.Direction
            });
        }
    }

    /// <summary>蓄力后发射弹道的示例技能（AbilityTask 演示，锁方向）。</summary>
    public sealed class ChanneledProjectileAbility : GameplayAbility
    {
        readonly float _channelTime;
        readonly ProjectileAbility _projectile;

        /// <summary>构造蓄力弹道技能。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="cooldown">冷却秒数。</param>
        /// <param name="channelTime">引导时长（秒）。</param>
        /// <param name="projectile">引导结束后发射的弹道技能。</param>
        public ChanneledProjectileAbility(string abilityId, float cooldown, float channelTime, ProjectileAbility projectile)
            : base(abilityId, cooldown, projectile.RequiredTags, projectile.BlockedTags)
        {
            _channelTime = channelTime;
            _projectile = projectile;
        }

        /// <inheritdoc/>
        public override IReadOnlyDictionary<string, float> CostAttributes => _projectile.CostAttributes;

        /// <inheritdoc/>
        public override void Activate(
            AbilitySystemComponent owner,
            ActiveAbilityInstance instance,
            BattleContext battle)
        {
            instance.TaskRunner.AddTask(instance, new Tasks.WaitDelayTask(_channelTime, () =>
            {
                if (instance.State == ActiveAbilityState.Active)
                {
                    _projectile.Activate(owner, instance, battle);
                }
            }));
        }

        /// <inheritdoc/>
        public override bool AutoCommit => true;
    }

    /// <summary>近战扇形多目标：前摇、判定窗口、后摇；窗口内每个目标只结算一次。</summary>
    public sealed class MeleeSweepAbility : GameplayAbility
    {
        readonly Targeting.ConeEnemyQuery _queryCone;
        readonly float _damage;
        readonly float _range;
        readonly float _halfAngleDegrees;
        readonly float _windup;
        readonly float _hitDuration;
        readonly float _recovery;
        readonly float _knockback;
        readonly string _hitEffectId;
        readonly string _comboEffectId;
        readonly BattleDamageType _damageType;
        readonly IReadOnlyDictionary<string, float> _cost;

        /// <inheritdoc/>
        public override IReadOnlyDictionary<string, float> CostAttributes => _cost;

        /// <summary>构造近战扇形技能。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="cooldown">冷却秒数。</param>
        /// <param name="damage">对每个目标的伤害。</param>
        /// <param name="range">扇形半径（米）。</param>
        /// <param name="halfAngleDegrees">扇形半角（度）。</param>
        /// <param name="queryCone">扇形敌对查询；不可为 null。</param>
        /// <param name="windup">前摇秒数。</param>
        /// <param name="hitDuration">判定窗口秒数。</param>
        /// <param name="recovery">后摇秒数。</param>
        /// <param name="knockback">击退距离（米）。</param>
        /// <param name="hitEffectId">命中效果 ID；空则不施加。</param>
        /// <param name="comboEffectId">后摇时自身连招窗口效果 ID；空则不施加。</param>
        /// <param name="damageType">伤害类型。</param>
        /// <param name="requiredTags">激活前提标签。</param>
        /// <param name="blockedTags">阻止激活标签。</param>
        /// <param name="costAttributes">消耗。</param>
        public MeleeSweepAbility(
            string abilityId,
            float cooldown,
            float damage,
            float range,
            float halfAngleDegrees,
            Targeting.ConeEnemyQuery queryCone,
            float windup = 0f,
            float hitDuration = 0.1f,
            float recovery = 0.2f,
            float knockback = 0f,
            string hitEffectId = null,
            string comboEffectId = null,
            BattleDamageType damageType = BattleDamageType.Physical,
            IReadOnlyList<GameplayTag> requiredTags = null,
            IReadOnlyList<GameplayTag> blockedTags = null,
            IReadOnlyDictionary<string, float> costAttributes = null)
            : base(abilityId, cooldown, requiredTags, blockedTags)
        {
            _queryCone = queryCone;
            _damage = damage;
            _range = range;
            _halfAngleDegrees = halfAngleDegrees;
            _windup = windup;
            _hitDuration = hitDuration;
            _recovery = recovery;
            _knockback = knockback;
            _hitEffectId = hitEffectId;
            _comboEffectId = comboEffectId;
            _damageType = damageType;
            _cost = costAttributes ?? new Dictionary<string, float>();
        }

        /// <inheritdoc/>
        public override void Activate(
            AbilitySystemComponent owner,
            ActiveAbilityInstance instance,
            BattleContext battle)
        {
            PublishCast(owner, instance, battle, instance.Context.PrimaryTarget);
            instance.TaskRunner.AddTask(instance, new Tasks.MeleeSweepTask(
                owner,
                battle,
                _queryCone,
                _windup,
                _hitDuration,
                _recovery,
                _range,
                _halfAngleDegrees,
                _damage,
                _knockback,
                AbilityId,
                _hitEffectId,
                _comboEffectId,
                _damageType));
        }

        static void PublishCast(
            AbilitySystemComponent owner,
            ActiveAbilityInstance instance,
            BattleContext battle,
            ActorId primaryTarget)
        {
            ProjectileAbility.PublishCast(owner, instance, battle, primaryTarget);
        }
    }

    /// <summary>闪避冲刺：给自身上无敌帧，并沿朝向写入击退冲量。</summary>
    public sealed class DashAbility : GameplayAbility
    {
        readonly float _distance;
        readonly float _duration;
        readonly string _selfEffectId;

        /// <summary>构造闪避技能。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="cooldown">冷却秒数。</param>
        /// <param name="distance">冲刺距离（米）。</param>
        /// <param name="duration">无敌与技能持续秒数。</param>
        /// <param name="selfEffectId">施加到自身的效果 ID（如 IFrame）；空则只位移。</param>
        /// <param name="requiredTags">激活前提标签。</param>
        /// <param name="blockedTags">阻止激活标签。</param>
        public DashAbility(
            string abilityId,
            float cooldown,
            float distance,
            float duration,
            string selfEffectId,
            IReadOnlyList<GameplayTag> requiredTags = null,
            IReadOnlyList<GameplayTag> blockedTags = null)
            : base(abilityId, cooldown, requiredTags, blockedTags)
        {
            _distance = distance;
            _duration = duration > 0f ? duration : 0.25f;
            _selfEffectId = selfEffectId;
        }

        /// <inheritdoc/>
        public override void Activate(
            AbilitySystemComponent owner,
            ActiveAbilityInstance instance,
            BattleContext battle)
        {
            ProjectileAbility.PublishCast(owner, instance, battle, instance.Context.PrimaryTarget);
            var direction = instance.Context.Direction;
            if (_distance > 0f)
            {
                battle.Commands.EnqueueApplyDisplace(new ApplyDisplaceCommand
                {
                    Target = owner.ActorId,
                    Offset = direction * _distance
                });
            }

            if (!string.IsNullOrEmpty(_selfEffectId))
            {
                battle.Commands.EnqueueApplyEffect(new ApplyEffectCommand
                {
                    Source = owner.ActorId,
                    Target = owner.ActorId,
                    EffectId = _selfEffectId
                });
            }

            instance.TaskRunner.AddTask(instance, new Tasks.WaitDelayTask(_duration, () => { }));
        }
    }
}
