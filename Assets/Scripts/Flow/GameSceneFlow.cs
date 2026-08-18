using System.Collections;
using Framework.Core;
using Framework.Coroutine;
using Framework.Logging;
using Framework.Res;
using Framework.UI;

namespace Game
{
    /// <summary>
    /// 全局场景流程：负责主界面与战斗场景切换，并持有当前活跃的 <see cref="BattleFlow"/>。
    /// </summary>
    public sealed class GameSceneFlow : PersistentSingleton<GameSceneFlow>
    {
        bool _isEnteringBattle;

        /// <summary>
        /// 当前活跃战斗流程；没有战斗时为 null。
        /// </summary>
        public BattleFlow CurrentBattleFlow { get; private set; }

        /// <summary>
        /// 打开主界面窗口。
        /// </summary>
        public void ShowMainPage()
        {
            UIManager.Instance.ShowAsync<MainUIWindow>(onComplete: window =>
            {
                if (window != null)
                {
                    GameLog.Info(LogCategories.Launch, "Main page shown");
                }
            });
        }

        /// <summary>
        /// 进入战斗主场景，并为该次进入创建新的 <see cref="BattleFlow"/>。
        /// </summary>
        public void EnterBattle()
        {
            if (_isEnteringBattle)
            {
                return;
            }

            GameCoroutine.StartGlobal(EnterBattleAsync());
        }

        /// <summary>
        /// 创建一个仅供当前场景自举使用的战斗流程。
        /// 适用于直接从编辑器打开 Battle 场景的情况。
        /// </summary>
        /// <returns>当前可用的战斗流程实例。</returns>
        public BattleFlow EnsureBattleFlow()
        {
            if (CurrentBattleFlow == null || CurrentBattleFlow.State == BattleFlowState.Exited)
            {
                CurrentBattleFlow = new BattleFlow();
            }

            return CurrentBattleFlow;
        }

        /// <summary>
        /// 当场景内表现层结束战斗后，清理当前流程引用。
        /// </summary>
        /// <param name="flow">已退出的流程实例。</param>
        public void ClearBattleFlow(BattleFlow flow)
        {
            if (flow != null && ReferenceEquals(CurrentBattleFlow, flow))
            {
                CurrentBattleFlow = null;
            }
        }

        /// <summary>
        /// 战斗局部资源（Session Scope、表现 GO 等）释放后，请求分帧卸载未使用资源。
        /// 多次调用会由 <see cref="ResourceManager"/> 调度器合并为一次。
        /// </summary>
        public static void RequestUnusedAssetsCleanup()
        {
            if (!ResourceManager.HasInstance)
            {
                return;
            }

            var res = ResourceManager.Instance;
            if (res == null || !res.IsInitialized)
            {
                return;
            }

            res.RequestUnloadUnusedAssets();
        }

        IEnumerator EnterBattleAsync()
        {
            _isEnteringBattle = true;
            try
            {
                if (CurrentBattleFlow != null)
                {
                    CurrentBattleFlow.Dispose();
                    CurrentBattleFlow = null;
                }

                CurrentBattleFlow = new BattleFlow();
                GameLog.Info(LogCategories.Launch, $"Loading scene {LogStyle.Name("Battle")}");
                yield return ResourceManager.Instance.LoadMainSceneAsync(ResourceAddresses.BattleScene);
                GameLog.Info(LogCategories.Launch, $"Scene {LogStyle.Name("Battle")} {LogStyle.Ok("opened")}");
            }
            finally
            {
                _isEnteringBattle = false;
            }
        }
    }
}
