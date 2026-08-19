using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.ECS;
using Framework.ECS.Components;
using Framework.GAS;
using Framework.GAS.Tags;
using Framework.GAS.Targeting;
using Framework.FixedMath;
using UnityEngine;

namespace Framework.GamePlay
{
    /// <summary>
    /// 战斗 Actor 的运行时数据容器，聚合 Actor ID、GAS 组件、ECS 实体及队伍信息。
    /// 由 <see cref="ActorRegistry"/> 创建和管理。
    /// </summary>
    public sealed class BattleActor
    {
        /// <summary>Actor 的全局唯一 ID。</summary>
        public ActorId ActorId { get; }

        /// <summary>该 Actor 的 GAS 能力系统组件，负责属性、技能与标签管理。</summary>
        public AbilitySystemComponent AbilitySystem { get; }

        /// <summary>该 Actor 在 ECS 世界中对应的实体；由 <see cref="ActorRegistry"/> 创建后写入。</summary>
        public Entity EcsEntity { get; internal set; }

        /// <summary>Actor 所属队伍编号；同队视为友方，不同队视为敌方。</summary>
        public int TeamId { get; }

        /// <summary>Actor 在世界空间的当前位置（表现缓存），由 <see cref="ActorRegistry.SyncPositionsFromEcs"/> 每帧更新。</summary>
        public Vector3 Position { get; internal set; }

        /// <summary>Actor 仿真世界坐标（定点），逻辑链统一读取此字段。</summary>
        public TSVector SimPosition { get; internal set; }

        /// <summary>创建一个 BattleActor 数据容器。</summary>
        /// <param name="actorId">Actor 唯一 ID。</param>
        /// <param name="abilitySystem">已初始化的 GAS 组件；不可为 null。</param>
        /// <param name="teamId">所属队伍编号。</param>
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

        /// <summary>创建 Actor 注册表。</summary>
        /// <param name="world">关联的 ECS 世界，用于创建和销毁实体。</param>
        public ActorRegistry(World world)
        {
            _world = world;
        }

        /// <summary>当前所有已注册 Actor 的只读字典，键为 <see cref="ActorId"/>，值为 <see cref="BattleActor"/>。</summary>
        public IReadOnlyDictionary<ActorId, BattleActor> Actors => _actors;

        /// <summary>
        /// 创建一个新 Actor：初始化 <see cref="BattleActor"/> 并在 ECS 世界中生成对应实体与组件。
        /// </summary>
        /// <param name="actorId">Actor 唯一 ID；同一 ID 不可重复注册。</param>
        /// <param name="position">Actor 的初始世界坐标。</param>
        /// <param name="maxHealth">最大生命值，用于初始化 GAS 属性。</param>
        /// <param name="teamId">所属队伍编号。</param>
        /// <param name="asc">已初始化的 GAS 组件；不可为 null。</param>
        /// <returns>创建成功的 <see cref="BattleActor"/> 实例。</returns>
        /// <exception cref="InvalidOperationException">指定 <paramref name="actorId"/> 已存在时抛出。</exception>
        public BattleActor Create(
            ActorId actorId,
            TSVector position,
            FP maxHealth,
            int teamId,
            AbilitySystemComponent asc)
        {
            if (_actors.ContainsKey(actorId))
            {
                throw new InvalidOperationException($"Actor {actorId} already exists.");
            }

            var actor = new BattleActor(actorId, asc, teamId)
            {
                Position = FPConversions.ToVector3(position),
                SimPosition = position
            };
            var entity = _world.CreateEntity();
            actor.EcsEntity = entity;

            _world.AddComponent(entity, new TransformComponent
            {
                Position = position,
                Forward = TSVector.forward
            });
            _world.AddComponent(entity, new ActorLinkComponent { ActorId = actorId });
            _world.AddComponent(entity, new CombatStateComponent { IsAlive = true });
            _world.AddComponent(entity, new TeamComponent { TeamId = teamId });
            _world.AddComponent(entity, new CollisionComponent { Radius = BattleConstants.DefaultActorCollisionRadius });
            _world.AddComponent(entity, new VelocityComponent { Value = TSVector.zero });

            _world.GetSingleton<BattlePhysicsWorld>()?.AddActorBody(
                entity.Id,
                actorId,
                teamId,
                position,
                BattleConstants.DefaultActorCollisionRadius);

            _actors[actorId] = actor;
            return actor;
        }

        /// <summary>尝试按 ID 获取 Actor。</summary>
        /// <param name="actorId">目标 Actor ID。</param>
        /// <param name="actor">获取成功时输出对应的 <see cref="BattleActor"/>；失败时为 <c>null</c>。</param>
        /// <returns>Actor 存在时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryGet(ActorId actorId, out BattleActor actor) => _actors.TryGetValue(actorId, out actor);

        /// <summary>尝试获取 Actor 对应的 ECS 实体。</summary>
        /// <param name="actorId">目标 Actor ID。</param>
        /// <param name="entity">获取成功时输出 ECS 实体；失败时为 <c>null</c>。</param>
        /// <returns>Actor 存在且已绑定 ECS 实体时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
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

        /// <summary>将指定 Actor 的 ECS 战斗状态标记为死亡（<see cref="CombatStateComponent.IsAlive"/> = false）。</summary>
        /// <param name="actorId">目标 Actor ID；若 Actor 或其 ECS 实体不存在则静默忽略。</param>
        public void MarkDead(ActorId actorId)
        {
            SetCombatAlive(actorId, false);
        }

        /// <summary>将指定 Actor 的 ECS 战斗状态标记为存活。</summary>
        /// <param name="actorId">目标 Actor ID；若 Actor 或其 ECS 实体不存在则静默忽略。</param>
        public void MarkAlive(ActorId actorId) => SetCombatAlive(actorId, true);

        void SetCombatAlive(ActorId actorId, bool isAlive)
        {
            if (!TryGetEntity(actorId, out var entity))
            {
                return;
            }

            _world.AddComponent(entity, new CombatStateComponent { IsAlive = isAlive });
            _world.GetSingleton<BattlePhysicsWorld>()?.SetEnabled(entity.Id, isAlive);
        }

        /// <summary>写入 Actor 世界坐标（同时更新 ECS、<see cref="BattleActor.SimPosition"/> 与表现缓存）。</summary>
        /// <param name="actorId">目标 Actor。</param>
        /// <param name="position">世界坐标（定点）。</param>
        public void SetPosition(ActorId actorId, TSVector position)
        {
            if (!_actors.TryGetValue(actorId, out var actor) || !TryGetEntity(actorId, out var entity))
            {
                return;
            }

            if (!_world.TryGetComponent(entity, out TransformComponent transform))
            {
                transform = new TransformComponent { Forward = TSVector.forward };
            }

            transform.Position = position;
            _world.AddComponent(entity, transform);
            actor.SimPosition = position;
            actor.Position = FPConversions.ToVector3(position);
            _world.GetSingleton<BattlePhysicsWorld>()?.SetPosition(entity.Id, position);
        }

        /// <summary>尝试读取 Actor 仿真坐标。</summary>
        /// <param name="actorId">目标 Actor。</param>
        /// <param name="position">输出仿真坐标。</param>
        /// <returns>Actor 存在时返回 true。</returns>
        public bool TryGetSimPosition(ActorId actorId, out TSVector position)
        {
            if (_actors.TryGetValue(actorId, out var actor))
            {
                position = actor.SimPosition;
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>移除击退冲量。</summary>
        /// <param name="actorId">目标 Actor。</param>
        public void ClearKnockback(ActorId actorId)
        {
            if (!TryGetEntity(actorId, out var entity))
            {
                return;
            }

            _world.GetStorage<KnockbackComponent>().Remove(entity.Id);
        }

        /// <summary>在指定范围内查询距 origin 最近的异队存活 Actor ID。</summary>
        /// <param name="source">发起查询的 Actor ID；结果排除自身及同队 Actor。</param>
        /// <param name="origin">查询中心的世界坐标。</param>
        /// <param name="range">查询范围半径（世界单位）；超出范围的 Actor 不会被返回。</param>
        /// <returns>范围内最近敌方的 <see cref="ActorId"/>；无有效目标时返回 <see cref="ActorId.Invalid"/>。</returns>
        public ActorId QueryNearestEnemy(ActorId source, TSVector origin, FP range)
        {
            if (!_actors.TryGetValue(source, out var sourceActor))
            {
                return ActorId.Invalid;
            }

            var grid = _world.GetSingleton<SpatialHashGrid>();
            if (grid == null)
            {
                return ActorId.Invalid;
            }

            ActorId best = ActorId.Invalid;
            var bestDistance = FP.MaxValue;
            var candidates = grid.QueryNearby(origin, range);

            for (var i = 0; i < candidates.Count; i++)
            {
                if (!TryResolveActor(candidates[i], out var target, out var position))
                {
                    continue;
                }

                if (target.TeamId == sourceActor.TeamId || target.ActorId == source)
                {
                    continue;
                }

                if (target.AbilitySystem.Tags.HasTag(new GAS.Tags.GameplayTag(BattleConstants.TagDead)))
                {
                    continue;
                }

                var distance = TSVector.Distance(origin, position);
                if (distance > range || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = target.ActorId;
            }

            return best;
        }

        /// <summary>在半径内查询符合筛选条件的 Actor 列表。</summary>
        /// <param name="origin">圆心。</param>
        /// <param name="radius">半径。</param>
        /// <param name="filter">筛选条件。</param>
        /// <param name="results">输出列表（调用前清空）。</param>
        public void QueryTargetsInRadius(TSVector origin, FP radius, GAS.Targeting.TargetDataFilter filter, List<ActorId> results)
        {
            results.Clear();
            if (!_actors.TryGetValue(filter.Source, out var sourceActor))
            {
                return;
            }

            var grid = _world.GetSingleton<SpatialHashGrid>();
            if (grid == null)
            {
                return;
            }

            var candidates = grid.QueryNearby(origin, radius);
            for (var i = 0; i < candidates.Count; i++)
            {
                if (!TryResolveActor(candidates[i], out var target, out var position))
                {
                    continue;
                }

                if (target.ActorId == filter.Source)
                {
                    continue;
                }

                if (filter.EnemiesOnly && target.TeamId == sourceActor.TeamId)
                {
                    continue;
                }

                if (target.AbilitySystem.Tags.HasTag(new GAS.Tags.GameplayTag(BattleConstants.TagDead)))
                {
                    continue;
                }

                var distance = TSVector.Distance(origin, position);
                if (distance > radius)
                {
                    continue;
                }

                if (filter.MaxDistance > FP.Zero && distance > filter.MaxDistance)
                {
                    continue;
                }

                if (!MatchesRequiredTags(target, filter))
                {
                    continue;
                }

                results.Add(target.ActorId);
            }
        }

        /// <summary>在扇形范围内查询符合筛选条件的 Actor 列表。</summary>
        public void QueryTargetsInCone(
            TSVector origin,
            TSVector direction,
            FP halfAngleDegrees,
            FP range,
            GAS.Targeting.TargetDataFilter filter,
            List<ActorId> results)
        {
            results.Clear();
            if (!_actors.TryGetValue(filter.Source, out var sourceActor))
            {
                return;
            }

            var grid = _world.GetSingleton<SpatialHashGrid>();
            if (grid == null)
            {
                return;
            }

            var forward = direction;
            forward.y = FP.Zero;
            if (forward.sqrMagnitude <= FP.Zero)
            {
                forward = TSVector.forward;
            }
            else
            {
                forward.Normalize();
            }

            var candidates = grid.QueryNearby(origin, range);
            var minDist = FP.EN3;

            for (var i = 0; i < candidates.Count; i++)
            {
                if (!TryResolveActor(candidates[i], out var target, out var position))
                {
                    continue;
                }

                if (target.ActorId == filter.Source)
                {
                    continue;
                }

                if (filter.EnemiesOnly && target.TeamId == sourceActor.TeamId)
                {
                    continue;
                }

                if (target.AbilitySystem.Tags.HasTag(new GAS.Tags.GameplayTag(BattleConstants.TagDead)))
                {
                    continue;
                }

                var toTarget = position - origin;
                toTarget.y = FP.Zero;
                var distance = toTarget.magnitude;
                if (distance > range || distance <= minDist)
                {
                    continue;
                }

                var angle = TSVector.Angle(forward, toTarget);
                if (angle > halfAngleDegrees)
                {
                    continue;
                }

                if (!MatchesRequiredTags(target, filter))
                {
                    continue;
                }

                results.Add(target.ActorId);
            }
        }

        /// <summary>从 ECS 实体 ID 解析 <see cref="BattleActor"/> 与世界坐标。</summary>
        bool TryResolveActor(uint entityId, out BattleActor actor, out TSVector position)
        {
            actor = null;
            position = default;

            if (!_world.TryGetComponent(entityId, out ActorLinkComponent link))
            {
                return false;
            }

            if (!_actors.TryGetValue(link.ActorId, out actor))
            {
                return false;
            }

            if (_world.TryGetComponent(entityId, out TransformComponent transform))
            {
                position = transform.Position;
            }
            else
            {
                position = actor.SimPosition;
            }

            return true;
        }

        static bool MatchesRequiredTags(BattleActor target, TargetDataFilter filter)
        {
            if (filter.RequiredTags == null || filter.RequiredTags.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < filter.RequiredTags.Count; i++)
            {
                if (target.AbilitySystem.Tags.HasTag(new GameplayTag(filter.RequiredTags[i])))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>设置 Actor 的 ECS 速度；定身时由调用方传零向量。</summary>
        /// <param name="actorId">目标 Actor。</param>
        /// <param name="velocity">世界空间速度（定点）。</param>
        public void SetVelocity(ActorId actorId, TSVector velocity)
        {
            if (!TryGetEntity(actorId, out var entity))
            {
                return;
            }

            _world.AddComponent(entity, new VelocityComponent { Value = velocity });
        }

        /// <summary>对 Actor 施加击退冲量（写入 <see cref="KnockbackComponent"/>）。</summary>
        /// <param name="actorId">目标 Actor。</param>
        /// <param name="offset">期望位移向量（米）。</param>
        /// <param name="duration">冲量持续秒数；≤0 时使用默认时长。</param>
        public void ApplyKnockback(ActorId actorId, TSVector offset, FP duration = default)
        {
            if (!TryGetEntity(actorId, out var entity) || offset.sqrMagnitude < FP.EN4)
            {
                return;
            }

            if (duration <= FP.Zero)
            {
                duration = BattleConstants.DefaultKnockbackDuration;
            }

            var add = offset / duration;
            if (_world.TryGetComponent(entity, out KnockbackComponent existing))
            {
                existing.Velocity += add;
                existing.Remaining = TSMath.Max(existing.Remaining, duration);
                _world.AddComponent(entity, existing);
                return;
            }

            _world.AddComponent(entity, new KnockbackComponent
            {
                Velocity = add,
                Remaining = duration
            });
        }

        /// <summary>对 Actor 施加世界空间位移（击退）。</summary>
        /// <param name="actorId">目标 Actor。</param>
        /// <param name="offset">位移向量。</param>
        public void ApplyDisplacement(ActorId actorId, TSVector offset)
        {
            ApplyKnockback(actorId, offset);
        }

        /// <summary>读取 Actor 仿真朝向；无 Transform 时返回 <see cref="TSVector.forward"/>。</summary>
        /// <param name="actorId">目标 Actor。</param>
        /// <returns>朝向向量。</returns>
        public TSVector GetSimForward(ActorId actorId)
        {
            if (!TryGetEntity(actorId, out var entity) ||
                !_world.TryGetComponent(entity, out TransformComponent transform))
            {
                return TSVector.forward;
            }

            return transform.Forward.sqrMagnitude > FP.Zero
                ? transform.Forward
                : TSVector.forward;
        }

        /// <summary>读取 Actor 朝向（表现）；无 Transform 时返回 <see cref="Vector3.forward"/>。</summary>
        /// <param name="actorId">目标 Actor。</param>
        /// <returns>朝向向量。</returns>
        public Vector3 GetForward(ActorId actorId)
        {
            if (!TryGetEntity(actorId, out var entity) ||
                !_world.TryGetComponent(entity, out TransformComponent transform))
            {
                return Vector3.forward;
            }

            return transform.Forward.sqrMagnitude > FP.Zero
                ? transform.ToUnityForward()
                : Vector3.forward;
        }

        /// <summary>写入 Actor 朝向（定点）。</summary>
        /// <param name="actorId">目标 Actor。</param>
        /// <param name="forward">朝向。</param>
        public void SetForward(ActorId actorId, TSVector forward)
        {
            if (!TryGetEntity(actorId, out var entity) ||
                !_world.TryGetComponent(entity, out TransformComponent transform))
            {
                return;
            }

            var dir = forward;
            dir.y = FP.Zero;
            transform.Forward = dir.sqrMagnitude > FP.Zero
                ? dir.normalized
                : TSVector.forward;
            _world.AddComponent(entity, transform);
        }

        /// <summary>将所有 Actor 的位置从 ECS <see cref="TransformComponent"/> 同步到 <see cref="BattleActor.SimPosition"/> 与表现缓存，每帧 Tick 末尾调用。</summary>
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
                    actor.SimPosition = transform.Position;
                    actor.Position = transform.ToUnityPosition();
                }
            }
        }

        /// <summary>移除指定 Actor：销毁其 ECS 实体并从注册表中删除记录。</summary>
        /// <param name="actorId">要移除的 Actor ID；若不存在则静默忽略。</param>
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
