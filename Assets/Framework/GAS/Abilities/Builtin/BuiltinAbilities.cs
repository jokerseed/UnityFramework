using Framework.Core;
using Framework.Core.Commands;
using Framework.GAS.Abilities;
using Framework.GAS.Events;
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

        /// <summary>构造弹道技能。</summary>
        /// <param name="abilityId">技能唯一 ID。</param>
        /// <param name="cooldown">冷却时间（秒）。</param>
        /// <param name="speed">弹道飞行速度（米/秒）。</param>
        /// <param name="radius">弹道碰撞半径（米）。</param>
        /// <param name="lifetime">弹道最大存活时间（秒）；超时自动销毁。</param>
        /// <param name="damage">命中时造成的原始伤害量。</param>
        /// <param name="teamId">所属队伍 ID；用于区分敌我阵营。</param>
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

        /// <summary>激活弹道技能：发布表现事件并写入 SpawnProjectile 命令。</summary>
        /// <param name="owner">施放技能的单位 ASC。</param>
        /// <param name="context">激活上下文。</param>
        /// <param name="battle">当前帧战斗上下文。</param>
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

        /// <summary>构造近战即时伤害技能。</summary>
        /// <param name="abilityId">技能唯一 ID。</param>
        /// <param name="cooldown">冷却时间（秒）。</param>
        /// <param name="damage">造成的原始伤害量。</param>
        /// <param name="range">攻击范围（米）；上下文 Range &gt; 0 时以上下文为准。</param>
        /// <param name="targetSelector">目标选择策略；不可为 null。</param>
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

        /// <summary>在基类检查之上额外验证目标选择器能否选到有效目标。</summary>
        /// <param name="owner">持有技能的 ASC。</param>
        /// <param name="context">激活上下文。</param>
        /// <returns>基类通过且能选到目标时返回成功结果。</returns>
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

        /// <summary>激活近战技能：选择目标后发布表现事件并写入 ApplyDamage 命令。</summary>
        /// <param name="owner">施放技能的单位 ASC。</param>
        /// <param name="context">激活上下文。</param>
        /// <param name="battle">当前帧战斗上下文。</param>
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
