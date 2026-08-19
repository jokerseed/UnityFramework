using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.ECS.Components;
using Framework.FixedMath;

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
        /// <param name="deltaTime">距上一帧的时间间隔（秒，定点）；本系统未使用。</param>
        public void Update(World world, FP deltaTime)
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
        /// <param name="deltaTime">距上一帧的时间间隔（秒，定点）。</param>
        public void Update(World world, FP deltaTime)
        {
            world.ForEach<VelocityComponent, TransformComponent>((entityId, velocity, transform) =>
            {
                transform.Position += velocity.Value * deltaTime;
                world.GetStorage<TransformComponent>().Add(entityId, transform);
            });
        }
    }

    /// <summary>击退冲量系统：在位移之后叠加击退速度，到期移除组件。</summary>
    public sealed class KnockbackSystem : ISystem
    {
        readonly List<uint> _scratch = new List<uint>(16);
        readonly List<uint> _expired = new List<uint>(16);

        /// <inheritdoc/>
        public EcsSystemPhase Phase => EcsSystemPhase.Simulate;

        /// <inheritdoc/>
        public void OnCreate(World world) { }

        /// <inheritdoc/>
        public void OnDestroy(World world) { }

        /// <inheritdoc/>
        public void Update(World world, FP deltaTime)
        {
            _scratch.Clear();
            _expired.Clear();
            var knockbacks = world.GetStorage<KnockbackComponent>();
            var transforms = world.GetStorage<TransformComponent>();
            foreach (var pair in knockbacks.All)
            {
                _scratch.Add(pair.Key);
            }

            for (var i = 0; i < _scratch.Count; i++)
            {
                var entityId = _scratch[i];
                if (!knockbacks.TryGet(entityId, out var knockback))
                {
                    continue;
                }

                if (!transforms.TryGet(entityId, out var transform))
                {
                    _expired.Add(entityId);
                    continue;
                }

                transform.Position += knockback.Velocity * deltaTime;
                knockback.Remaining -= deltaTime;
                transforms.Add(entityId, transform);
                if (knockback.Remaining <= FP.Zero)
                {
                    _expired.Add(entityId);
                }
                else
                {
                    knockbacks.Add(entityId, knockback);
                }
            }

            for (var i = 0; i < _expired.Count; i++)
            {
                knockbacks.Remove(_expired[i]);
            }
        }
    }

    /// <summary>存活 Actor 圆形挤开，避免割草时叠在同一点。</summary>
    public sealed class ActorSeparationSystem : ISystem
    {
        readonly List<uint> _ids = new List<uint>(64);
        readonly List<TSVector> _positions = new List<TSVector>(64);
        readonly List<FP> _radii = new List<FP>(64);
        readonly List<TSVector> _pushes = new List<TSVector>(64);

        /// <inheritdoc/>
        public EcsSystemPhase Phase => EcsSystemPhase.Simulate;

        /// <inheritdoc/>
        public void OnCreate(World world) { }

        /// <inheritdoc/>
        public void OnDestroy(World world) { }

        /// <inheritdoc/>
        public void Update(World world, FP deltaTime)
        {
            _ids.Clear();
            _positions.Clear();
            _radii.Clear();
            _pushes.Clear();

            var defaultRadius = BattleConstants.DefaultActorCollisionRadius;
            var combat = world.GetStorage<CombatStateComponent>();
            world.ForEach<ActorLinkComponent, TransformComponent, CollisionComponent>(
                (entityId, _, transform, collision) =>
                {
                    if (combat.TryGet(entityId, out var state) && !state.IsAlive)
                    {
                        return;
                    }

                    _ids.Add(entityId);
                    _positions.Add(transform.Position);
                    _radii.Add(collision.Radius > FP.Zero ? collision.Radius : defaultRadius);
                    _pushes.Add(TSVector.zero);
                });

            for (var i = 0; i < _ids.Count; i++)
            {
                for (var j = i + 1; j < _ids.Count; j++)
                {
                    var delta = _positions[i] - _positions[j];
                    delta.y = FP.Zero;
                    var minDist = _radii[i] + _radii[j];
                    var distSq = delta.sqrMagnitude;
                    if (distSq >= minDist * minDist)
                    {
                        continue;
                    }

                    TSVector axis;
                    FP overlap;
                    if (distSq < FP.EN4)
                    {
                        var bucket = (int)(((uint)i * 2654435761u ^ (uint)j * 340573321u) % 628u);
                        var angle = (FP)bucket / 100;
                        axis = new TSVector(TSMath.Cos(angle), 0, TSMath.Sin(angle));
                        overlap = minDist / 2;
                    }
                    else
                    {
                        var dist = TSMath.Sqrt(distSq);
                        overlap = (minDist - dist) / 2;
                        axis = delta / dist;
                    }

                    _pushes[i] += axis * overlap;
                    _pushes[j] -= axis * overlap;
                }
            }

            var transforms = world.GetStorage<TransformComponent>();
            for (var i = 0; i < _ids.Count; i++)
            {
                if (_pushes[i].sqrMagnitude < FP.EN4 || !transforms.TryGet(_ids[i], out var transform))
                {
                    continue;
                }

                transform.Position += _pushes[i];
                transforms.Add(_ids[i], transform);
            }
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
        /// <param name="deltaTime">距上一帧的时间间隔（秒，定点）。</param>
        public void Update(World world, FP deltaTime)
        {
            var dt = deltaTime;
            _toDestroy.Clear();
            _ids.Clear();
            var projectiles = world.GetStorage<ProjectileComponent>();

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

                projectile.RemainingLifetime -= dt;
                projectiles.Add(entityId, projectile);

                if (projectile.RemainingLifetime <= FP.Zero)
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
        /// <param name="deltaTime">距上一帧的时间间隔（秒，定点）；本系统未使用。</param>
        public void Update(World world, FP deltaTime)
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
            var defaultRadius = BattleConstants.DefaultActorCollisionRadius;

            foreach (var projectilePair in projectiles.All)
            {
                if (!transforms.TryGet(projectilePair.Key, out var projectileTransform))
                {
                    continue;
                }

                var projectile = projectilePair.Value;
                var candidates = grid.QueryNearby(
                    projectileTransform.Position,
                    projectile.Radius + defaultRadius);

                var hitThisFrame = false;
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
                        : defaultRadius;

                    var distance = TSVector.Distance(projectileTransform.Position, targetTransform.Position);
                    if (distance > projectile.Radius + targetRadius)
                    {
                        continue;
                    }

                    if (projectile.ExplodeRadius > FP.Zero)
                    {
                        commands.EnqueueApplyAreaEffect(new ApplyAreaEffectCommand
                        {
                            Source = projectile.Owner,
                            Origin = projectileTransform.Position,
                            Radius = projectile.ExplodeRadius,
                            Damage = projectile.Damage,
                            AbilityId = projectile.AbilityId,
                            EffectId = projectile.HitEffectId,
                            TeamId = projectile.TeamId,
                            DamageType = projectile.DamageType
                        });
                    }
                    else
                    {
                        commands.EnqueueApplyDamage(new ApplyDamageCommand
                        {
                            Source = projectile.Owner,
                            Target = actorLink.ActorId,
                            Damage = projectile.Damage,
                            AbilityId = projectile.AbilityId,
                            DamageType = projectile.DamageType
                        });

                        if (!string.IsNullOrEmpty(projectile.HitEffectId))
                        {
                            commands.EnqueueApplyEffect(new ApplyEffectCommand
                            {
                                Source = projectile.Owner,
                                Target = actorLink.ActorId,
                                EffectId = projectile.HitEffectId
                            });
                        }
                    }

                    hitThisFrame = true;
                    if (projectile.PierceRemaining > 0)
                    {
                        projectile.PierceRemaining--;
                        projectiles.Add(projectilePair.Key, projectile);
                    }
                    else
                    {
                        _hits.Add(projectilePair.Key);
                    }

                    break;
                }

                if (!hitThisFrame)
                {
                    continue;
                }
            }

            for (var i = 0; i < _hits.Count; i++)
            {
                world.DestroyEntity(_hits[i]);
            }
        }
    }
}
