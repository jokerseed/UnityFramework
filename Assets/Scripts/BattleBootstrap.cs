using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Config;
using Framework.Coroutine;
using Framework.ECS.Components;
using Framework.GamePlay;
using Framework.GamePlay.Data;
using Framework.GAS.Abilities;
using Framework.GAS.Events;
using Framework.GAS.Tags;
using Framework.Core;
using Framework.Logging;
using Framework.ObjectPool;
using Framework.Res;
using UnityEngine;

namespace Game
{
/// <summary>
/// 战斗场景表现层绑定器：向 <see cref="BattleDirector"/> 申请 Session，加载模型、同步 GO 表现与输入。
/// Hero 可用 WASD 移动；J 近战三连，K 火球，Left Shift 闪避。12 只杂兵槽位，全灭后刷下一波。
/// 挂到 <c>Assets/Bundles/Scenes/Battle.unity</c> 任意常驻节点上。
/// </summary>
public sealed class BattleBootstrap : MonoBehaviour
{
    static readonly Vector3 HeroPosition = new Vector3(-2f, 0f, 0f);
    static readonly Quaternion FaceCamera = Quaternion.Euler(0f, 180f, 0f);

    const int MonsterCount = 12;
    const int HeroTeamId = 1;
    const int MonsterTeamId = 2;
    const string FireballAbilityId = "Fireball";
    const float FireballCastHeight = 1.2f;
    const float FireballBallMinScale = 0.35f;
    const string DodgeAbilityId = "Dodge";
    const float ComboBufferSeconds = 0.28f;
    const float MonsterBaseHealth = 40f;
    const float HitStopScale = 0.18f;
    const float HitStopSeconds = 0.05f;
    static readonly string[] MeleeComboIds = { "Slash3", "Slash2", "Slash" };

    // ── Session ──────────────────────────────────────────────
    BattleSession _session;
    GamePlayFramework _framework; // 快捷引用，等同 _session.Framework

    // ── 战斗状态 ─────────────────────────────────────────────
    ActorId _heroId;
    readonly List<ActorId> _monsterIds = new List<ActorId>(MonsterCount);
    BattleWaveDirector _waveDirector;
    Action<DamageDealtEvent> _onDamageDealt;
    Action<ActorDiedEvent> _onActorDied;
    Action<GameplayCueEvent> _onCue;
    readonly BattleCuePresenter _cuePresenter = new BattleCuePresenter();
    bool _battleStarted;
    float _meleeBuffer;
    float _hitStopUntilUnscaled;

    // ── 表现层 GO ─────────────────────────────────────────────
    GameObject _heroInstance;
    readonly List<BattleMonsterView> _monsterViews = new List<BattleMonsterView>(MonsterCount);
    readonly Dictionary<uint, GameObject> _projectileViews = new Dictionary<uint, GameObject>(16);
    readonly List<uint> _projectileViewRemoveScratch = new List<uint>(16);
    readonly HashSet<uint> _aliveProjectileIds = new HashSet<uint>();

    void Start()
    {
        GameCoroutine.StartGlobal(StartBattleAsync());
    }

    IEnumerator StartBattleAsync()
    {
        if (!TryCreateSession())
        {
            yield break;
        }

        yield return SpawnModelsAsync();
        if (_heroInstance == null || _monsterViews.Count == 0)
        {
            GameLog.Error(LogCategories.GamePlay, "Battle models failed to spawn; gameplay not started.");
            CleanupBattleResources();
            yield break;
        }

        if (!TryCreateActors())
        {
            CleanupBattleResources();
            yield break;
        }

        _battleStarted = true;
        GameLog.Info(LogCategories.GamePlay, $"Battle started  session={LogStyle.Value(_session.SessionId.ToString())}");
    }

    // ── Session 创建 ──────────────────────────────────────────

    bool TryCreateSession()
    {
        var module = GamePlayModule.Instance;
        if (module?.Director == null)
        {
            GameLog.Error(LogCategories.GamePlay, "GamePlayModule / Director is not ready.");
            return false;
        }

        if (!ConfigManager.HasInstance)
        {
            GameLog.Error(LogCategories.GamePlay, "ConfigManager is not ready.");
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

        _session = module.Director.CreateSession();
        _framework = _session.Framework;

        _onDamageDealt = OnDamageDealt;
        _framework.EventBus.Subscribe(_onDamageDealt);
        _onActorDied = OnActorDied;
        _framework.EventBus.Subscribe(_onActorDied);
        _onCue = OnGameplayCue;
        _framework.EventBus.Subscribe(_onCue);
        return true;
    }

    // ── Actor 创建 ────────────────────────────────────────────

    bool TryCreateActors()
    {
        try
        {
            var tables = ConfigManager.Instance.GetTables();
            _cuePresenter.Bind(tables.CfgTbCue);

            _heroId = _session.AllocateActorId();
            _framework.CreateActor(_heroId, HeroPosition, maxHealth: 120f, teamId: HeroTeamId);
            _framework.RegisterActorAbilities(
                _heroId,
                teamId: HeroTeamId,
                abilityIds: new[] { FireballAbilityId, "Slash", "Slash2", "Slash3", DodgeAbilityId },
                tables);

            _session.AllocateActorIds(MonsterCount, _monsterIds);
            for (var i = 0; i < MonsterCount; i++)
            {
                var monsterId = _monsterIds[i];
                _framework.CreateActor(monsterId, MonsterSpawnPosition(i), maxHealth: 40f, teamId: MonsterTeamId);
                _framework.RegisterActorAbilities(monsterId, teamId: MonsterTeamId, abilityIds: new[] { "MobSlash" }, tables);
                _framework.SetBattleAgent(monsterId, BattleAiNodes.CreateMeleeChaserAgent("MobSlash", _heroId));
            }

            _waveDirector = new BattleWaveDirector(_monsterIds, _heroId, MonsterBaseHealth, "MobSlash");
            return true;
        }
        catch (Exception ex)
        {
            GameLog.Error(LogCategories.GamePlay, $"Create battle actors failed: {ex.Message}");
            CleanupSession();
            return false;
        }
    }

    // ── Unity 帧循环 ──────────────────────────────────────────

    void Update()
    {
        if (!_battleStarted || _framework == null)
        {
            return;
        }

        RestoreHitStopIfExpired();
        TryMoveHero();
        TryHeroMelee();
        TryCastHeroFireball();
        TryHeroDodge();
        _framework.Tick(Time.deltaTime);
        TickWaves();
        _cuePresenter.Tick(ResolveCuePosition);
        SyncModelTransforms();
        SyncProjectileViews();
    }

    // ── 输入处理 ──────────────────────────────────────────────

    void TryMoveHero()
    {
        if (!_framework.TryGetActor(_heroId, out var asc) || asc.IsDead)
        {
            return;
        }

        if (asc.Tags.HasTag(new GameplayTag(BattleConstants.TagStunned)) ||
            asc.Tags.HasTag(new GameplayTag(BattleConstants.TagKnockedDown)))
        {
            _framework.SetActorVelocity(_heroId, Vector3.zero);
            return;
        }

        if (asc.Tags.HasTag(new GameplayTag(BattleConstants.TagDodging)))
        {
            return;
        }

        var move = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (move.sqrMagnitude < 0.01f)
        {
            _framework.SetActorVelocity(_heroId, Vector3.zero);
            return;
        }

        move.Normalize();
        _framework.Registry.SetForward(_heroId, move);
        _framework.SetActorVelocity(_heroId, move * 3.5f);
    }

    void TryHeroMelee()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            _meleeBuffer = ComboBufferSeconds;
        }

        if (_meleeBuffer <= 0f)
        {
            return;
        }

        if (!_framework.Registry.TryGet(_heroId, out var hero) ||
            !_framework.TryGetActor(_heroId, out var asc) ||
            asc.IsDead)
        {
            _meleeBuffer = 0f;
            return;
        }

        var forward = _framework.Registry.GetForward(_heroId);
        var context = new AbilityActivationContext(hero.Position, forward);
        for (var i = 0; i < MeleeComboIds.Length; i++)
        {
            if (!_framework.TryActivateAbility(_heroId, MeleeComboIds[i], context).Success)
            {
                continue;
            }

            _meleeBuffer = 0f;
            GameLog.Info(LogCategories.GamePlay, $"Hero melee {LogStyle.Name(MeleeComboIds[i])}");
            return;
        }

        _meleeBuffer -= Time.deltaTime;
    }

    void TryCastHeroFireball()
    {
        if (!Input.GetKeyDown(KeyCode.K) || !_framework.Registry.TryGet(_heroId, out var hero))
        {
            return;
        }

        var targetId = _framework.QueryNearestEnemy(_heroId, hero.Position, 20f);
        var origin = hero.Position + Vector3.up * FireballCastHeight;
        var direction = _framework.Registry.GetForward(_heroId);
        if (_framework.Registry.TryGet(targetId, out var target))
        {
            var toTarget = target.Position - hero.Position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                direction = toTarget.normalized;
                _framework.Registry.SetForward(_heroId, direction);
            }
        }

        var result = _framework.TryActivateAbility(_heroId, FireballAbilityId, new AbilityActivationContext(origin, direction, targetId));
        if (result.Success)
        {
            GameLog.Info(LogCategories.GamePlay, $"Hero cast {LogStyle.Name(FireballAbilityId)}");
        }
    }

    void TryHeroDodge()
    {
        if (!Input.GetKeyDown(KeyCode.LeftShift) || !_framework.Registry.TryGet(_heroId, out var hero))
        {
            return;
        }

        var forward = _framework.Registry.GetForward(_heroId);
        var move = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (move.sqrMagnitude > 0.01f)
        {
            forward = move.normalized;
            _framework.Registry.SetForward(_heroId, forward);
        }

        var result = _framework.TryActivateAbility(_heroId, DodgeAbilityId, new AbilityActivationContext(hero.Position, forward));
        if (result.Success)
        {
            GameLog.Info(LogCategories.GamePlay, $"Hero {LogStyle.Name(DodgeAbilityId)}");
        }
    }

    void TickWaves()
    {
        if (_waveDirector == null || !_framework.Registry.TryGet(_heroId, out var hero))
        {
            return;
        }

        if (_waveDirector.Tick(_framework, hero.Position, Time.deltaTime))
        {
            GameLog.Info(LogCategories.GamePlay, $"Wave {LogStyle.Value(_waveDirector.Wave.ToString())}");
        }
    }

    // ── 模型加载 ──────────────────────────────────────────────

    IEnumerator SpawnModelsAsync()
    {
        var res = ResourceManager.Instance;
        if (res == null || !res.IsInitialized)
        {
            GameLog.Error(LogCategories.GamePlay, "ResourceManager is not ready.");
            yield break;
        }

        ResourceAssetHandle heroHandle = default;
        ResourceAssetHandle monsterHandle = default;
        yield return _session.Scope.LoadAsync<GameObject>(
            ResourceAddresses.MaleSword01Prefab, h => heroHandle = h, priority: 10);
        if (!heroHandle.IsValid || !heroHandle.Succeeded)
        {
            GameLog.Error(LogCategories.GamePlay, $"Load model failed: {LogStyle.Name("Hero")} error={heroHandle.Error}");
            yield break;
        }

        yield return _session.Scope.LoadAsync<GameObject>(
            ResourceAddresses.AxeKnightPrefab, h => monsterHandle = h, priority: 10);
        if (!monsterHandle.IsValid || !monsterHandle.Succeeded)
        {
            GameLog.Error(LogCategories.GamePlay, $"Load model failed: {LogStyle.Name("Monster")} error={monsterHandle.Error}");
            yield break;
        }

        GameObject heroGo = null;
        yield return res.InstantiateAsync(heroHandle, null, go => heroGo = go, priority: 10);
        if (heroGo == null)
        {
            GameLog.Error(LogCategories.GamePlay, $"Instantiate model failed: {LogStyle.Name("Hero")}");
            yield break;
        }

        heroGo.name = "Hero";
        heroGo.transform.SetPositionAndRotation(HeroPosition, FaceCamera);
        _heroInstance = heroGo;

        var monsterPrefab = monsterHandle.GetAsset<GameObject>();
        if (monsterPrefab == null)
        {
            GameLog.Error(LogCategories.GamePlay, "Monster prefab asset is null.");
            yield break;
        }

        BattleMonsterView.Configure(monsterPrefab);
        BattleMonsterView.Setup();

        for (var i = 0; i < MonsterCount; i++)
        {
            var view = BattleMonsterView.SpawnAt(MonsterSpawnPosition(i), FaceCamera, "Monster_" + (i + 1));
            _monsterViews.Add(view);
            if (i < MonsterCount - 1)
            {
                yield return null;
            }
        }

        GameLog.Info(LogCategories.GamePlay,
            $"Battle models spawned  Hero={LogStyle.Name(_heroInstance.name)}  Monsters={LogStyle.Value(_monsterViews.Count.ToString())}");
    }

    // ── 表现层同步 ────────────────────────────────────────────

    void SyncModelTransforms()
    {
        SyncOne(_heroInstance, _heroId);
        var count = Mathf.Min(_monsterViews.Count, _monsterIds.Count);
        for (var i = 0; i < count; i++)
        {
            SyncOne(_monsterViews[i].View, _monsterIds[i]);
        }
    }

    void SyncOne(GameObject instance, ActorId actorId)
    {
        if (instance == null || !_framework.Registry.TryGet(actorId, out var actor))
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

        var forward = _framework.Registry.GetForward(actorId);
        forward.y = 0f;
        var rotation = forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(forward) * FaceCamera
            : FaceCamera;
        instance.transform.SetPositionAndRotation(actor.Position, rotation);
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

    // ── HitStop ───────────────────────────────────────────────

    void RestoreHitStopIfExpired()
    {
        if (Time.timeScale < 1f && Time.unscaledTime >= _hitStopUntilUnscaled)
        {
            Time.timeScale = 1f;
        }
    }

    // ── 事件回调 ──────────────────────────────────────────────

    void OnDamageDealt(DamageDealtEvent e)
    {
        GameLog.Info(LogCategories.GamePlay,
            $"Damage {LogStyle.Value(e.Source.Value)} → {LogStyle.Value(e.Target.Value)}  ability={LogStyle.Name(e.AbilityId)}  final={LogStyle.Value(e.FinalDamage.ToString("F1"))}  crit={e.IsCrit}");

        if (e.Source != _heroId || e.FinalDamage <= 0f)
        {
            return;
        }

        Time.timeScale = HitStopScale;
        _hitStopUntilUnscaled = Time.unscaledTime + HitStopSeconds;
    }

    static void OnActorDied(ActorDiedEvent e)
    {
        GameLog.Info(LogCategories.GamePlay,
            $"Actor died {LogStyle.Value(e.Actor.Value)}  killer={LogStyle.Value(e.Killer.Value)}  ability={LogStyle.Name(e.AbilityId ?? "")}");
    }

    void OnGameplayCue(GameplayCueEvent e) => _cuePresenter.Handle(e);

    Vector3? ResolveCuePosition(ActorId actorId)
    {
        if (_framework != null && _framework.Registry.TryGet(actorId, out var actor))
        {
            return actor.Position;
        }

        return null;
    }

    // ── 清理 ──────────────────────────────────────────────────

    void CleanupSession()
    {
        if (_framework != null)
        {
            if (_onDamageDealt != null) { _framework.EventBus.Unsubscribe(_onDamageDealt); _onDamageDealt = null; }
            if (_onActorDied != null) { _framework.EventBus.Unsubscribe(_onActorDied); _onActorDied = null; }
            if (_onCue != null) { _framework.EventBus.Unsubscribe(_onCue); _onCue = null; }
        }

        _cuePresenter.Clear();
        _waveDirector = null;
        _monsterIds.Clear();

        if (_session != null)
        {
            GamePlayModule.Instance?.Director?.DestroySession(_session);
            _session = null;
        }

        _framework = null;
    }

    void CleanupBattleResources()
    {
        if (_heroInstance != null)
        {
            Destroy(_heroInstance);
            _heroInstance = null;
        }

        for (var i = 0; i < _monsterViews.Count; i++)
        {
            if (_monsterViews[i] != null)
            {
                BattleMonsterView.Unspawn(_monsterViews[i]);
            }
        }

        _monsterViews.Clear();
        if (BattleMonsterView.IsReady)
        {
            BattleMonsterView.TearDown();
        }
    }

    void OnDestroy()
    {
        _battleStarted = false;
        Time.timeScale = 1f;
        ClearProjectileViews();
        CleanupSession();       // 先取消事件 + 销毁 Session（含 Framework.Dispose + Scope.Dispose）
        CleanupBattleResources(); // 再销毁表现层 GO
    }

    // ── 工具方法 ──────────────────────────────────────────────

    static Vector3 MonsterSpawnPosition(int index)
    {
        var column = index % 4;
        var row = index / 4;
        return new Vector3(2.2f + row * 1.05f, 0f, (column - 1.5f) * 1.1f);
    }
}
}
