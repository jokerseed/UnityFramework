namespace Framework.Fsm
{
    /// <summary>
    /// 状态机状态基类。同一状态实例只属于一个 <see cref="Fsm{TOwner}"/>，状态间共享的数据放在持有者或 <see cref="Fsm{TOwner}.SetData"/> 里。
    /// </summary>
    /// <typeparam name="TOwner">状态机持有者类型。</typeparam>
    public abstract class FsmState<TOwner> where TOwner : class
    {
        /// <summary>状态机创建时调用一次。</summary>
        /// <param name="fsm">所属状态机。</param>
        protected virtual void OnInit(Fsm<TOwner> fsm)
        {
        }

        /// <summary>进入本状态时调用；可在此直接切换到下一个状态。</summary>
        /// <param name="fsm">所属状态机。</param>
        protected virtual void OnEnter(Fsm<TOwner> fsm)
        {
        }

        /// <summary>每次 <see cref="Fsm{TOwner}.Update"/> 时调用。</summary>
        /// <param name="fsm">所属状态机。</param>
        /// <param name="deltaTime">距上次更新的时间（秒）。</param>
        protected virtual void OnUpdate(Fsm<TOwner> fsm, float deltaTime)
        {
        }

        /// <summary>离开本状态时调用；此时不允许再切换状态。</summary>
        /// <param name="fsm">所属状态机。</param>
        protected virtual void OnExit(Fsm<TOwner> fsm)
        {
        }

        /// <summary>状态机销毁时调用一次。</summary>
        /// <param name="fsm">所属状态机。</param>
        protected virtual void OnDestroy(Fsm<TOwner> fsm)
        {
        }

        internal void InvokeInit(Fsm<TOwner> fsm) => OnInit(fsm);

        internal void InvokeEnter(Fsm<TOwner> fsm) => OnEnter(fsm);

        internal void InvokeUpdate(Fsm<TOwner> fsm, float deltaTime) => OnUpdate(fsm, deltaTime);

        internal void InvokeExit(Fsm<TOwner> fsm) => OnExit(fsm);

        internal void InvokeDestroy(Fsm<TOwner> fsm) => OnDestroy(fsm);

        /// <summary>切换到指定类型的状态。</summary>
        /// <typeparam name="TState">目标状态类型，须已注册到状态机。</typeparam>
        /// <param name="fsm">所属状态机。</param>
        protected void ChangeState<TState>(Fsm<TOwner> fsm) where TState : FsmState<TOwner>
        {
            fsm.ChangeState<TState>();
        }
    }
}
