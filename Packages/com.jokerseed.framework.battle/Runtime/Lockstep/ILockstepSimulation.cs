namespace Framework.Lockstep
{
    /// <summary>
    /// 锁步驱动的模拟。调用方按固定步长调用 <see cref="Simulate"/>，并用检查点支持回滚。
    /// 检查点编号是“该步尚未执行”的状态。
    /// </summary>
    public interface ILockstepSimulation
    {
        /// <summary>保存该步开始前的状态。同一编号再次调用会覆盖。</summary>
        /// <param name="tick">逻辑步，从 0 起。</param>
        void SaveCheckpoint(int tick);

        /// <summary>恢复到该步开始前的状态。</summary>
        /// <param name="tick">先前保存过的逻辑步。</param>
        void RestoreCheckpoint(int tick);

        /// <summary>不大于 <paramref name="tick"/> 的最近检查点。</summary>
        /// <param name="tick">需要回退到的逻辑步。</param>
        /// <returns>检查点编号。</returns>
        int LatestCheckpointAtOrBefore(int tick);

        /// <summary>用这一步各玩家的输入推进恰好一帧。</summary>
        /// <param name="tick">逻辑步。</param>
        /// <param name="playerIds">玩家 ID，与 <paramref name="payloads"/> 下标对齐。</param>
        /// <param name="payloads">各玩家输入；与玩家一一对应。</param>
        void Simulate(int tick, int[] playerIds, byte[][] payloads);

        /// <summary>当前模拟的校验和。</summary>
        long Checksum { get; }
    }

    /// <summary>
    /// 可选：模拟实现它以后，<see cref="LockstepHost"/> 会在回滚窗口前移时通知丢弃旧检查点。
    /// 不实现时检查点一直保留，长局内存随时长增长。
    /// 单独成接口而不是默认接口方法，是因为 Unity 2021.3 的 Mono / IL2CPP 对默认接口方法的支持不可靠。
    /// </summary>
    public interface ILockstepCheckpointPruning
    {
        /// <summary>丢弃编号小于 <paramref name="tick"/> 的检查点。编号等于 <paramref name="tick"/> 的那份必须保留。</summary>
        /// <param name="tick">仍可能回滚到的最早检查点。</param>
        void DiscardCheckpointsBefore(int tick);
    }
}
