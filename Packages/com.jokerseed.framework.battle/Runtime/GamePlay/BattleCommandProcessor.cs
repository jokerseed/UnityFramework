using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.Events;
using Framework.ECS;
using Framework.ECS.Components;
using Framework.GAS;
using Framework.GAS.Combat;
using Framework.GAS.Targeting;
using Framework.GamePlay.Data;
using Framework.FixedMath;

namespace Framework.GamePlay
{
    /// <summary>每 Tick 将命令缓冲刷入 ECS / GAS，替代热路径 EventBus 订阅。</summary>
    public sealed class BattleCommandProcessor
    {
        readonly World _world;
        readonly ActorRegistry _registry;
        readonly List<ActorId> _areaScratch = new List<ActorId>(16);

        /// <summary>
        /// 具名效果目录。模拟结算只读这个目录，不再回退到全局 <c>ConfigManager</c>。
        /// 未设置或找不到效果时抛出 <see cref="InvalidOperationException"/>，两端一起失败。
        /// </summary>
        public IBattleEffectCatalog EffectCatalog { get; set; }

        /// <summary>创建命令处理器。</summary>
        /// <param name="world">ECS 世界实例，用于创建投射物实体。</param>
        /// <param name="registry">Actor 注册表，用于查找伤害目标的 GAS 组件。</param>
        public BattleCommandProcessor(World world, ActorRegistry registry)
        {
            _world = world;
            _registry = registry;
        }

        /// <summary>
        /// 将缓冲中所有 <see cref="SpawnProjectileCommand"/> 转换为 ECS 实体并清空队列。
        /// 应在 ECS Tick 之前调用，以使本帧生成的投射物参与本帧碰撞检测。
        /// </summary>
        /// <param name="buffer">待处理的命令缓冲；处理完毕后会清空投射物生成队列。</param>
        public void FlushSpawnCommands(BattleCommandBuffer buffer)
        {
            var commands = buffer.SpawnProjectiles;
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                var entity = _world.CreateEntity();
                var direction = command.Direction;
                direction.y = FP.Zero;
                if (direction.sqrMagnitude <= FP.EN4)
                {
                    direction = TSVector.forward;
                }
                else
                {
                    direction.Normalize();
                }

                _world.AddComponent(entity, new TransformComponent
                {
                    Position = command.Position,
                    Forward = direction
                });
                _world.AddComponent(entity, new VelocityComponent
                {
                    Value = direction * command.Speed
                });
                _world.AddComponent(entity, new ProjectileComponent
                {
                    Owner = command.Owner,
                    AbilityId = command.AbilityId,
                    Damage = command.Damage,
                    Radius = command.Radius,
                    RemainingLifetime = command.Lifetime,
                    TeamId = command.TeamId,
                    PierceRemaining = command.PierceCount,
                    HitEffectId = command.HitEffectId,
                    ExplodeRadius = command.ExplodeRadius,
                    DamageType = command.DamageType
                });
                _world.AddComponent(entity, new TeamComponent { TeamId = command.TeamId });
                _world.GetSingleton<BattlePhysicsWorld>()?.AddProjectileBody(
                    entity.Id,
                    command.Owner,
                    command.TeamId,
                    command.Position,
                    command.Radius);
            }

            buffer.ClearSpawnProjectiles();
        }

        /// <summary>
        /// 刷写伤害、治疗、效果、范围与位移命令，并同步死亡状态。
        /// 应在 ECS Tick 之后调用。
        /// </summary>
        /// <param name="buffer">待处理的命令缓冲。</param>
        /// <param name="presentation">表现层事件总线。</param>
        public void FlushOutcomeCommands(BattleCommandBuffer buffer, IEventBus presentation)
        {
            var failed = false;
            try
            {
                for (var round = 0; round < 4; round++)
                {
                    if (buffer.ApplyDamage.Count == 0 &&
                        buffer.ApplyHeal.Count == 0 &&
                        buffer.ApplyEffect.Count == 0 &&
                        buffer.ApplyAreaEffect.Count == 0 &&
                        buffer.ApplyDisplace.Count == 0)
                    {
                        break;
                    }

                    FlushDamage(buffer, presentation);
                    buffer.ClearApplyDamage();
                    FlushHeal(buffer, presentation);
                    buffer.ClearApplyHeal();
                    FlushEffects(buffer, presentation);
                    buffer.ClearApplyEffect();
                    FlushArea(buffer, presentation);
                    buffer.ClearApplyAreaEffect();
                    FlushDisplace(buffer);
                    buffer.ClearApplyDisplace();
                }
            }
            catch
            {
                failed = true;
                throw;
            }
            finally
            {
                if (failed)
                {
                    buffer.ClearApplyDamage();
                    buffer.ClearApplyHeal();
                    buffer.ClearApplyEffect();
                    buffer.ClearApplyAreaEffect();
                    buffer.ClearApplyDisplace();
                }
            }
        }

        void FlushDamage(BattleCommandBuffer buffer, IEventBus presentation)
        {
            var commands = buffer.ApplyDamage;
            for (var i = 0; i < commands.Count; i++)
            {
                ApplyDamageToActor(commands[i], presentation);
            }
        }

        void ApplyDamageToActor(in ApplyDamageCommand command, IEventBus presentation)
        {
            if (!_registry.TryGet(command.Target, out var targetActor))
            {
                return;
            }

            _registry.TryGet(command.Source, out var sourceActor);
            var context = new DamageContext(
                command.Source,
                command.Target,
                command.Damage,
                command.AbilityId,
                damageType: command.DamageType);

            targetActor.AbilitySystem.ApplyDamage(context, presentation, sourceActor?.AbilitySystem);
            if (targetActor.AbilitySystem.IsDead)
            {
                _registry.MarkDead(command.Target);
            }
        }

        void FlushHeal(BattleCommandBuffer buffer, IEventBus presentation)
        {
            var commands = buffer.ApplyHeal;
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                if (!_registry.TryGet(command.Target, out var targetActor))
                {
                    continue;
                }

                targetActor.AbilitySystem.ApplyHeal(command.Amount, presentation);
            }
        }

        void FlushEffects(BattleCommandBuffer buffer, IEventBus presentation)
        {
            var commands = buffer.ApplyEffect;
            for (var i = 0; i < commands.Count; i++)
            {
                ApplyNamedEffect(commands[i].Source, commands[i].Target, commands[i].EffectId, presentation);
            }
        }

        void FlushArea(BattleCommandBuffer buffer, IEventBus presentation)
        {
            var commands = buffer.ApplyAreaEffect;
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                var filter = new TargetDataFilter(command.Source, command.TeamId, enemiesOnly: true);
                if (command.HalfAngleDegrees > FP.Zero)
                {
                    _registry.QueryTargetsInCone(
                        command.Origin,
                        command.Direction,
                        command.HalfAngleDegrees,
                        command.Radius,
                        filter,
                        _areaScratch);
                }
                else
                {
                    _registry.QueryTargetsInRadius(command.Origin, command.Radius, filter, _areaScratch);
                }
                for (var t = 0; t < _areaScratch.Count; t++)
                {
                    var targetId = _areaScratch[t];
                    if (command.Damage > FP.Zero)
                    {
                        ApplyDamageToActor(
                            new ApplyDamageCommand
                            {
                                Source = command.Source,
                                Target = targetId,
                                Damage = command.Damage,
                                AbilityId = command.AbilityId,
                                DamageType = command.DamageType
                            },
                            presentation);
                    }

                    if (!string.IsNullOrEmpty(command.EffectId))
                    {
                        ApplyNamedEffect(command.Source, targetId, command.EffectId, presentation);
                    }
                }
            }
        }

        void FlushDisplace(BattleCommandBuffer buffer)
        {
            var commands = buffer.ApplyDisplace;
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                _registry.ApplyDisplacement(command.Target, command.Offset);
            }
        }

        void ApplyNamedEffect(ActorId source, ActorId target, string effectId, IEventBus presentation)
        {
            if (string.IsNullOrEmpty(effectId) || !_registry.TryGet(target, out var targetActor))
            {
                return;
            }

            if (EffectCatalog == null)
            {
                throw new InvalidOperationException(
                    "EffectCatalog is not set. Inject IBattleEffectCatalog before applying '" + effectId + "'.");
            }

            if (!EffectCatalog.TryCreateEffect(effectId, out var catalogDef))
            {
                throw new InvalidOperationException(
                    "Effect '" + effectId + "' is not in the injected catalog.");
            }

            _registry.TryGet(source, out var catalogSource);
            targetActor.AbilitySystem.ApplyEffect(
                catalogDef,
                source,
                presentation,
                sourceAsc: catalogSource?.AbilitySystem);
        }
    }
}
