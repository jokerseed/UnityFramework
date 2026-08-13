using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.ECS.Components;
using UnityEngine;

namespace Framework.ECS.Systems
{
    /// <summary>
    /// 空间索引系统，在 Movement 之后重建 Actor 空间索引，
    /// 供 <see cref="ProjectileCollisionSystem"/> 进行 broadphase 查询。
    /// </summary>
    public sealed class SpatialIndexSystem : ISystem
    {
        SpatialHashGrid _grid;

        /// <inheritdoc/>
        public EcsSystemPhase Phase => EcsSystemPhase.Simulate;

        /// <summary>系统创建时初始化 <see cref="SpatialHashGrid"/> 并注册为 World 单例。</summary>
        /// <param name="world">拥有该系统的 ECS 世界。</param>
        public void OnCreate(World world)
        {
            _grid = new SpatialHashGrid(BattleConstants.SpatialCellSize);
            world.RegisterSingleton(_grid);
        }

        /// <inheritdoc/>
        public void OnDestroy(World world) { }

        /// <summary>Movement 之后重建 Actor 空间索引。</summary>
        /// <param name="world">拥有该系统的 ECS 世界。</param>
        /// <param name="deltaTime">距上一帧的时间间隔（秒）；本系统未使用。</param>
        public void Update(World world, float deltaTime)
        {
            SpatialIndexService.RebuildActors(world, _grid);
        }
    }

    /// <summary>
    /// 移动系统，根据 <see cref="VelocityComponent"/> 每帧更新实体的 <see cref="TransformComponent.Position"/>。
    /// </summary>
    public sealed class MovementSystem : ISystem
    {
        /// <inheritdoc/>
        public EcsSystemPhase Phase => EcsSystemPhase.Simulate;

        /// <inheritdoc/>
        public void OnCreate(World world) { }

        /// <inheritdoc/>
        public void OnDestroy(World world) { }

        /// <summary>遍历所有具有速度组件的实体，按 deltaTime 更新其位置。</summary>
        /// <param name="world">拥有该系统的 ECS 世界。</param>
        /// <param name="deltaTime">距上一帧的时间间隔（秒）。</param>
        public void Update(World world, float deltaTime)
        {
            world.ForEach<VelocityComponent, TransformComponent>((entityId, velocity, transform) =>
            {
                transform.Position += velocity.Value * deltaTime;
                world.GetStorage<TransformComponent>().Add(entityId, transform);
            });
        }
    }

    /// <summary>
    /// 投射物生命周期系统，每帧递减 <see cref="ProjectileComponent.RemainingLifetime"/>，
    /// 到期时销毁对应实体。
    /// </summary>
    public sealed class ProjectileLifetimeSystem : ISystem
    {
        readonly List<uint> _ids = new List<uint>(32);
        readonly List<uint> _toDestroy = new List<uint>(16);

        /// <inheritdoc/>
        public EcsSystemPhase Phase => EcsSystemPhase.Simulate;

        /// <inheritdoc/>
        public void OnCreate(World world) { }

        /// <inheritdoc/>
        public void OnDestroy(World world) { }

        /// <summary>遍历所有投射物，递减剩余生命时间并销毁已到期的实体。</summary>
        /// <param name="world">拥有该系统的 ECS 世界。</param>
        /// <param name="deltaTime">距上一帧的时间间隔（秒）。</param>
        public void Update(World world, float deltaTime)
        {
            _toDestroy.Clear();
            _ids.Clear();
            var projectiles = world.GetStorage<ProjectileComponent>();

            // Unity/Mono 下 Dictionary 赋值会 bump version，不能边 foreach All 边 Add 同存储。
            foreach (var pair in projectiles.All)
            {
                _ids.Add(pair.Key);
            }

            for (var i = 0; i < _ids.Count; i++)
            {
                var entityId = _ids[i];
                if (!projectiles.TryGet(entityId, out var projectile))
                {
                    continue;
                }

                projectile.RemainingLifetime -= deltaTime;
                projectiles.Add(entityId, projectile);

                if (projectile.RemainingLifetime <= 0f)
                {
                    _toDestroy.Add(entityId);
                }
            }

            for (var i = 0; i < _toDestroy.Count; i++)
            {
                world.DestroyEntity(_toDestroy[i]);
            }
        }
    }

    /// <summary>
    /// 投射物碰撞系统，利用 <see cref="SpatialHashGrid"/> 对投射物与 Actor 进行近邻检测，
    /// 命中时向 <see cref="World.Commands"/> 写入 <see cref="ApplyDamageCommand"/> 并销毁投射物。
    /// </summary>
    public sealed class ProjectileCollisionSystem : ISystem
    {
        readonly List<uint> _hits = new List<uint>(16);

        /// <inheritdoc/>
        public EcsSystemPhase Phase => EcsSystemPhase.Simulate;

        /// <inheritdoc/>
        public void OnCreate(World world) { }

        /// <inheritdoc/>
        public void OnDestroy(World world) { }

        /// <summary>
        /// 每帧检测所有投射物与存活 Actor 之间的碰撞；
        /// 命中（非友方、非自身、距离在半径之内）时入队伤害指令并摧毁投射物。
        /// </summary>
        /// <param name="world">拥有该系统的 ECS 世界。</param>
        /// <param name="deltaTime">距上一帧的时间间隔（秒）；本系统未使用。</param>
        public void Update(World world, float deltaTime)
        {
            var grid = world.GetSingleton<SpatialHashGrid>();
            if (grid == null || world.Commands == null)
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
