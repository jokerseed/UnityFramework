using System.Collections.Generic;
using Framework.ECS.Components;
using Framework.FixedMath;
using Framework.GamePlay;
using Framework.Battle;
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
        readonly Dictionary<uint, ProjectileViewStamp> _projectileStamps = new Dictionary<uint, ProjectileViewStamp>(16);
        readonly List<uint> _projectileViewRemoveScratch = new List<uint>(16);
        readonly HashSet<uint> _aliveProjectileIds = new HashSet<uint>();

        struct ProjectileViewStamp
        {
            public string AbilityId;
            public uint Owner;
            public long RadiusRaw;
            public long LifetimeRaw;
        }

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
        /// <param name="freezeViews">为 true 时跳过位移同步，用于表现层 HitStop。</param>
        public void Sync(
            GamePlayFramework framework,
            ActorId heroId,
            IReadOnlyList<ActorId> monsterIds,
            float interpolationAlpha,
            bool freezeViews = false)
        {
            if (freezeViews)
            {
                return;
            }

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

            var forward = FPConversions.ToVector3(framework.Registry.GetSimForward(actorId));
            forward.y = 0f;
            var rotation = forward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(forward) * BattleSetup.FaceCamera
                : BattleSetup.FaceCamera;

            if (!_actorRenderStates.TryGetValue(actorId.Value, out var renderState))
            {
                renderState = new ActorRenderState();
                _actorRenderStates[actorId.Value] = renderState;
            }

            var position = FPConversions.ToVector3(actor.SimPosition);
            if (!renderState.Initialized)
            {
                renderState.Initialized = true;
                renderState.PreviousPosition = position;
                renderState.CurrentPosition = position;
                renderState.PreviousRotation = rotation;
                renderState.CurrentRotation = rotation;
            }
            else if (renderState.CurrentPosition != position || renderState.CurrentRotation != rotation)
            {
                renderState.PreviousPosition = renderState.CurrentPosition;
                renderState.CurrentPosition = position;
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
                var projectile = pair.Value;
                var stamp = new ProjectileViewStamp
                {
                    AbilityId = projectile.AbilityId,
                    Owner = projectile.Owner.Value,
                    RadiusRaw = projectile.Radius.RawValue,
                    LifetimeRaw = projectile.RemainingLifetime.RawValue,
                };
                // 寿命变长不再重建。回滚会把同一发投射物的剩余寿命调回去，按寿命重建会闪一下。
                // 同一帧回收 ID 且技能、主人、半径都相同的新弹，仍可能沿用旧视图。
                if (!_projectileViews.TryGetValue(entityId, out var view) || view == null ||
                    !_projectileStamps.TryGetValue(entityId, out var previous) ||
                    previous.AbilityId != stamp.AbilityId ||
                    previous.Owner != stamp.Owner ||
                    previous.RadiusRaw != stamp.RadiusRaw)
                {
                    if (view != null)
                    {
                        Object.Destroy(view);
                    }

                    view = CreateFireballView(entityId, projectile.Radius.AsFloat());
                    _projectileViews[entityId] = view;
                }

                _projectileStamps[entityId] = stamp;
                view.transform.position = FPConversions.ToVector3(transform.Position);
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
                _projectileStamps.Remove(id);
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
            DemoPrimitiveMaterial.Apply(renderer, new Color(1f, 0.35f, 0.08f, 1f));

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
            _projectileStamps.Clear();
            _aliveProjectileIds.Clear();
            _projectileViewRemoveScratch.Clear();
        }
    }

    /// <summary>演示用球体共用一份材质，颜色走 PropertyBlock，避免 <c>renderer.material</c> 复制泄漏。</summary>
    static class DemoPrimitiveMaterial
    {
        static Material _shared;
        static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        /// <summary>把颜色写到渲染器，不实例化材质。</summary>
        /// <param name="renderer">目标渲染器；为 null 时忽略。</param>
        /// <param name="color">要显示的颜色。</param>
        public static void Apply(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            if (_shared == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var source = temp.GetComponent<Renderer>();
                _shared = source != null ? source.sharedMaterial : null;
                UnityEngine.Object.Destroy(temp);
            }

            if (_shared != null)
            {
                renderer.sharedMaterial = _shared;
            }

            Block.SetColor("_Color", color);
            renderer.SetPropertyBlock(Block);
        }
    }
}
