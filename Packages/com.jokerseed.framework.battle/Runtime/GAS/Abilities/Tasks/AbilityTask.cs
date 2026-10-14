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

        /// <summary>把会影响后续模拟的进度混成稳定整数。默认只含完成和取消标记。</summary>
        /// <returns>进度哈希。</returns>
        public virtual int ProgressHash()
        {
            unchecked
            {
                var hash = (int)2166136261;
                hash = (hash * 16777619) ^ (IsDone ? 1 : 0);
                hash = (hash * 16777619) ^ (IsCancelled ? 1 : 0);
                return hash;
            }
        }

        /// <summary>按快照写回完成与取消标记，不调用取消回调。</summary>
        /// <param name="isDone">是否已完成。</param>
        /// <param name="isCancelled">是否已取消。</param>
        internal void RestoreFlags(bool isDone, bool isCancelled)
        {
            IsDone = isDone;
            IsCancelled = isCancelled;
        }
    }
}
