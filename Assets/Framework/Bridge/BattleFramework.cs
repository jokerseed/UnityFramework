using System;
using Framework.Core;
using Framework.Core.Commands;
using Framework.Core.Tick;
using Framework.ECS;
using Framework.ECS.Systems;
using Framework.Events;
using Framework.GAS;
using Framework.GAS.Abilities;
using UnityEngine;

namespace Framework.Bridge
{
    /// <summary>
    /// 战斗框架入口。
    /// Tick 顺序：GAS Tick → Flush Spawn → ECS Tick → Flush Damage → 同步坐标
    /// </summary>
    public sealed class BattleFramework : ITickable, IDisposable
    {
        readonly ZeroGcEventBus _presentationBus = new ZeroGcEventBus();
        readonly BattleCommandBuffer _commandBuffer = new BattleCommandBuffer();
        readonly BattleContext _battleContext;
        readonly World _world;
        readonly ActorRegistry _registry;
        readonly BattleCommandProcessor _commandProcessor;

        public IEventBus EventBus => _presentationBus;
        public BattleCommandBuffer Commands => _commandBuffer;
        public BattleContext Context => _battleContext;
        public World EcsWorld => _world;
        public ActorRegistry Registry => _registry;

        public BattleFramework()
        {
            _battleContext = new BattleContext(_commandBuffer, _presentationBus);
            _world = new World { Commands = _commandBuffer };
            _registry = new ActorRegistry(_world);
            _commandProcessor = new BattleCommandProcessor(_world, _registry);

            _world.AddSystem(new SpatialIndexSystem());
            _world.AddSystem(new MovementSystem());
            _world.AddSystem(new ProjectileCollisionSystem());
            _world.AddSystem(new ProjectileLifetimeSystem());
        }

        public AbilitySystemComponent CreateActor(
            ActorId actorId,
            Vector3 position,
            float maxHealth = 100f,
            int teamId = 0)
        {
            var asc = new AbilitySystemComponent(actorId);
            asc.InitializeHealth(maxHealth);
            asc.Attributes.GetOrCreate(BattleConstants.Attack, 10f);
            asc.Attributes.GetOrCreate(BattleConstants.Defense, 0f);

            _registry.Create(actorId, position, maxHealth, teamId, asc);
            return asc;
        }

        public bool TryGetActor(ActorId actorId, out AbilitySystemComponent asc)
        {
            if (_registry.TryGet(actorId, out var actor))
            {
                asc = actor.AbilitySystem;
                return true;
            }

            asc = null;
            return false;
        }

        public void RegisterAbility(ActorId actorId, GameplayAbility ability)
        {
            if (!_registry.TryGet(actorId, out var actor))
            {
                throw new InvalidOperationException($"Cannot register ability: actor {actorId} not found.");
            }

            actor.AbilitySystem.RegisterAbility(ability);
        }

        public AbilityActivationResult TryActivateAbility(
            ActorId actorId,
            string abilityId,
            in AbilityActivationContext context)
        {
            if (!_registry.TryGet(actorId, out var actor))
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.CustomBlocked);
            }

            return actor.AbilitySystem.TryActivateAbility(abilityId, context, _battleContext);
        }

        public ActorId QueryNearestEnemy(ActorId source, Vector3 origin, float range) =>
            _registry.QueryNearestEnemy(source, origin, range);

        public void DestroyActor(ActorId actorId) => _registry.Remove(actorId);

        public void Tick(float deltaTime)
        {
            foreach (var pair in _registry.Actors)
            {
                pair.Value.AbilitySystem.Tick(deltaTime, _presentationBus);
            }

            _commandProcessor.FlushSpawnCommands(_commandBuffer);
            _world.Tick(deltaTime);
            _commandProcessor.FlushDamageCommands(_commandBuffer, _presentationBus);
            _registry.SyncPositionsFromEcs();
        }

        public void Dispose()
        {
            _world.Dispose();
            _commandBuffer.ClearAll();
            _presentationBus.Clear();
        }
    }
}
