using System;
using Framework.FixedMath;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 代码侧流畅构建行为树。构建结果供单个 Agent 使用。
    /// </summary>
    public sealed class BtTreeBuilder
    {
        BtNode _root;

        /// <summary>
        /// 设置根节点。
        /// </summary>
        /// <param name="root">根节点；不可为 null。</param>
        /// <returns>构建器自身。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> 为 null。</exception>
        public BtTreeBuilder Root(BtNode root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            return this;
        }

        /// <summary>
        /// 生成行为树实例。
        /// </summary>
        /// <param name="name">调试名；可为 null。</param>
        /// <returns>新的 <see cref="BehaviourTree"/>。</returns>
        /// <exception cref="InvalidOperationException">尚未设置根节点。</exception>
        public BehaviourTree Build(string name = null)
        {
            if (_root == null)
            {
                throw new InvalidOperationException("Behaviour tree root is not set.");
            }

            return new BehaviourTree(_root, name);
        }

        /// <summary>创建空的顺序节点。</summary>
        /// <returns>新序列。</returns>
        public static BtSequence Sequence() => new BtSequence();

        /// <summary>创建空的选择节点。</summary>
        /// <returns>新选择器。</returns>
        public static BtSelector Selector() => new BtSelector();

        /// <summary>
        /// 创建并行节点。
        /// </summary>
        /// <param name="policy">汇总策略。</param>
        /// <returns>新并行节点。</returns>
        public static BtParallel Parallel(BtParallelPolicy policy = BtParallelPolicy.RequireAll)
            => new BtParallel(policy);

        /// <summary>
        /// 创建取反装饰。
        /// </summary>
        /// <param name="child">子节点。</param>
        /// <returns>装饰节点。</returns>
        public static BtInverter Invert(BtNode child) => new BtInverter(child);

        /// <summary>
        /// 创建重复装饰。
        /// </summary>
        /// <param name="child">子节点。</param>
        /// <param name="times">次数；&lt;=0 无限。</param>
        /// <returns>装饰节点。</returns>
        public static BtRepeater Repeat(BtNode child, int times = 0) => new BtRepeater(child, times);

        /// <summary>
        /// 创建强制成功装饰。
        /// </summary>
        /// <param name="child">子节点。</param>
        /// <returns>装饰节点。</returns>
        public static BtForceSuccess AlwaysSucceed(BtNode child) => new BtForceSuccess(child);

        /// <summary>
        /// 创建按帧等待。
        /// </summary>
        /// <param name="frames">逻辑帧数。</param>
        /// <returns>叶子节点。</returns>
        public static BtWaitFrames WaitFrames(int frames) => new BtWaitFrames(frames);

        /// <summary>
        /// 创建按定点时间等待。
        /// </summary>
        /// <param name="duration">时长。</param>
        /// <returns>叶子节点。</returns>
        public static BtWaitTime WaitTime(FP duration) => new BtWaitTime(duration);

        /// <summary>
        /// 创建委托动作。
        /// </summary>
        /// <param name="tick">Tick 回调。</param>
        /// <param name="onReset">Reset 回调。</param>
        /// <returns>叶子节点。</returns>
        public static BtAction Action(Func<BtContext, BtStatus> tick, Action<BtContext> onReset = null)
            => new BtAction(tick, onReset);

        /// <summary>
        /// 创建委托条件。
        /// </summary>
        /// <param name="predicate">条件。</param>
        /// <returns>叶子节点。</returns>
        public static BtCondition Condition(Func<BtContext, bool> predicate)
            => new BtCondition(predicate);
    }
}
