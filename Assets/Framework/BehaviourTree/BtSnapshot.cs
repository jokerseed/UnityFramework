using System;

namespace Framework.BehaviourTree
{
    /// <summary>单帧行为树可还原快照（运行时槽 + 权威黑板 + 帧号）。</summary>
    public sealed class BtSnapshot
    {
        /// <summary>创建快照容器。</summary>
        /// <param name="frameIndex">逻辑帧号。</param>
        /// <param name="lastStatus">树根上一帧状态。</param>
        /// <param name="runtime">运行时槽副本；不可为 null。</param>
        /// <param name="blackboard">权威黑板副本；不可为 null。</param>
        /// <exception cref="ArgumentNullException">runtime 或 blackboard 为 null。</exception>
        public BtSnapshot(int frameIndex, BtStatus lastStatus, BtRuntime runtime, BtBlackboard blackboard)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
            FrameIndex = frameIndex;
            LastStatus = lastStatus;
        }

        /// <summary>逻辑帧号。</summary>
        public int FrameIndex { get; }

        /// <summary>根节点状态。</summary>
        public BtStatus LastStatus { get; }

        /// <summary>运行时槽副本。</summary>
        public BtRuntime Runtime { get; }

        /// <summary>权威黑板副本（不含 object 袋）。</summary>
        public BtBlackboard Blackboard { get; }
    }
}
