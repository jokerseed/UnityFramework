using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.ECS.Components;
using Framework.FixedMath;

namespace Framework.ECS.Systems
{
    /// <summary>
    /// 把 ECS 速度/击退写入 Farseer、步进物理、回写 Transform，并处理投射物命中。
    /// </summary>
    public sealed class PhysicsSimulationSystem : ISystem
    {
        readonly List<uint> _bodyIds = new List<uint>(64);
        readonly List<uint> _projectileIds = new List<uint>(32);
        readonly List<uint> _touching = new List<uint>(8);
        readonly List<uint> _expiredKnockback = new List<uint>(16);
        readonly List<uint> _hitProjectiles = new List<uint>(16);
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
            ResolveProjectileHits(world);
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
            _bodyIds.Clear();
            foreach (var pair in transforms.All)
            {
                _bodyIds.Add(pair.Key);
            }

            _bodyIds.Sort();
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

        void ResolveProjectileHits(World world)
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
            _projectileIds.Clear();
            foreach (var pair in projectiles.All)
            {
                _projectileIds.Add(pair.Key);
            }

            _projectileIds.Sort();
            _hitProjectiles.Clear();
            for (var i = 0; i < _projectileIds.Count; i++)
            {
                var entityId = _projectileIds[i];
                if (!projectiles.TryGet(entityId, out var projectile))
                {
                    continue;
                }

                _physics.CollectTouchingEntityIds(entityId, _touching);
                uint hitEntity = 0;
                var found = false;
                for (var t = 0; t < _touching.Count; t++)
                {
                    var otherId = _touching[t];
                    if (!actors.TryGet(otherId, out var link))
                    {
                        continue;
                    }

                    if (combat.TryGet(otherId, out var state) && !state.IsAlive)
                    {
                        continue;
                    }

                    if (link.ActorId == projectile.Owner)
                    {
                        continue;
                    }

                    if (teams.TryGet(otherId, out var team) && team.TeamId == projectile.TeamId)
                    {
                        continue;
                    }

                    hitEntity = otherId;
                    found = true;
                    break;
                }

                if (!found)
                {
                    continue;
                }

                var origin = transforms.TryGet(entityId, out var projectileTransform)
                    ? projectileTransform.Position
                    : TSVector.zero;
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
    }
}
