using System;
using Framework.Config;
using Framework.GamePlay;
using Framework.Logging;

namespace Game
{
    /// <summary>
    /// 战斗流程编排器：负责申请 / 持有 / 释放一场战斗对应的 <see cref="BattleSession"/>。
    /// 场景表现层通过该流程获取 Session，避免直接依赖 <see cref="GamePlayModule"/>。
    /// </summary>
    public sealed class BattleFlow : IDisposable
    {
        bool _disposed;

        /// <summary>
        /// 当前流程状态。
        /// </summary>
        public BattleFlowState State { get; private set; } = BattleFlowState.Idle;

        /// <summary>
        /// 当前战斗会话；未进入战斗时为 null。
        /// </summary>
        public BattleSession Session { get; private set; }

        /// <summary>
        /// 尝试进入战斗：加载配置表并向 <see cref="BattleDirector"/> 申请会话。
        /// </summary>
        /// <returns>成功进入返回 <see langword="true"/>。</returns>
        public bool TryEnter()
        {
            ThrowIfDisposed();
            if (State == BattleFlowState.Running && Session != null)
            {
                return true;
            }

            State = BattleFlowState.Preparing;
            var module = GamePlayModule.Instance;
            if (module?.Director == null)
            {
                State = BattleFlowState.Failed;
                GameLog.Error(LogCategories.GamePlay, "GamePlayModule / Director is not ready.");
                return false;
            }

            if (!ConfigManager.HasInstance)
            {
                State = BattleFlowState.Failed;
                GameLog.Error(LogCategories.GamePlay, "ConfigManager is not ready.");
                return false;
            }

            try
            {
                ConfigManager.Instance.LoadTables();
                Session = module.Director.CreateSession();
                State = BattleFlowState.Running;
                GameLog.Info(
                    LogCategories.GamePlay,
                    $"BattleFlow entered  session={LogStyle.Value(Session.SessionId.ToString())}");
                return true;
            }
            catch (Exception ex)
            {
                State = BattleFlowState.Failed;
                GameLog.Error(LogCategories.GamePlay, $"BattleFlow enter failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 结束战斗并释放持有的会话。
        /// </summary>
        public void Exit()
        {
            if (_disposed || State == BattleFlowState.Exited)
            {
                return;
            }

            if (Session != null)
            {
                GamePlayModule.Instance?.Director?.DestroySession(Session);
                Session = null;
            }

            State = BattleFlowState.Exited;
            GameLog.Info(LogCategories.GamePlay, "BattleFlow exited");
        }

        /// <summary>
        /// 释放流程对象。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Exit();
            _disposed = true;
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BattleFlow));
            }
        }
    }
}
