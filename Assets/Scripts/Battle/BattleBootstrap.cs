using System.Collections;
using Framework.Coroutine;
using Framework.GamePlay;
using Framework.Logging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// 战斗场景装配器：持有 Session 与子模块，驱动启动协程与 Update 顺序。
    /// Hero 可用 WASD 移动；J 近战三连，K 火球，Left Shift 闪避。12 只杂兵槽位，全灭后刷下一波。
    /// F8：用当前录像在影子 Session 上本地对拍。
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
        LocalLockstepHost _host;
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
            _input.Enable();
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

            _host = new LocalLockstepHost(_session);
            return true;
        }

        void Update()
        {
            if (!_battleStarted || _session?.Framework == null)
            {
                return;
            }

            var framework = _session.Framework;
            _input.Sample(Time.unscaledDeltaTime);
            _presentation.TickHitStop();
            _host.Tick(
                Time.unscaledDeltaTime,
                frame => _input.Encode(framework, _setup.HeroId, frame.Commands, _session.FixedDeltaTime),
                fixedDeltaTime => _setup.TickWaves(framework, fixedDeltaTime));
            _presentation.Tick();
            _view.Sync(framework, _setup.HeroId, _setup.MonsterIds, _host.InterpolationAlpha, _presentation.FreezeViews);
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                TryVerifyReplay();
            }
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
            _host = null;
        }

        void TryVerifyReplay()
        {
            if (_host == null || _session == null || GamePlayModule.Instance?.Director == null)
            {
                return;
            }

            var tape = _host.Recorder.ToTape(_session.RandomSeed, _session.FixedDeltaTime);
            if (tape.Frames.Count == 0)
            {
                GameLog.Warning(LogCategories.GamePlay, "Replay verify skipped: tape is empty.");
                return;
            }

            var director = GamePlayModule.Instance.Director;
            var shadow = director.CreateSession(tape.Seed);
            shadow.FixedDeltaTime = tape.FixedDeltaTime;
            var setup = new BattleSetup();
            if (!setup.CreateActors(shadow))
            {
                director.DestroySession(shadow);
                GameLog.Error(LogCategories.GamePlay, "Replay verify failed: shadow CreateActors failed.");
                return;
            }

            var result = BattleReplayVerifier.Run(
                tape,
                shadow,
                dt => setup.TickWaves(shadow.Framework, dt));
            director.DestroySession(shadow);

            if (result.Matched)
            {
                GameLog.Info(
                    LogCategories.GamePlay,
                    $"Replay verify matched  frames={LogStyle.Value(result.ComparedFrames.ToString())}  checksum={LogStyle.Value(_host.LastChecksum.ToString())}");
            }
            else
            {
                GameLog.Error(
                    LogCategories.GamePlay,
                    $"Replay verify mismatch  frame={LogStyle.Value(result.MismatchFrameIndex.ToString())}  expected={LogStyle.Value(result.ExpectedChecksum.ToString())}  actual={LogStyle.Value(result.ActualChecksum.ToString())}");
            }
        }

        void OnDestroy()
        {
            _battleStarted = false;
            _host?.Clear();
            _input?.Dispose();
            _view?.Clear();
            CleanupSession();
            _setup?.TearDown();
        }
    }
}
