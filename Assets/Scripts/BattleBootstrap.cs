using System;
using System.Collections.Generic;
using Framework.Config;
using Framework.Core;
using Framework.ECS.Components;
using Framework.GamePlay;
using Framework.GamePlay.Data;
using Framework.GAS.Abilities;
using Framework.GAS.Events;
using Framework.Logging;
using Framework.Res;
using UnityEngine;

/// <summary>
/// 战斗场景业务入口：进 Battle 后由 GamePlay 接管——创建 Actor、注册技能、每帧 Tick，并同步模型表现。
/// Hero 周期施放 Fireball；弹道用简易球体跟随 ECS 投射物。
/// 挂到 <c>Assets/Bundles/Scenes/Battle.unity</c> 任意常驻节点上。
/// </summary>
public sealed class BattleBootstrap : MonoBehaviour
{
    static readonly Vector3 HeroPosition = new Vector3(-2f, 0f, 0f);
    static readonly Vector3 MonsterPosition = new Vector3(2f, 0f, 0f);
    static readonly Quaternion FaceCamera = Quaternion.Euler(0f, 180f, 0f);

    static readonly ActorId HeroId = new ActorId(1);
    static readonly ActorId MonsterId = new ActorId(2);
    const int HeroTeamId = 1;
    const int MonsterTeamId = 2;
    const string FireballAbilityId = "Fireball";
    const float FireballCastHeight = 1.2f;
    const float FireballBallMinScale = 0.35f;

    GamePlayFramework _framework;
    Action<DamageDealtEvent> _onDamageDealt;
    bool _battleStarted;

    ResourceAssetHandle _heroHandle;
    ResourceAssetHandle _monsterHandle;
    GameObject _heroInstance;
    GameObject _monsterInstance;

    readonly Dictionary<uint, GameObject> _projectileViews = new Dictionary<uint, GameObject>(16);
    readonly List<uint> _projectileViewRemoveScratch = new List<uint>(16);
    readonly HashSet<uint> _aliveProjectileIds = new HashSet<uint>();

    void Start()
    {
        if (!TryBindFramework())
        {
            return;
        }

        SpawnModels();
        if (!TryCreateActors())
        {
            return;
        }

        _battleStarted = true;
        GameLog.Info(LogCategories.GamePlay, "Battle gameplay started (actors + tick).");
    }

    void Update()
    {
        if (!_battleStarted || _framework == null)
        {
            return;
        }

        TryCastHeroFireball();
        _framework.Tick(Time.deltaTime);
        SyncModelTransforms();
        SyncProjectileViews();
    }

    bool TryBindFramework()
    {
        _framework = GamePlayModule.Instance?.Framework;
        if (_framework == null)
        {
            GameLog.Error(LogCategories.GamePlay, "GamePlayModule / Framework is not ready; cannot start battle.");
            return false;
        }

        if (!ConfigManager.HasInstance)
        {
            GameLog.Error(LogCategories.GamePlay, "ConfigManager is not ready; cannot register abilities.");
            return false;
        }

        try
        {
            ConfigManager.Instance.LoadTables();
        }
        catch (Exception ex)
        {
            GameLog.Error(LogCategories.GamePlay, $"Load config tables failed: {ex.Message}");
            return false;
        }

        _onDamageDealt = OnDamageDealt;
        _framework.EventBus.Subscribe(_onDamageDealt);
        return true;
    }

    bool TryCreateActors()
    {
        try
        {
            var tables = ConfigManager.Instance.GetTables();

            _framework.CreateActor(HeroId, HeroPosition, maxHealth: 100f, teamId: HeroTeamId);
            _framework.CreateActor(MonsterId, MonsterPosition, maxHealth: 100f, teamId: MonsterTeamId);

            _framework.RegisterActorAbilities(
                HeroId,
                teamId: HeroTeamId,
                abilityIds: new[] { FireballAbilityId, "Slash" },
                tables);

            _framework.RegisterActorAbilities(
                MonsterId,
                teamId: MonsterTeamId,
                abilityIds: new[] { "Slash" },
                tables);

            return true;
        }
        catch (Exception ex)
        {
            GameLog.Error(LogCategories.GamePlay, $"Create battle actors failed: {ex.Message}");
            CleanupActors();
            return false;
        }
    }

    void TryCastHeroFireball()
    {
        if (!_framework.Registry.TryGet(HeroId, out var hero) ||
            !_framework.Registry.TryGet(MonsterId, out var monster))
        {
            return;
        }

        var origin = hero.Position + Vector3.up * FireballCastHeight;
        var toTarget = monster.Position - hero.Position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            toTarget = Vector3.right;
        }

        var context = new AbilityActivationContext(origin, toTarget, MonsterId);
        var result = _framework.TryActivateAbility(HeroId, FireballAbilityId, context);
        if (!result.Success)
        {
            return;
        }

        GameLog.Info(LogCategories.GamePlay, $"Hero cast {LogStyle.Name(FireballAbilityId)}");
    }

    void SpawnModels()
    {
        var res = ResourceManager.Instance;
        if (res == null || !res.IsInitialized)
        {
            GameLog.Error(LogCategories.GamePlay, "ResourceManager is not ready; cannot spawn battle models.");
            return;
        }

        _heroInstance = SpawnModel(res, ResourceAddresses.MaleSword01Prefab, HeroPosition, "Hero", out _heroHandle);
        _monsterInstance = SpawnModel(res, ResourceAddresses.AxeKnightPrefab, MonsterPosition, "Monster", out _monsterHandle);

        GameLog.Info(LogCategories.GamePlay,
            $"Battle models spawned  Hero={LogStyle.Name(_heroInstance != null ? _heroInstance.name : "null")}  Monster={LogStyle.Name(_monsterInstance != null ? _monsterInstance.name : "null")}");
    }

    static GameObject SpawnModel(
        ResourceManager res,
        string location,
        Vector3 position,
        string instanceName,
        out ResourceAssetHandle handle)
    {
        handle = res.LoadAssetSync<GameObject>(location);
        if (!handle.IsValid || !handle.Succeeded)
        {
            GameLog.Error(LogCategories.GamePlay,
                $"Load model failed: {LogStyle.Name(instanceName)}  location={LogStyle.Value(location)}  error={handle.Error}");
            handle.Dispose();
            handle = default;
            return null;
        }

        var instance = handle.InstantiateSync();
        if (instance == null)
        {
            GameLog.Error(LogCategories.GamePlay, $"Instantiate model failed: {LogStyle.Name(instanceName)}");
            handle.Dispose();
            handle = default;
            return null;
        }

        instance.name = instanceName;
        instance.transform.SetPositionAndRotation(position, FaceCamera);
        return instance;
    }

    void SyncModelTransforms()
    {
        SyncOne(_heroInstance, HeroId);
        SyncOne(_monsterInstance, MonsterId);
    }

    void SyncOne(GameObject instance, ActorId actorId)
    {
        if (instance == null || !_framework.Registry.TryGet(actorId, out var actor))
        {
            return;
        }

        instance.transform.position = actor.Position;
    }

    void SyncProjectileViews()
    {
        var world = _framework.EcsWorld;
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
                Destroy(view);
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
            Destroy(collider);
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
                Destroy(pair.Value);
            }
        }

        _projectileViews.Clear();
        _aliveProjectileIds.Clear();
        _projectileViewRemoveScratch.Clear();
    }

    static void OnDamageDealt(DamageDealtEvent e)
    {
        GameLog.Info(LogCategories.GamePlay,
            $"Damage {LogStyle.Value(e.Source.Value)} → {LogStyle.Value(e.Target.Value)}  ability={LogStyle.Name(e.AbilityId)}  final={LogStyle.Value(e.FinalDamage.ToString("F1"))}");
    }

    void CleanupActors()
    {
        if (_framework == null)
        {
            return;
        }

        if (_onDamageDealt != null)
        {
            _framework.EventBus.Unsubscribe(_onDamageDealt);
            _onDamageDealt = null;
        }

        _framework.DestroyActor(HeroId);
        _framework.DestroyActor(MonsterId);
    }

    void OnDestroy()
    {
        _battleStarted = false;
        ClearProjectileViews();
        CleanupActors();
        // Framework 由 GamePlayModule 持有，场景退出时不 Dispose。

        if (_heroInstance != null)
        {
            Destroy(_heroInstance);
            _heroInstance = null;
        }

        if (_monsterInstance != null)
        {
            Destroy(_monsterInstance);
            _monsterInstance = null;
        }

        if (_heroHandle.IsValid)
        {
            _heroHandle.Dispose();
            _heroHandle = default;
        }

        if (_monsterHandle.IsValid)
        {
            _monsterHandle.Dispose();
            _monsterHandle = default;
        }

        _framework = null;
    }
}
