using System;
using Framework.Core;
using Framework.Logging;
using UnityEngine;

namespace Framework.Fsm
{
    /// <summary>
    /// 流程管理器：用一个 <see cref="Fsm{TOwner}"/> 管理全部 <see cref="ProcedureBase"/>，在 Unity Update 中驱动当前流程。
    /// 先 <see cref="Initialize"/> 注册流程，等启动组 Ready 后再 <see cref="StartProcedure{TProcedure}"/>。
    /// </summary>
    public sealed class ProcedureManager : PersistentSingleton<ProcedureManager>
    {
        Fsm<ProcedureManager> _fsm;

        /// <summary>是否已注册流程。</summary>
        public bool IsInitialized => _fsm != null;

        /// <summary>当前流程；未启动时为 null。</summary>
        public ProcedureBase CurrentProcedure => (ProcedureBase)_fsm?.CurrentState;

        /// <summary>进入当前流程后经过的时间（秒，受 timeScale 影响）。</summary>
        public float CurrentProcedureTime => _fsm?.CurrentStateTime ?? 0f;

        /// <summary>流程切换后触发，参数为（旧流程，新流程）；首次启动时旧流程为 null。</summary>
        public event Action<ProcedureBase, ProcedureBase> ProcedureChanged;

        /// <summary>注册全部流程。</summary>
        /// <param name="procedures">流程实例；同一类型只能出现一次。</param>
        /// <exception cref="InvalidOperationException">已注册过。</exception>
        public void Initialize(params ProcedureBase[] procedures)
        {
            if (_fsm != null)
            {
                throw new InvalidOperationException("ProcedureManager is already initialized. Call Shutdown first.");
            }

            _fsm = new Fsm<ProcedureManager>("Procedure", this, procedures);
            _fsm.StateChanged += OnStateChanged;
        }

        /// <summary>以指定流程启动。</summary>
        /// <typeparam name="TProcedure">入口流程类型。</typeparam>
        /// <exception cref="InvalidOperationException">未注册流程、已启动或流程未注册。</exception>
        public void StartProcedure<TProcedure>() where TProcedure : ProcedureBase
        {
            EnsureInitialized();
            _fsm.Start<TProcedure>();
        }

        /// <summary>切换到指定流程。</summary>
        /// <typeparam name="TProcedure">目标流程类型。</typeparam>
        /// <exception cref="InvalidOperationException">未注册、未启动或流程未注册。</exception>
        public void ChangeProcedure<TProcedure>() where TProcedure : ProcedureBase
        {
            EnsureInitialized();
            _fsm.ChangeState<TProcedure>();
        }

        /// <summary>获取已注册的流程实例。</summary>
        /// <typeparam name="TProcedure">流程类型。</typeparam>
        /// <returns>流程实例。</returns>
        /// <exception cref="InvalidOperationException">未注册或流程未注册。</exception>
        public TProcedure GetProcedure<TProcedure>() where TProcedure : ProcedureBase
        {
            EnsureInitialized();
            return _fsm.GetState<TProcedure>();
        }

        /// <summary>退出当前流程并销毁状态机；之后可重新 <see cref="Initialize"/>。</summary>
        public void Shutdown()
        {
            if (_fsm == null)
            {
                return;
            }

            _fsm.Destroy();
            _fsm = null;
        }

        void Update()
        {
            _fsm?.Update(Time.deltaTime);
        }

        void OnStateChanged(FsmState<ProcedureManager> previous, FsmState<ProcedureManager> next)
        {
            GameLog.Info(
                LogCategories.Procedure,
                $"{LogStyle.Name(previous?.GetType().Name ?? "-")} → {LogStyle.Name(next.GetType().Name)}");
            ProcedureChanged?.Invoke((ProcedureBase)previous, (ProcedureBase)next);
        }

        void EnsureInitialized()
        {
            if (_fsm == null)
            {
                throw new InvalidOperationException("ProcedureManager is not initialized. Add ProcedureModule or call Initialize first.");
            }
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            Shutdown();
            base.OnDestroy();
        }
    }
}
