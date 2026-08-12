using Framework.Core;
using Framework.Core.Commands;
using Framework.Events;
using Framework.ECS;
using Framework.ECS.Components;
using Framework.GAS.Combat;

namespace Framework.Bridge
{
    /// <summary>每 Tick 将命令缓冲刷入 ECS / GAS，替代热路径 EventBus 订阅。</summary>
    public sealed class BattleCommandProcessor
    {
        readonly World _world;
        readonly ActorRegistry _registry;

        public BattleCommandProcessor(World world, ActorRegistry registry)
        {
            _world = world;
            _registry = registry;
        }

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
