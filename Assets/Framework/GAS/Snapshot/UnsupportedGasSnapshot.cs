using System;
using Framework.Core.Snapshot;

namespace Framework.GAS.Snapshot
{
    /// <summary>
    /// GAS 快照扩展点。当前不能安全恢复，因此 Save / Restore 会抛出
    /// <see cref="NotSupportedException"/>，避免半份状态被当成可回滚。
    /// 预测回滚不在本次范围。
    /// 实现时应按 ActorId 保存并恢复：属性 Base/Current、标签计数、
    /// 活跃效果（EffectId、剩余时间、周期计时、层数、来源）、冷却（现为效果）、
    /// 已授予技能与激活实例。只回写属性当前值而不重放 Modifier 会导致下一帧被效果 Tick 覆盖。
    /// </summary>
    public sealed class UnsupportedGasSnapshot : ISnapshotable
    {
        /// <inheritdoc />
        /// <exception cref="NotSupportedException">GAS 快照尚未实现。</exception>
        public void Save(SnapshotWriter writer) =>
            throw new NotSupportedException(
                "GAS snapshot is not implemented. Persist attributes, tags, effects, cooldowns and abilities together.");

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">GAS 快照尚未实现。</exception>
        public void Restore(SnapshotReader reader) =>
            throw new NotSupportedException(
                "GAS snapshot is not implemented. Restore must rebuild modifiers, not only current attribute values.");
    }
}
