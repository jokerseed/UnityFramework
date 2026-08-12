using Framework.Core;
using Framework.Core.Commands;
using Framework.Events;
using Framework.ECS;
using Framework.ECS.Components;
using Framework.GAS.Combat;

namespace Framework.GamePlay
{
    /// <summary>每 Tick 将命令缓冲刷入 ECS / GAS，替代热路径 EventBus 订阅。</summary>
    public sealed class BattleCommandProcessor
    {
        readonly World _world;
        readonly ActorRegistry _registry;

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
                _world.AddComponent(entity, new TransformComponent
                {
                    Position = command.Position,
                    Forward = command.Direction
                });
                _world.AddComponent(entity, new VelocityComponent
                {
                    Value = command.Direction.normalized * command.Speed
                });
                _world.AddComponent(entity, new ProjectileComponent
                {
                    Owner = command.Owner,
                    AbilityId = command.AbilityId,
                    Damage = command.Damage,
                    Radius = command.Radius,
                    RemainingLifetime = command.Lifetime,
                    TeamId = command.TeamId
                });
                _world.AddComponent(entity, new TeamComponent { TeamId = command.TeamId });
            }

            buffer.ClearSpawnProjectiles();
        }

        /// <summary>
        /// 将缓冲中所有 <see cref="ApplyDamageCommand"/> 分发给目标 Actor 的 GAS 执行伤害计算，
        /// 死亡时同步更新 ECS 战斗状态，最后清空队列。
        /// 应在 ECS Tick 之后调用。
        /// </summary>
        /// <param name="buffer">待处理的命令缓冲；处理完毕后会清空伤害队列。</param>
        /// <param name="presentation">表现层事件总线，用于广播伤害、死亡等事件。</param>
        public void FlushDamageCommands(BattleCommandBuffer buffer, IEventBus presentation)
        {
            var commands = buffer.ApplyDamage;
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                if (!_registry.TryGet(command.Target, out var targetActor))
                {
                    continue;
                }

                _registry.TryGet(command.Source, out var sourceActor);
                var context = new DamageContext(
                    command.Source,
                    command.Target,
                    command.Damage,
                    command.AbilityId);

                targetActor.AbilitySystem.ApplyDamage(context, presentation, sourceActor?.AbilitySystem);

                if (targetActor.AbilitySystem.Attributes.GetCurrentValue(BattleConstants.Health) <= 0f)
                {
                    _registry.MarkDead(command.Target);
                }
            }

            buffer.ClearApplyDamage();
        }
    }
}
