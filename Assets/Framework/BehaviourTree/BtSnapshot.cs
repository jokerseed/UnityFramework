using System;

namespace Framework.BehaviourTree
{
    /// <summary>单帧行为树可还原快照（运行时槽 + 权威黑板 + 帧号 + 随机源）。</summary>
    public sealed class BtSnapshot
    {
        /// <summary>创建快照容器。不带随机源时恢复不会改写上下文里的 <c>TSRandom</c>。</summary>
        /// <param name="frameIndex">逻辑帧号。</param>
        /// <param name="lastStatus">树根上一帧状态。</param>
        /// <param name="runtime">运行时槽副本；不可为 null。</param>
        /// <param name="blackboard">权威黑板副本；不可为 null。</param>
        /// <param name="randomState"><see cref="Framework.FixedMath.TSRandom"/> 状态表；为 null 表示不还原随机源。</param>
        /// <param name="randomIndex">状态表下标。</param>
        /// <exception cref="ArgumentNullException">runtime 或 blackboard 为 null。</exception>
        public BtSnapshot(
            int frameIndex,
            BtStatus lastStatus,
            BtRuntime runtime,
            BtBlackboard blackboard,
            uint[] randomState = null,
            int randomIndex = 0)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
            FrameIndex = frameIndex;
            LastStatus = lastStatus;
            RandomIndex = randomIndex;
            if (randomState != null)
            {
                RandomState = new uint[randomState.Length];
                Array.Copy(randomState, RandomState, randomState.Length);
            }
        }

        /// <summary>逻辑帧号。</summary>
        public int FrameIndex { get; }

        /// <summary>根节点状态。</summary>
        public BtStatus LastStatus { get; }

        /// <summary>运行时槽副本。</summary>
        public BtRuntime Runtime { get; }

        /// <summary>权威黑板副本（不含 object 袋）。</summary>
        public BtBlackboard Blackboard { get; }

        /// <summary>随机源状态表；为 null 时恢复不改写随机源。</summary>
        public uint[] RandomState { get; }

        /// <summary>随机源状态下标。</summary>
        public int RandomIndex { get; }
    }
}
