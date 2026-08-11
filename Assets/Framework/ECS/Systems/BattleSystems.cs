using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.Core.Events;
using Framework.ECS.Components;
using UnityEngine;

namespace Framework.ECS.Systems
{
    public sealed class SpatialIndexSystem : ISystem
    {
        SpatialHashGrid _grid;

        public void OnCreate(World world)
        {
            _grid = new SpatialHashGrid(BattleConstants.SpatialCellSize);
            world.UserData = _grid;
        }

        public void OnDestroy(World world) { }

        public void Update(World world, float deltaTime)
        {
            _grid.Clear();
            var transforms = world.GetStorage<TransformComponent>();
            var actors = world.GetStorage<ActorLinkComponent>();

            foreach (var pair in actors.All)
            {
                if (transforms.TryGet(pair.Key, out var transform))
                {
                    _grid.Insert(pair.Key, transform.Position);
                }
            }
        }
    }

    public sealed class MovementSystem : ISystem
    {
        public void OnCreate(World world) { }

        public void OnDestroy(World world) { }

        public void Update(World world, float deltaTime)
        {
            var transforms = world.GetStorage<TransformComponent>();
            var velocities = world.GetStorage<VelocityComponent>();

            foreach (var pair in transforms.All)
            {
                if (!velocities.TryGet(pair.Key, out var velocity))
                {
                    continue;
                }

                var transform = pair.Value;
                transform.Position += velocity.Value * deltaTime;
                transforms.Add(pair.Key, transform);
            }
        }
    }

    public sealed class ProjectileLifetimeSystem : ISystem
    {
        readonly List<uint> _toDestroy = new List<uint>(16);

        public void OnCreate(World world) { }

        public void OnDestroy(World world) { }

        public void Update(World world, float deltaTime)
        {
            _toDestroy.Clear();
            var projectiles = world.GetStorage<ProjectileComponent>();

            foreach (var pair in projectiles.All)
            {
                var projectile = pair.Value;
                projectile.RemainingLifetime -= deltaTime;
                projectiles.Add(pair.Key, projectile);

                if (projectile.RemainingLifetime <= 0f)
                {
                    _toDestroy.Add(pair.Key);
                }
            }

            for (var i = 0; i < _toDestroy.Count; i++)
            {
                world.DestroyEntity(_toDestroy[i]);
            }
        }
    }

    public sealed class ProjectileCollisionSystem : ISystem
    {
        readonly List<uint> _hits = new List<uint>(16);

        public void OnCreate(World world) { }

        public void OnDestroy(World world) { }

        public void Update(World world, float deltaTime)
        {
            if (world.UserData is not SpatialHashGrid grid || world.Commands == null)
            {
                return;
            }

            _hits.Clear();
            var projectiles = world.GetStorage<ProjectileComponent>();
            var transforms = world.GetStorage<TransformComponent>();
            var actors = world.GetStorage<ActorLinkComponent>();
            var combatStates = world.GetStorage<CombatStateComponent>();
            var teams = world.GetStorage<TeamComponent>();
            var collisions = world.GetStorage<CollisionComponent>();
            var commands = world.Commands;

            foreach (var projectilePair in projectiles.All)
            {
                if (!transforms.TryGet(projectilePair.Key, out var projectileTransform))
                {
                    continue;
                }

                var projectile = projectilePair.Value;
                var candidates = grid.QueryNearby(
                    projectileTransform.Position,
                    projectile.Radius + BattleConstants.DefaultActorCollisionRadius);

                for (var c = 0; c < candidates.Count; c++)
                {
                    var targetEntityId = candidates[c];
                    if (targetEntityId == projectilePair.Key)
                    {
                        continue;
                    }

                    if (!actors.TryGet(targetEntityId, out var actorLink))
                    {
                        continue;
                    }

                    if (!transforms.TryGet(targetEntityId, out var targetTransform))
                    {
                        continue;
                    }

                    if (!combatStates.TryGet(targetEntityId, out var combatState) || !combatState.IsAlive)
                    {
                        continue;
                    }

                    if (actorLink.ActorId == projectile.Owner)
                    {
                        continue;
                    }

                    if (teams.TryGet(projectilePair.Key, out var projectileTeam) &&
                        teams.TryGet(targetEntityId, out var targetTeam) &&
                        projectileTeam.TeamId == targetTeam.TeamId)
                    {
                        continue;
                    }

                    var targetRadius = collisions.TryGet(targetEntityId, out var collision)
                        ? collision.Radius
                        : BattleConstants.DefaultActorCollisionRadius;

                    var distance = Vector3.Distance(projectileTransform.Position, targetTransform.Position);
                    if (distance > projectile.Radius + targetRadius)
                    {
                        continue;
                    }

                    commands.EnqueueApplyDamage(new ApplyDamageCommand
                    {
                        Source = projectile.Owner,
                        Target = actorLink.ActorId,
                        Damage = projectile.Damage,
                        AbilityId = projectile.AbilityId
                    });

                    _hits.Add(projectilePair.Key);
                    break;
                }
            }

            for (var i = 0; i < _hits.Count; i++)
            {
                world.DestroyEntity(_hits[i]);
            }
        }
    }
}
