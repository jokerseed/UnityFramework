using Framework.FixedMath;
using Framework.GAS.Events;

namespace Framework.GAS.Abilities.Tasks
{
    /// <summary>
    /// 技能任务基类，由 <see cref="ActiveAbilityInstance"/> 在 ASC Tick 中驱动。
    /// 子类实现 <see cref="OnActivate"/> / <see cref="Tick"/> / <see cref="OnCancel"/>。
    /// </summary>
    public abstract class AbilityTask
    {
        /// <summary>所属激活实例。</summary>
        protected ActiveAbilityInstance Instance { get; private set; }

        /// <summary>任务是否已完成。</summary>
        public bool IsDone { get; protected set; }

        /// <summary>任务是否已被取消。</summary>
        public bool IsCancelled { get; private set; }

        internal void Bind(ActiveAbilityInstance instance) => Instance = instance;

        internal void ActivateTask() => OnActivate();

        /// <summary>任务启动时调用。</summary>
        protected virtual void OnActivate() { }

        /// <summary>每帧 Tick；默认无操作。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public virtual void Tick(FP deltaTime) { }

        internal void CancelTask()
        {
            if (IsCancelled || IsDone)
            {
                return;
            }

            IsCancelled = true;
            OnCancel();
        }

        /// <summary>任务被取消时调用。</summary>
        protected virtual void OnCancel() { }

        /// <summary>收到 GameplayEvent 时调用；默认忽略。</summary>
        /// <param name="eventData">事件数据。</param>
        protected virtual void OnGameplayEvent(in GameplayEventData eventData) { }

        internal void HandleGameplayEvent(in GameplayEventData eventData) =>
            OnGameplayEvent(eventData);

        /// <summary>标记任务完成。</summary>
        protected void Finish() => IsDone = true;
    }
}
