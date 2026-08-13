using Framework.Logging;
using Framework.Res;
using UnityEngine;

/// <summary>
/// 战斗场景业务入口：加载英雄与怪物模型，并放置到固定站位（本阶段仅站立待机）。
/// 挂到 <c>Assets/Bundles/Scenes/Battle.unity</c> 任意常驻节点上。
/// </summary>
public sealed class BattleBootstrap : MonoBehaviour
{
    static readonly Vector3 HeroPosition = new Vector3(-2f, 0f, 0f);
    static readonly Vector3 MonsterPosition = new Vector3(2f, 0f, 0f);
    static readonly Quaternion FaceCamera = Quaternion.Euler(0f, 180f, 0f);

    ResourceAssetHandle _heroHandle;
    ResourceAssetHandle _monsterHandle;
    GameObject _heroInstance;
    GameObject _monsterInstance;

    void Start()
    {
        SpawnModels();
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

    void OnDestroy()
    {
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
    }
}
