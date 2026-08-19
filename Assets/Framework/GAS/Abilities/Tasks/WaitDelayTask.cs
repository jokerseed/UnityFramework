using System;
using Framework.FixedMath;

namespace Framework.GAS.Abilities.Tasks
{
    /// <summary>等待指定秒数后执行回调。</summary>
    public sealed class WaitDelayTask : AbilityTask
    {
        readonly FP _duration;
        readonly Action _onComplete;
        FP _elapsed;

        /// <summary>构造延迟 Task。</summary>
        /// <param name="duration">等待秒数。</param>
        /// <param name="onComplete">完成回调。</param>
        public WaitDelayTask(FP duration, Action onComplete)
        {
            _duration = duration;
            _onComplete = onComplete;
        }

        /// <inheritdoc/>
        public override void Tick(FP deltaTime)
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
