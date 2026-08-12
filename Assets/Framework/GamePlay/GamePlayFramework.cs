using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.Core.Tick;
using Framework.ECS;
using Framework.ECS.Systems;
using Framework.Events;
using Framework.GAS;
using Framework.GAS.Abilities;
using Framework.GAS.Cues;
using Framework.GAS.Events;
using Framework.GAS.Targeting;
using UnityEngine;

namespace Framework.GamePlay
{
    /// <summary>
    /// 玩法运行时主入口：编排 GAS 规则与 ECS 模拟。
    /// Tick 顺序：GAS Tick → Flush Spawn → ECS Tick → Flush Damage → 同步坐标
    /// </summary>
    public sealed class GamePlayFramework : ITickable, IDisposable
    {
        readonly ZeroGcEventBus _presentationBus = new ZeroGcEventBus();
        readonly BattleCommandBuffer _commandBuffer = new BattleCommandBuffer();
        readonly BattleContext _battleContext;
        readonly World _world;
        readonly ActorRegistry _registry;
        readonly BattleCommandProcessor _commandProcessor;
        readonly EventBusGameplayCueManager _cueManager;
        readonly List<ActorId> _targetScratch = new List<ActorId>();

        /// <summary>表现层事件总线，用于向外广播战斗事件（如伤害飘字、技能特效触发）。</summary>
        public IEventBus EventBus => _presentationBus;

        /// <summary>GameplayCue 管理器。</summary>
        public IGameplayCueManager CueManager => _cueManager;

        /// <summary>战斗命令缓冲，GAS 系统通过此缓冲向 ECS 提交延迟指令。</summary>
        public BattleCommandBuffer Commands => _commandBuffer;

        /// <summary>战斗上下文，包含命令缓冲与事件总线的统一引用，供 GAS 内部使用。</summary>
        public BattleContext Context => _battleContext;

        /// <summary>底层 ECS 世界实例，通常仅供高级调试或扩展使用。</summary>
        public World EcsWorld => _world;

        /// <summary>Actor 注册表，维护所有参战单位的 GAS 与 ECS 双侧数据。</summary>
        public ActorRegistry Registry => _registry;

        /// <summary>创建并初始化玩法框架，注册默认 ECS 系统。</summary>
        public GamePlayFramework()
        {
            _battleContext = new BattleContext(_commandBuffer, _presentationBus);
            _world = new World { Commands = _commandBuffer };
            _registry = new ActorRegistry(_world);
            _commandProcessor = new BattleCommandProcessor(_world, _registry);
            _cueManager = new EventBusGameplayCueManager(_presentationBus);

            _world.AddSystem(new MovementSystem());
            _world.AddSystem(new SpatialIndexSystem());
            _world.AddSystem(new ProjectileCollisionSystem());
            _world.AddSystem(new ProjectileLifetimeSystem());
        }

        /// <summary>
        /// 创建一个战斗 Actor，同时初始化其 GAS 属性并在 ECS 世界中注册对应实体。
        /// </summary>
        /// <param name="actorId">Actor 的唯一 ID；同一 ID 不可重复创建。</param>
        /// <param name="position">Actor 的初始世界坐标。</param>
        /// <param name="maxHealth">最大生命值，默认 100。</param>
        /// <param name="teamId">所属队伍编号，默认 0。</param>
        /// <returns>已初始化的 <see cref="AbilitySystemComponent"/>；可用于注册技能或读取属性。</returns>
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

        /// <summary>尝试获取指定 Actor 的 GAS 组件。</summary>
        /// <param name="actorId">目标 Actor ID。</param>
        /// <param name="asc">获取成功时输出对应的 <see cref="AbilitySystemComponent"/>；失败时为 <c>null</c>。</param>
        /// <returns>Actor 存在时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
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

        /// <summary>授予技能并返回 Spec 句柄。</summary>
        public GameplayAbilitySpecHandle GiveAbility(ActorId actorId, GameplayAbilityDef def, int level = 1, int inputId = -1)
        {
            if (!_registry.TryGet(actorId, out var actor))
            {
                throw new InvalidOperationException($"Cannot give ability: actor {actorId} not found.");
            }

            return actor.AbilitySystem.GiveAbility(def, level, inputId);
        }

        /// <summary>向指定 Actor 注册技能（兼容 API）。</summary>
        public void RegisterAbility(ActorId actorId, GameplayAbility ability) =>
            GiveAbility(actorId, new GameplayAbilityDef(ability));

        /// <summary>尝试激活技能（兼容 API）。</summary>
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

        /// <summary>按 Spec 句柄激活技能。</summary>
        public AbilityActivationResult TryActivateAbility(
            ActorId actorId,
            GameplayAbilitySpecHandle handle,
            in AbilityActivationContext context,
            out ActiveAbilityInstance instance)
        {
            instance = null;
            if (!_registry.TryGet(actorId, out var actor))
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.CustomBlocked);
            }

            return actor.AbilitySystem.TryActivateAbility(handle, context, _battleContext, out instance);
        }

        /// <summary>取消指定激活实例。</summary>
        public bool CancelAbility(ActorId actorId, int activeInstanceId)
        {
            if (!_registry.TryGet(actorId, out var actor))
            {
                return false;
            }

            return actor.AbilitySystem.CancelAbility(activeInstanceId, _presentationBus);
        }

        /// <summary>分发 GameplayEvent 到指定 Actor ASC。</summary>
        public bool HandleGameplayEvent(ActorId actorId, in GameplayEventData eventData)
        {
            if (!_registry.TryGet(actorId, out var actor))
            {
                return false;
            }

            return actor.AbilitySystem.HandleGameplayEvent(eventData, _battleContext);
        }

        /// <summary>半径内目标查询。</summary>
        public void QueryTargetsInRadius(
            ActorId source,
            Vector3 origin,
            float radius,
            TargetDataFilter filter,
            List<ActorId> results)
        {
            var f = new TargetDataFilter(source, filter.SourceTeamId, filter.EnemiesOnly, filter.MaxDistance, filter.RequiredTags);
            if (!_registry.TryGet(source, out var sourceActor))
            {
                results.Clear();
                return;
            }

            f = new TargetDataFilter(source, sourceActor.TeamId, filter.EnemiesOnly, filter.MaxDistance, filter.RequiredTags);
            _registry.QueryTargetsInRadius(origin, radius, f, results);
        }

        /// <summary>扇形目标查询。</summary>
        public void QueryTargetsInCone(
            ActorId source,
            Vector3 origin,
            Vector3 direction,
            float halfAngleDegrees,
            float range,
            TargetDataFilter filter,
            List<ActorId> results)
        {
            if (!_registry.TryGet(source, out var sourceActor))
            {
                results.Clear();
                return;
            }

            var f = new TargetDataFilter(source, sourceActor.TeamId, filter.EnemiesOnly, filter.MaxDistance, filter.RequiredTags);
            _registry.QueryTargetsInCone(origin, direction, halfAngleDegrees, range, f, results);
        }

        /// <summary>在指定范围内查询距 origin 最近的敌方 Actor ID。</summary>
        /// <param name="source">发起查询的 Actor ID；结果不包含自身及同队 Actor。</param>
        /// <param name="origin">查询中心的世界坐标。</param>
        /// <param name="range">查询范围半径（世界单位）。</param>
        /// <returns>范围内最近敌方的 <see cref="ActorId"/>；无有效目标时返回 <see cref="ActorId.Invalid"/>。</returns>
        public ActorId QueryNearestEnemy(ActorId source, Vector3 origin, float range) =>
            _registry.QueryNearestEnemy(source, origin, range);

        /// <summary>销毁指定 Actor，同时移除其 ECS 实体及注册表记录。</summary>
        /// <param name="actorId">要销毁的 Actor ID；若不存在则静默忽略。</param>
        public void DestroyActor(ActorId actorId) => _registry.Remove(actorId);

        /// <summary>
        /// 执行一帧战斗逻辑：GAS Tick → 刷新投射物生成 → ECS Tick → 刷新伤害 → 同步坐标。
        /// </summary>
        /// <param name="deltaTime">距上一帧的时间间隔（秒）。</param>
        public void Tick(float deltaTime)
        {
            var grid = _world.GetSingleton<SpatialHashGrid>();
            if (grid != null)
            {
                SpatialIndexService.RebuildActors(_world, grid);
            }

            foreach (var pair in _registry.Actors)
            {
                pair.Value.AbilitySystem.Tick(deltaTime, _presentationBus);
            }

            _commandProcessor.FlushSpawnCommands(_commandBuffer);
            _world.Tick(deltaTime);
            _commandProcessor.FlushDamageCommands(_commandBuffer, _presentationBus);
            _registry.SyncPositionsFromEcs();
        }

        /// <summary>释放玩法框架持有的全部资源，包括 ECS 世界、命令缓冲与事件总线。</summary>
        public void Dispose()
        {
            _world.Dispose();
            _commandBuffer.ClearAll();
            _presentationBus.Clear();
        }
    }
}
