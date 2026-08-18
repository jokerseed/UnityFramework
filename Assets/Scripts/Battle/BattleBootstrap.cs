using System.Collections;
using Framework.Coroutine;
using Framework.GamePlay;
using Framework.Logging;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 战斗场景装配器：持有 Session 与子模块，驱动启动协程与 Update 顺序。
    /// Hero 可用 WASD 移动；J 近战三连，K 火球，Left Shift 闪避。12 只杂兵槽位，全灭后刷下一波。
    /// 挂到 <c>Assets/Bundles/Scenes/Battle.unity</c> 任意常驻节点上。
    /// </summary>
    public sealed class BattleBootstrap : MonoBehaviour
    {
        BattleFlow _flow;
        BattleSession _session;
        BattleSetup _setup;
        BattleInputController _input;
        BattleViewBinder _view;
        BattlePresentation _presentation;
        bool _battleStarted;

        void Start()
        {
            _setup = new BattleSetup();
            _input = new BattleInputController();
            _view = new BattleViewBinder();
            _presentation = new BattlePresentation();
            GameCoroutine.StartGlobal(StartBattleAsync());
        }

        IEnumerator StartBattleAsync()
        {
            if (!TryEnterBattle())
            {
                yield break;
            }

            yield return _setup.LoadViewsAsync(_session, _view);
            if (!_view.ViewsReady)
            {
                GameLog.Error(LogCategories.GamePlay, "Battle models failed to spawn; gameplay not started.");
                _view.Clear();
                _setup.TearDown();
                yield break;
            }

            if (!_setup.CreateActors(_session))
            {
                CleanupSession();
                _view.Clear();
                _setup.TearDown();
                yield break;
            }

            _presentation.Bind(_session.Framework, _setup.HeroId);
            _battleStarted = true;
            GameLog.Info(LogCategories.GamePlay, $"Battle started  session={LogStyle.Value(_session.SessionId.ToString())}");
        }

        bool TryEnterBattle()
        {
            _flow = GameSceneFlow.Instance.EnsureBattleFlow();
            if (_flow == null || !_flow.TryEnter())
            {
                return false;
            }

            _session = _flow.Session;
            if (_session?.Framework == null)
            {
                GameLog.Error(LogCategories.GamePlay, "BattleFlow session is not ready.");
                return false;
            }

            return true;
        }

        void Update()
        {
            if (!_battleStarted || _session?.Framework == null)
            {
                return;
            }

            var framework = _session.Framework;
            var deltaTime = Time.deltaTime;
            var inputFrame = _input.Sample(deltaTime);

            _presentation.RestoreHitStopIfExpired();
            _session.Tick(deltaTime, fixedDeltaTime =>
            {
                _input.Apply(framework, _setup.HeroId, ref inputFrame);
                _setup.TickWaves(framework, fixedDeltaTime);
            });
            _presentation.Tick();
            _view.Sync(framework, _setup.HeroId, _setup.MonsterIds, _session.InterpolationAlpha);
        }

        void CleanupSession()
        {
            _presentation?.Unbind();

            if (_flow != null)
            {
                _flow.Exit();
                if (GameSceneFlow.HasInstance)
                {
                    GameSceneFlow.Instance.ClearBattleFlow(_flow);
                }

                _flow = null;
            }

            _setup?.ResetState();
            _session = null;
        }

        void OnDestroy()
        {
            _battleStarted = false;
            Time.timeScale = 1f;
            _view?.Clear();
            CleanupSession();
            _setup?.TearDown();
        }
    }
}
