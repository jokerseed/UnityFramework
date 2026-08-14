using System;
using Framework.FixedMath;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 单 Agent 的行为树运行时槽。拓扑在 <see cref="BtTreeTemplate"/>，进度只写在本对象。
    /// </summary>
    public sealed class BtRuntime
    {
        readonly BtStatus[] _status;
        readonly int[] _ints;
        readonly long[] _fpRaw;
        readonly byte[] _flags;

        /// <summary>按节点数量创建空运行时。</summary>
        /// <param name="nodeCount">模板节点数；必须 &gt;= 0。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="nodeCount"/> 为负数。</exception>
        public BtRuntime(int nodeCount)
        {
            if (nodeCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeCount));
            }

            NodeCount = nodeCount;
            _status = new BtStatus[nodeCount];
            _ints = new int[nodeCount];
            _fpRaw = new long[nodeCount];
            _flags = new byte[nodeCount];
        }

        /// <summary>节点槽位数，与模板一致。</summary>
        public int NodeCount { get; }

        /// <summary>读取节点状态；越界返回 Failure。</summary>
        /// <param name="nodeIndex">节点下标。</param>
        /// <returns>已记录状态。</returns>
        public BtStatus GetStatus(int nodeIndex) =>
            IsValid(nodeIndex) ? _status[nodeIndex] : BtStatus.Failure;

        /// <summary>写入节点状态。</summary>
        /// <param name="nodeIndex">节点下标。</param>
        /// <param name="status">状态。</param>
        public void SetStatus(int nodeIndex, BtStatus status)
        {
            if (IsValid(nodeIndex))
            {
                _status[nodeIndex] = status;
            }
        }

        /// <summary>读取整型槽。</summary>
        /// <param name="nodeIndex">节点下标。</param>
        /// <returns>整型值；越界为 0。</returns>
        public int GetInt(int nodeIndex) => IsValid(nodeIndex) ? _ints[nodeIndex] : 0;

        /// <summary>写入整型槽。</summary>
        /// <param name="nodeIndex">节点下标。</param>
        /// <param name="value">值。</param>
        public void SetInt(int nodeIndex, int value)
        {
            if (IsValid(nodeIndex))
            {
                _ints[nodeIndex] = value;
            }
        }

        /// <summary>读取定点槽。</summary>
        /// <param name="nodeIndex">节点下标。</param>
        /// <returns>定点数；越界为 0。</returns>
        public FP GetFp(int nodeIndex) =>
            IsValid(nodeIndex) ? FP.FromRaw(_fpRaw[nodeIndex]) : FP.Zero;

        /// <summary>写入定点槽。</summary>
        /// <param name="nodeIndex">节点下标。</param>
        /// <param name="value">定点数。</param>
        public void SetFp(int nodeIndex, FP value)
        {
            if (IsValid(nodeIndex))
            {
                _fpRaw[nodeIndex] = value.RawValue;
            }
        }

        /// <summary>节点是否已进入 Started。</summary>
        /// <param name="nodeIndex">节点下标。</param>
        /// <returns>已开始则为 true。</returns>
        public bool IsStarted(int nodeIndex) =>
            IsValid(nodeIndex) && (_flags[nodeIndex] & 1) != 0;

        /// <summary>设置 Started 标记。</summary>
        /// <param name="nodeIndex">节点下标。</param>
        /// <param name="started">是否已开始。</param>
        public void SetStarted(int nodeIndex, bool started)
        {
            if (!IsValid(nodeIndex))
            {
                return;
            }

            if (started)
            {
                _flags[nodeIndex] |= 1;
            }
            else
            {
                _flags[nodeIndex] &= unchecked((byte)~1);
            }
        }

        /// <summary>清空全部槽位。</summary>
        public void ResetAll()
        {
            Array.Clear(_status, 0, _status.Length);
            Array.Clear(_ints, 0, _ints.Length);
            Array.Clear(_fpRaw, 0, _fpRaw.Length);
            Array.Clear(_flags, 0, _flags.Length);
        }

        /// <summary>深拷贝运行时槽。</summary>
        /// <returns>独立副本。</returns>
        public BtRuntime Clone()
        {
            var copy = new BtRuntime(NodeCount);
            copy.CopyFrom(this);
            return copy;
        }

        /// <summary>从另一运行时拷贝槽位；节点数必须相同。</summary>
        /// <param name="source">来源；不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 null。</exception>
        /// <exception cref="ArgumentException">节点数不一致。</exception>
        public void CopyFrom(BtRuntime source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.NodeCount != NodeCount)
            {
                throw new ArgumentException("BtRuntime node count mismatch.", nameof(source));
            }

            Array.Copy(source._status, _status, NodeCount);
            Array.Copy(source._ints, _ints, NodeCount);
            Array.Copy(source._fpRaw, _fpRaw, NodeCount);
            Array.Copy(source._flags, _flags, NodeCount);
        }

        bool IsValid(int nodeIndex) => (uint)nodeIndex < (uint)NodeCount;
    }
}
