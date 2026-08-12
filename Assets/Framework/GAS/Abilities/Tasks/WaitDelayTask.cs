using System;

namespace Framework.GAS.Abilities.Tasks
{
    /// <summary>等待指定秒数后执行回调。</summary>
    public sealed class WaitDelayTask : AbilityTask
    {
        readonly float _duration;
        readonly Action _onComplete;
        float _elapsed;

        /// <summary>构造延迟 Task。</summary>
        /// <param name="duration">等待秒数。</param>
        /// <param name="onComplete">完成回调。</param>
        public WaitDelayTask(float duration, Action onComplete)
        {
            _duration = duration;
            _onComplete = onComplete;
        }

        /// <inheritdoc/>
        public override void Tick(float deltaTime)
        {
            if (IsDone || IsCancelled)
            {
                return;
            }

            _elapsed += deltaTime;
            if (_elapsed >= _duration)
            {
                _onComplete?.Invoke();
                Finish();
            }
        }

        /// <inheritdoc/>
        protected override void OnCancel() => Finish();
    }
}
