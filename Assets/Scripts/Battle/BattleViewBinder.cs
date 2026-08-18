using System.Collections.Generic;
using Framework.ECS.Components;
using Framework.GamePlay;
using Framework.Core;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 战斗表现层绑定：同步 Actor 与投射物的 GameObject 状态。
    /// </summary>
    public sealed class BattleViewBinder
    {
        sealed class ActorRenderState
        {
            public Vector3 PreviousPosition;
            public Vector3 CurrentPosition;
            public Quaternion PreviousRotation;
            public Quaternion CurrentRotation;
            public bool Initialized;
        }

        const float FireballBallMinScale = 0.35f;

        GameObject _heroInstance;
        readonly List<BattleMonsterView> _monsterViews = new List<BattleMonsterView>(12);
        readonly Dictionary<uint, ActorRenderState> _actorRenderStates = new Dictionary<uint, ActorRenderState>(16);
        readonly Dictionary<uint, GameObject> _projectileViews = new Dictionary<uint, GameObject>(16);
        readonly List<uint> _projectileViewRemoveScratch = new List<uint>(16);
        readonly HashSet<uint> _aliveProjectileIds = new HashSet<uint>();

        /// <summary>英雄与杂兵 View 是否就绪。</summary>
        public bool ViewsReady => _heroInstance != null && _monsterViews.Count > 0;

        /// <summary>注册英雄实例。</summary>
        /// <param name="hero">英雄 GameObject。</param>
        public void RegisterHero(GameObject hero) => _heroInstance = hero;

        /// <summary>注册杂兵视图列表。</summary>
        /// <param name="views">杂兵视图。</param>
        public void RegisterMonsters(IReadOnlyList<BattleMonsterView> views)
        {
            _monsterViews.Clear();
            _monsterViews.AddRange(views);
        }

        /// <summary>同步 Actor 与投射物表现。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="heroId">英雄 ActorId。</param>
        /// <param name="monsterIds">杂兵 ActorId 列表。</param>
        /// <param name="interpolationAlpha">逻辑帧到渲染帧的插值系数。</param>
        public void Sync(GamePlayFramework framework, ActorId heroId, IReadOnlyList<ActorId> monsterIds, float interpolationAlpha)
        {
            SyncActors(framework, heroId, monsterIds, interpolationAlpha);
            SyncProjectiles(framework);
        }

        /// <summary>销毁所有表现层 GameObject 并清空引用。</summary>
        public void Clear()
        {
            ClearProjectileViews();

            if (_heroInstance != null)
            {
                Object.Destroy(_heroInstance);
                _heroInstance = null;
            }

            _monsterViews.Clear();
            _actorRenderStates.Clear();
        }

        void SyncActors(
            GamePlayFramework framework,
            ActorId heroId,
            IReadOnlyList<ActorId> monsterIds,
            float interpolationAlpha)
        {
            SyncOne(framework, _heroInstance, heroId, interpolationAlpha);
            var count = Mathf.Min(_monsterViews.Count, monsterIds.Count);
            for (var i = 0; i < count; i++)
            {
                SyncOne(framework, _monsterViews[i].View, monsterIds[i], interpolationAlpha);
            }
        }

        void SyncOne(GamePlayFramework framework, GameObject instance, ActorId actorId, float interpolationAlpha)
        {
            if (instance == null || !framework.Registry.TryGet(actorId, out var actor))
            {
                return;
            }

            var dead = actor.AbilitySystem.IsDead;
            if (instance.activeSelf == dead)
            {
                instance.SetActive(!dead);
            }

            if (dead)
            {
                return;
            }

            var forward = framework.Registry.GetForward(actorId);
            forward.y = 0f;
            var rotation = forward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(forward) * BattleSetup.FaceCamera
                : BattleSetup.FaceCamera;

            if (!_actorRenderStates.TryGetValue(actorId.Value, out var renderState))
            {
                renderState = new ActorRenderState();
                _actorRenderStates[actorId.Value] = renderState;
            }

            if (!renderState.Initialized)
            {
                renderState.Initialized = true;
                renderState.PreviousPosition = actor.Position;
                renderState.CurrentPosition = actor.Position;
                renderState.PreviousRotation = rotation;
                renderState.CurrentRotation = rotation;
            }
            else if (renderState.CurrentPosition != actor.Position || renderState.CurrentRotation != rotation)
            {
                renderState.PreviousPosition = renderState.CurrentPosition;
                renderState.CurrentPosition = actor.Position;
                renderState.PreviousRotation = renderState.CurrentRotation;
                renderState.CurrentRotation = rotation;
            }

            var renderPosition = Vector3.Lerp(renderState.PreviousPosition, renderState.CurrentPosition, interpolationAlpha);
            var renderRotation = Quaternion.Slerp(renderState.PreviousRotation, renderState.CurrentRotation, interpolationAlpha);
            instance.transform.SetPositionAndRotation(renderPosition, renderRotation);
        }

        void SyncProjectiles(GamePlayFramework framework)
        {
            var world = framework.EcsWorld;
            var projectiles = world.GetStorage<ProjectileComponent>();
            var transforms = world.GetStorage<TransformComponent>();

            _aliveProjectileIds.Clear();
            foreach (var pair in projectiles.All)
            {
                var entityId = pair.Key;
                if (!transforms.TryGet(entityId, out var transform))
                {
                    continue;
                }

                _aliveProjectileIds.Add(entityId);
                if (!_projectileViews.TryGetValue(entityId, out var view) || view == null)
                {
                    view = CreateFireballView(entityId, pair.Value.Radius);
                    _projectileViews[entityId] = view;
                }

                view.transform.position = transform.Position;
            }

            _projectileViewRemoveScratch.Clear();
            foreach (var pair in _projectileViews)
            {
                if (!_aliveProjectileIds.Contains(pair.Key))
                {
                    _projectileViewRemoveScratch.Add(pair.Key);
                }
            }

            for (var i = 0; i < _projectileViewRemoveScratch.Count; i++)
            {
                var id = _projectileViewRemoveScratch[i];
                if (_projectileViews.TryGetValue(id, out var view) && view != null)
                {
                    Object.Destroy(view);
                }

                _projectileViews.Remove(id);
            }
        }

        static GameObject CreateFireballView(uint entityId, float radius)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = $"FireballView_{entityId}";
            var collider = ball.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            var scale = Mathf.Max(FireballBallMinScale, radius * 2f);
            ball.transform.localScale = Vector3.one * scale;
            var renderer = ball.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0.35f, 0.08f, 1f);
            }

            return ball;
        }

        void ClearProjectileViews()
        {
            foreach (var pair in _projectileViews)
            {
                if (pair.Value != null)
                {
                    Object.Destroy(pair.Value);
                }
            }

            _projectileViews.Clear();
            _aliveProjectileIds.Clear();
            _projectileViewRemoveScratch.Clear();
        }
    }
}
