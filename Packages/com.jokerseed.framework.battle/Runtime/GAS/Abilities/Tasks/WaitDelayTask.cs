using System;
using Framework.Battle.Snapshot;
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

        /// <inheritdoc />
        public override int ProgressHash()
        {
            unchecked
            {
                var hash = base.ProgressHash();
                hash = (hash * 16777619) ^ _elapsed.RawValue.GetHashCode();
                hash = (hash * 16777619) ^ _duration.RawValue.GetHashCode();
                return hash;
            }
        }

        /// <summary>写入已经过的时间与完成标记。</summary>
        /// <param name="writer">写入器。</param>
        internal void WriteProgress(SnapshotWriter writer)
        {
            writer.WriteFp(_elapsed);
            writer.WriteBool(IsDone);
            writer.WriteBool(IsCancelled);
        }

        /// <summary>读回已经过的时间与完成标记，不重置回调。</summary>
        /// <param name="reader">读取器。</param>
        internal void ReadProgress(SnapshotReader reader)
        {
            _elapsed = reader.ReadFp();
            RestoreFlags(reader.ReadBool(), reader.ReadBool());
        }
    }
}
