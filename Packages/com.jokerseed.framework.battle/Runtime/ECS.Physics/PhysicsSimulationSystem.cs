using System.Collections.Generic;
using Framework.Battle;
using Framework.Battle.Commands;
using Framework.ECS.Components;
using Framework.FixedMath;

namespace Framework.ECS.Systems
{
    /// <summary>
    /// 把 ECS 速度/击退写入 Farseer、步进物理、回写 Transform，并处理投射物命中。
    /// 投射物命中同时看 Farseer 接触和本步扫过的线段（上一位置 = 当前位置 - 速度 × 步长），
    /// 高速投射物一步跨过目标也能命中。候选按沿飞行方向的距离取最近的一个，距离相同取实体 ID 小的。
    /// </summary>
    public sealed class PhysicsSimulationSystem : ISystem
    {
        readonly List<uint> _bodyIds = new List<uint>(64);
        readonly List<uint> _projectileIds = new List<uint>(32);
        readonly List<uint> _touching = new List<uint>(8);
        readonly List<uint> _expiredKnockback = new List<uint>(16);
        readonly List<uint> _hitProjectiles = new List<uint>(16);
        readonly List<uint> _actorIds = new List<uint>(64);
        BattlePhysicsWorld _physics;

        /// <inheritdoc/>
        public EcsSystemPhase Phase => EcsSystemPhase.Simulate;

        /// <inheritdoc/>
        public void OnCreate(World world)
        {
            _physics = world.GetSingleton<BattlePhysicsWorld>();
        }

        /// <inheritdoc/>
        public void OnDestroy(World world)
        {
            _physics?.Clear();
            _physics = null;
        }

        /// <summary>同步速度 → 物理步进 → 回写坐标 → 投射物命中入队。</summary>
        /// <param name="world">拥有该系统的 ECS 世界。</param>
        /// <param name="deltaTime">距上一帧的时间间隔（秒，定点）。</param>
        public void Update(World world, FP deltaTime)
        {
            if (_physics == null)
            {
                return;
            }

            PushVelocities(world, deltaTime);
            _physics.Step(deltaTime);
            PullTransforms(world);
            ResolveProjectileHits(world, deltaTime);
        }

        void PushVelocities(World world, FP deltaTime)
        {
            var velocities = world.GetStorage<VelocityComponent>();
            var knockbacks = world.GetStorage<KnockbackComponent>();
            var combat = world.GetStorage<CombatStateComponent>();
            _expiredKnockback.Clear();
            _bodyIds.Clear();
            foreach (var pair in velocities.All)
            {
                _bodyIds.Add(pair.Key);
            }

            _bodyIds.Sort();
            for (var i = 0; i < _bodyIds.Count; i++)
            {
                var entityId = _bodyIds[i];
                if (combat.TryGet(entityId, out var state) && !state.IsAlive)
                {
                    _physics.SetEnabled(entityId, false);
                    _physics.SetLinearVelocity(entityId, TSVector.zero);
                    continue;
                }

                _physics.SetEnabled(entityId, true);
                if (!velocities.TryGet(entityId, out var velocity))
                {
                    continue;
                }

                var linear = velocity.Value;
                if (knockbacks.TryGet(entityId, out var knockback))
                {
                    linear += knockback.Velocity;
                    knockback.Remaining -= deltaTime;
                    if (knockback.Remaining <= FP.Zero)
                    {
                        _expiredKnockback.Add(entityId);
                    }
                    else
                    {
                        knockbacks.Add(entityId, knockback);
                    }
                }

                _physics.SetLinearVelocity(entityId, linear);
            }

            for (var i = 0; i < _expiredKnockback.Count; i++)
            {
                knockbacks.Remove(_expiredKnockback[i]);
            }
        }

        void PullTransforms(World world)
        {
            var transforms = world.GetStorage<TransformComponent>();
            _physics.CopyBodyEntityIds(_bodyIds);
            for (var i = 0; i < _bodyIds.Count; i++)
            {
                var entityId = _bodyIds[i];
                if (!transforms.TryGet(entityId, out var transform))
                {
                    continue;
                }

                if (_physics.TryWriteTransform(entityId, ref transform))
                {
                    transforms.Add(entityId, transform);
                }
            }
        }

        void ResolveProjectileHits(World world, FP deltaTime)
        {
            if (world.Commands == null)
            {
                return;
            }

            var projectiles = world.GetStorage<ProjectileComponent>();
            var actors = world.GetStorage<ActorLinkComponent>();
            var combat = world.GetStorage<CombatStateComponent>();
            var teams = world.GetStorage<TeamComponent>();
            var transforms = world.GetStorage<TransformComponent>();
            var velocities = world.GetStorage<VelocityComponent>();
            var collisions = world.GetStorage<CollisionComponent>();
            _projectileIds.Clear();
            foreach (var pair in projectiles.All)
            {
                _projectileIds.Add(pair.Key);
            }

            _projectileIds.Sort();
            _actorIds.Clear();
            foreach (var pair in actors.All)
            {
                _actorIds.Add(pair.Key);
            }

            _actorIds.Sort();
            _hitProjectiles.Clear();
            for (var i = 0; i < _projectileIds.Count; i++)
            {
                var entityId = _projectileIds[i];
                if (!projectiles.TryGet(entityId, out var projectile))
                {
                    continue;
                }

                var hasTransform = transforms.TryGet(entityId, out var projectileTransform);
                var current = hasTransform ? projectileTransform.Position : TSVector.zero;
                var path = velocities.TryGet(entityId, out var velocity) ? velocity.Value * deltaTime : TSVector.zero;
                path.y = FP.Zero;
                var previous = current - path;
                var pathLengthSqr = path.x * path.x + path.z * path.z;
                var projectileRadius = projectile.Radius > FP.Zero
                    ? projectile.Radius
                    : BattleConstants.DefaultActorCollisionRadius;

                uint hitEntity = 0;
                var found = false;
                var bestKey = FP.Zero;
                var impact = current;

                _physics.CollectTouchingEntityIds(entityId, _touching);
                for (var t = 0; t < _touching.Count; t++)
                {
                    var otherId = _touching[t];
                    if (!IsValidTarget(otherId, projectile, actors, combat, teams))
                    {
                        continue;
                    }

                    var center = transforms.TryGet(otherId, out var otherTransform) ? otherTransform.Position : current;
                    ConsiderCandidate(otherId, center, previous, path, pathLengthSqr, current, ref found, ref hitEntity, ref bestKey, ref impact);
                }

                if (hasTransform && pathLengthSqr > FP.Zero)
                {
                    for (var a = 0; a < _actorIds.Count; a++)
                    {
                        var otherId = _actorIds[a];
                        if (_touching.Contains(otherId) ||
                            !transforms.TryGet(otherId, out var otherTransform) ||
                            !IsValidTarget(otherId, projectile, actors, combat, teams))
                        {
                            continue;
                        }

                        var actorRadius = collisions.TryGet(otherId, out var collision) && collision.Radius > FP.Zero
                            ? collision.Radius
                            : BattleConstants.DefaultActorCollisionRadius;
                        var reach = actorRadius + projectileRadius;
                        var center = otherTransform.Position;
                        var closest = ClosestPointOnPath(previous, path, pathLengthSqr, center);
                        var dx = center.x - closest.x;
                        var dz = center.z - closest.z;
                        if (dx * dx + dz * dz > reach * reach)
                        {
                            continue;
                        }

                        ConsiderCandidate(otherId, center, previous, path, pathLengthSqr, current, ref found, ref hitEntity, ref bestKey, ref impact);
                    }
                }

                if (!found)
                {
                    continue;
                }

                var origin = hasTransform ? impact : TSVector.zero;
                if (projectile.ExplodeRadius > FP.Zero)
                {
                    world.Commands.EnqueueApplyAreaEffect(new ApplyAreaEffectCommand
                    {
                        Source = projectile.Owner,
                        Origin = origin,
                        Radius = projectile.ExplodeRadius,
                        Damage = projectile.Damage,
                        AbilityId = projectile.AbilityId,
                        EffectId = projectile.HitEffectId,
                        TeamId = projectile.TeamId,
                        DamageType = projectile.DamageType
                    });
                }
                else if (actors.TryGet(hitEntity, out var hitLink))
                {
                    world.Commands.EnqueueApplyDamage(new ApplyDamageCommand
                    {
                        Source = projectile.Owner,
                        Target = hitLink.ActorId,
                        Damage = projectile.Damage,
                        AbilityId = projectile.AbilityId,
                        DamageType = projectile.DamageType
                    });

                    if (!string.IsNullOrEmpty(projectile.HitEffectId))
                    {
                        world.Commands.EnqueueApplyEffect(new ApplyEffectCommand
                        {
                            Source = projectile.Owner,
                            Target = hitLink.ActorId,
                            EffectId = projectile.HitEffectId
                        });
                    }
                }

                if (projectile.HitEntityIds == null)
                {
                    projectile.HitEntityIds = new List<uint>(4);
                }

                projectile.HitEntityIds.Add(actors.TryGet(hitEntity, out var hitActor) ? hitActor.ActorId.Value : hitEntity);
                if (projectile.PierceRemaining > 0)
                {
                    projectile.PierceRemaining--;
                    projectiles.Add(entityId, projectile);
                }
                else
                {
                    _hitProjectiles.Add(entityId);
                }
            }

            for (var i = 0; i < _hitProjectiles.Count; i++)
            {
                world.DestroyEntity(_hitProjectiles[i]);
            }
        }

        static bool IsValidTarget(
            uint otherId,
            in ProjectileComponent projectile,
            ComponentStorage<ActorLinkComponent> actors,
            ComponentStorage<CombatStateComponent> combat,
            ComponentStorage<TeamComponent> teams)
        {
            if (!actors.TryGet(otherId, out var link))
            {
                return false;
            }

            if (combat.TryGet(otherId, out var state) && !state.IsAlive)
            {
                return false;
            }

            if (link.ActorId == projectile.Owner)
            {
                return false;
            }

            if (teams.TryGet(otherId, out var team) && team.TeamId == projectile.TeamId)
            {
                return false;
            }

            return projectile.HitEntityIds == null || !projectile.HitEntityIds.Contains(link.ActorId.Value);
        }

        /// <summary>
        /// 候选排序键是目标中心在飞行方向上的投影（沿线越靠前越小）；投射物本步没动时用到当前位置的距离平方。
        /// 键相同取实体 ID 小的，保证各端选中同一个目标。
        /// </summary>
        static void ConsiderCandidate(
            uint otherId,
            TSVector center,
            TSVector previous,
            TSVector path,
            FP pathLengthSqr,
            TSVector current,
            ref bool found,
            ref uint hitEntity,
            ref FP bestKey,
            ref TSVector impact)
        {
            FP key;
            if (pathLengthSqr > FP.Zero)
            {
                key = (center.x - previous.x) * path.x + (center.z - previous.z) * path.z;
            }
            else
            {
                var dx = center.x - current.x;
                var dz = center.z - current.z;
                key = dx * dx + dz * dz;
            }

            if (found && (key > bestKey || (key == bestKey && otherId >= hitEntity)))
            {
                return;
            }

            found = true;
            hitEntity = otherId;
            bestKey = key;
            impact = pathLengthSqr > FP.Zero ? ClosestPointOnPath(previous, path, pathLengthSqr, center) : current;
        }

        /// <summary>XZ 平面上，线段 previous → previous + path 离 point 最近的点（Y 取 previous 的值）。</summary>
        static TSVector ClosestPointOnPath(TSVector previous, TSVector path, FP pathLengthSqr, TSVector point)
        {
            var t = ((point.x - previous.x) * path.x + (point.z - previous.z) * path.z) / pathLengthSqr;
            if (t < FP.Zero)
            {
                t = FP.Zero;
            }
            else if (t > FP.One)
            {
                t = FP.One;
            }

            return new TSVector(previous.x + path.x * t, previous.y, previous.z + path.z * t);
        }
    }
}
