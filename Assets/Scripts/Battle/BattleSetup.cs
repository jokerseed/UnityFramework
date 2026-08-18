using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Config;
using Framework.Core;
using Framework.GamePlay;
using Framework.GamePlay.Data;
using Framework.Logging;
using Framework.Res;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 战斗启动：加载模型、创建 Actor、驱动波次。
    /// </summary>
    public sealed class BattleSetup
    {
        /// <summary>英雄出生点。</summary>
        public static readonly Vector3 HeroPosition = new Vector3(-2f, 0f, 0f);

        /// <summary>面向摄像机的默认旋转。</summary>
        public static readonly Quaternion FaceCamera = Quaternion.Euler(0f, 180f, 0f);

        const int MonsterCount = 12;
        const int HeroTeamId = 1;
        const int MonsterTeamId = 2;
        const string FireballAbilityId = "Fireball";
        const string DodgeAbilityId = "Dodge";
        const float MonsterBaseHealth = 40f;

        readonly List<ActorId> _monsterIds = new List<ActorId>(MonsterCount);
        BattleWaveDirector _waveDirector;

        /// <summary>英雄 ActorId；<see cref="CreateActors"/> 成功后有效。</summary>
        public ActorId HeroId { get; private set; }

        /// <summary>杂兵 ActorId 列表。</summary>
        public IReadOnlyList<ActorId> MonsterIds => _monsterIds;

        /// <summary>按索引计算杂兵出生点。</summary>
        /// <param name="index">槽位索引。</param>
        /// <returns>世界坐标。</returns>
        public static Vector3 MonsterSpawnPosition(int index)
        {
            var column = index % 4;
            var row = index / 4;
            return new Vector3(2.2f + row * 1.05f, 0f, (column - 1.5f) * 1.1f);
        }

        /// <summary>异步加载英雄与杂兵模型，并注册到 <see cref="BattleViewBinder"/>。</summary>
        /// <param name="session">战斗会话。</param>
        /// <param name="viewBinder">表现层绑定器。</param>
        public IEnumerator LoadViewsAsync(BattleSession session, BattleViewBinder viewBinder)
        {
            var res = ResourceManager.Instance;
            if (res == null || !res.IsInitialized)
            {
                GameLog.Error(LogCategories.GamePlay, "ResourceManager is not ready.");
                yield break;
            }

            ResourceAssetHandle heroHandle = default;
            ResourceAssetHandle monsterHandle = default;
            yield return session.Scope.LoadAsync<GameObject>(
                ResourceAddresses.MaleSword01Prefab, h => heroHandle = h, priority: 10);
            if (!heroHandle.IsValid || !heroHandle.Succeeded)
            {
                GameLog.Error(LogCategories.GamePlay, $"Load model failed: {LogStyle.Name("Hero")} error={heroHandle.Error}");
                yield break;
            }

            yield return session.Scope.LoadAsync<GameObject>(
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
            viewBinder.RegisterHero(heroGo);

            var monsterPrefab = monsterHandle.GetAsset<GameObject>();
            if (monsterPrefab == null)
            {
                GameLog.Error(LogCategories.GamePlay, "Monster prefab asset is null.");
                yield break;
            }

            BattleMonsterView.Configure(monsterPrefab);
            BattleMonsterView.Setup();

            var monsterViews = new List<BattleMonsterView>(MonsterCount);
            for (var i = 0; i < MonsterCount; i++)
            {
                var view = BattleMonsterView.SpawnAt(MonsterSpawnPosition(i), FaceCamera, "Monster_" + (i + 1));
                monsterViews.Add(view);
                if (i < MonsterCount - 1)
                {
                    yield return null;
                }
            }

            viewBinder.RegisterMonsters(monsterViews);

            GameLog.Info(LogCategories.GamePlay,
                $"Battle models spawned  Hero={LogStyle.Name(heroGo.name)}  Monsters={LogStyle.Value(monsterViews.Count.ToString())}");
        }

        /// <summary>创建英雄与杂兵 Actor，并初始化波次导演。</summary>
        /// <param name="session">战斗会话。</param>
        /// <returns>成功返回 <see langword="true"/>。</returns>
        public bool CreateActors(BattleSession session)
        {
            var framework = session?.Framework;
            if (framework == null)
            {
                return false;
            }

            try
            {
                var tables = ConfigManager.Instance.GetTables();

                HeroId = session.AllocateActorId();
                framework.CreateActor(HeroId, HeroPosition, maxHealth: 120f, teamId: HeroTeamId);
                framework.RegisterActorAbilities(
                    HeroId,
                    teamId: HeroTeamId,
                    abilityIds: new[] { FireballAbilityId, "Slash", "Slash2", "Slash3", DodgeAbilityId },
                    tables);

                _monsterIds.Clear();
                session.AllocateActorIds(MonsterCount, _monsterIds);
                for (var i = 0; i < MonsterCount; i++)
                {
                    var monsterId = _monsterIds[i];
                    framework.CreateActor(monsterId, MonsterSpawnPosition(i), maxHealth: 40f, teamId: MonsterTeamId);
                    framework.RegisterActorAbilities(monsterId, teamId: MonsterTeamId, abilityIds: new[] { "MobSlash" }, tables);
                    framework.SetBattleAgent(monsterId, BattleAiNodes.CreateMeleeChaserAgent("MobSlash", HeroId));
                }

                _waveDirector = new BattleWaveDirector(_monsterIds, HeroId, MonsterBaseHealth, "MobSlash");
                return true;
            }
            catch (Exception ex)
            {
                GameLog.Error(LogCategories.GamePlay, $"Create battle actors failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>驱动波次刷新。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="deltaTime">帧间隔。</param>
        public void TickWaves(GamePlayFramework framework, float deltaTime)
        {
            if (_waveDirector == null || !framework.Registry.TryGet(HeroId, out var hero))
            {
                return;
            }

            if (_waveDirector.Tick(framework, hero.Position, deltaTime))
            {
                GameLog.Info(LogCategories.GamePlay, $"Wave {LogStyle.Value(_waveDirector.Wave.ToString())}");
            }
        }

        /// <summary>重置波次与 Actor 列表（不销毁 View）。</summary>
        public void ResetState()
        {
            _waveDirector = null;
            _monsterIds.Clear();
            HeroId = default;
        }

        /// <summary>释放杂兵对象池等资源。</summary>
        public void TearDown()
        {
            ResetState();
            if (BattleMonsterView.IsReady)
            {
                BattleMonsterView.TearDown();
            }
        }
    }
}
