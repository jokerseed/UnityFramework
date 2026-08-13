using System;
using Framework.Config;
using Framework.Core;
using Framework.GamePlay;
using Framework.GamePlay.Data;
using Framework.GAS.Events;
using Framework.Logging;
using Framework.Res;
using UnityEngine;

/// <summary>
/// 战斗场景业务入口：进 Battle 后由 GamePlay 接管——创建 Actor、注册技能、每帧 Tick，并同步模型表现。
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

    GamePlayFramework _framework;
    Action<DamageDealtEvent> _onDamageDealt;
    bool _battleStarted;

    ResourceAssetHandle _heroHandle;
    ResourceAssetHandle _monsterHandle;
    GameObject _heroInstance;
    GameObject _monsterInstance;

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

        _framework.Tick(Time.deltaTime);
        SyncModelTransforms();
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
                abilityIds: new[] { "Fireball", "Slash" },
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
