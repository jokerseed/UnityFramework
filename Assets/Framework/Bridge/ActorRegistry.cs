using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.ECS;
using Framework.ECS.Components;
using Framework.GAS;
using UnityEngine;

namespace Framework.Bridge
{
    public sealed class BattleActor
    {
        public ActorId ActorId { get; }
        public AbilitySystemComponent AbilitySystem { get; }
        public Entity EcsEntity { get; internal set; }
        public int TeamId { get; }
        public Vector3 Position { get; internal set; }

        public BattleActor(ActorId actorId, AbilitySystemComponent abilitySystem, int teamId)
        {
            ActorId = actorId;
            AbilitySystem = abilitySystem;
            TeamId = teamId;
        }
    }

    /// <summary>统一 Actor 注册表：GAS 与 ECS 的唯一关联点。</summary>
    public sealed class ActorRegistry
    {
        readonly Dictionary<ActorId, BattleActor> _actors = new Dictionary<ActorId, BattleActor>();
        readonly World _world;

        public ActorRegistry(World world)
        {
            _world = world;
        }

        public IReadOnlyDictionary<ActorId, BattleActor> Actors => _actors;

        public BattleActor Create(
            ActorId actorId,
            Vector3 position,
            float maxHealth,
            int teamId,
            AbilitySystemComponent asc)
        {
            if (_actors.ContainsKey(actorId))
            {
                throw new InvalidOperationException($"Actor {actorId} already exists.");
            }

            var actor = new BattleActor(actorId, asc, teamId) { Position = position };
            var entity = _world.CreateEntity();
            actor.EcsEntity = entity;

            _world.AddComponent(entity, new TransformComponent
            {
                Position = position,
                Forward = Vector3.forward
            });
            _world.AddComponent(entity, new ActorLinkComponent { ActorId = actorId });
            _world.AddComponent(entity, new CombatStateComponent { IsAlive = true });
            _world.AddComponent(entity, new TeamComponent { TeamId = teamId });
            _world.AddComponent(entity, new CollisionComponent { Radius = BattleConstants.DefaultActorCollisionRadius });

            _actors[actorId] = actor;
            return actor;
        }

        public bool TryGet(ActorId actorId, out BattleActor actor) => _actors.TryGetValue(actorId, out actor);

        public bool TryGetEntity(ActorId actorId, out Entity entity)
        {
            if (_actors.TryGetValue(actorId, out var actor) && actor.EcsEntity != null)
            {
                entity = actor.EcsEntity;
                return true;
            }

            entity = null;
            return false;
        }

        public void MarkDead(ActorId actorId)
        {
            if (!TryGetEntity(actorId, out var entity))
            {
                return;
            }

            _world.AddComponent(entity, new CombatStateComponent { IsAlive = false });
        }

        public ActorId QueryNearestEnemy(ActorId source, Vector3 origin, float range)
        {
            if (!_actors.TryGetValue(source, out var sourceActor))
            {
                return ActorId.Invalid;
            }

            ActorId best = ActorId.Invalid;
            var bestDistance = float.MaxValue;

            foreach (var pair in _actors)
            {
                var target = pair.Value;
                if (target.TeamId == sourceActor.TeamId || target.ActorId == source)
                {
                    continue;
                }

                if (target.AbilitySystem.Tags.HasTag(new GAS.Tags.GameplayTag(BattleConstants.TagDead)))
                {
                    continue;
                }

                var distance = Vector3.Distance(origin, target.Position);
                if (distance > range || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = target.ActorId;
            }

            return best;
        }

        public void SyncPositionsFromEcs()
        {
            foreach (var pair in _actors)
            {
                var actor = pair.Value;
                if (actor.EcsEntity == null)
                {
                    continue;
                }

                if (_world.TryGetComponent(actor.EcsEntity, out TransformComponent transform))
                {
                    actor.Position = transform.Position;
                }
            }
        }

        public void Remove(ActorId actorId)
        {
            if (_actors.TryGetValue(actorId, out var actor) && actor.EcsEntity != null)
            {
                _world.DestroyEntity(actor.EcsEntity);
            }

            _actors.Remove(actorId);
        }
    }
}
