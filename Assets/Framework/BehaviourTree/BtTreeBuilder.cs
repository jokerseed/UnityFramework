using System;
using Framework.FixedMath;

namespace Framework.BehaviourTree
{
    /// <summary>代码侧流畅构建行为树。构建结果供单个 Agent 使用。</summary>
    public sealed class BtTreeBuilder
    {
        BtNode _root;

        /// <summary>设置根节点。</summary>
        /// <param name="root">根节点；不可为 null。</param>
        /// <returns>构建器自身。</returns>
        public BtTreeBuilder Root(BtNode root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            return this;
        }

        /// <summary>生成行为树实例。</summary>
        /// <param name="name">调试名；可为 null。</param>
        /// <returns>新的 BehaviourTree。</returns>
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

        /// <summary>创建主动选择器。</summary>
        /// <returns>新选择器。</returns>
        public static BtActiveSelector ActiveSelector() => new BtActiveSelector();

        /// <summary>创建随机选择器。</summary>
        /// <returns>新选择器。</returns>
        public static BtRandomSelector RandomSelector() => new BtRandomSelector();

        /// <summary>创建加权选择器。</summary>
        /// <param name="weights">权重；可为 null。</param>
        /// <returns>新选择器。</returns>
        public static BtWeightedSelector WeightedSelector(int[] weights = null) =>
            new BtWeightedSelector(weights);

        /// <summary>创建并行节点。</summary>
        /// <param name="policy">汇总策略。</param>
        /// <param name="failFast">失败是否立刻结束。</param>
        /// <param name="succeedFast">成功是否立刻结束。</param>
        /// <returns>新并行节点。</returns>
        public static BtParallel Parallel(
            BtParallelPolicy policy = BtParallelPolicy.RequireAll,
            bool failFast = true,
            bool succeedFast = true) =>
            new BtParallel(policy, failFast, succeedFast);

        /// <summary>创建取反装饰。</summary>
        /// <param name="child">子节点。</param>
        /// <returns>装饰节点。</returns>
        public static BtInverter Invert(BtNode child) => new BtInverter(child);

        /// <summary>创建重复装饰。</summary>
        /// <param name="child">子节点。</param>
        /// <param name="times">次数；&lt;=0 无限。</param>
        /// <param name="repeatOnFailure">失败是否重试。</param>
        /// <returns>装饰节点。</returns>
        public static BtRepeater Repeat(BtNode child, int times = 0, bool repeatOnFailure = false) =>
            new BtRepeater(child, times, repeatOnFailure);

        /// <summary>创建强制成功装饰。</summary>
        /// <param name="child">子节点。</param>
        /// <returns>装饰节点。</returns>
        public static BtForceSuccess AlwaysSucceed(BtNode child) => new BtForceSuccess(child);

        /// <summary>创建强制失败装饰。</summary>
        /// <param name="child">子节点。</param>
        /// <returns>装饰节点。</returns>
        public static BtForceFailure AlwaysFail(BtNode child) => new BtForceFailure(child);

        /// <summary>创建直到成功装饰。</summary>
        /// <param name="child">子节点。</param>
        /// <returns>装饰节点。</returns>
        public static BtUntilSuccess UntilSuccess(BtNode child) => new BtUntilSuccess(child);

        /// <summary>创建冷却装饰。</summary>
        /// <param name="child">子节点。</param>
        /// <param name="duration">冷却时长。</param>
        /// <returns>装饰节点。</returns>
        public static BtCooldown Cooldown(BtNode child, FP duration) => new BtCooldown(child, duration);

        /// <summary>创建超时装饰。</summary>
        /// <param name="child">子节点。</param>
        /// <param name="duration">最大 Running 时长。</param>
        /// <returns>装饰节点。</returns>
        public static BtTimeout Timeout(BtNode child, FP duration) => new BtTimeout(child, duration);

        /// <summary>创建时限装饰。</summary>
        /// <param name="child">子节点。</param>
        /// <param name="duration">最大 Running 时长。</param>
        /// <returns>装饰节点。</returns>
        public static BtTimeLimit TimeLimit(BtNode child, FP duration) => new BtTimeLimit(child, duration);

        /// <summary>创建按帧等待。</summary>
        /// <param name="frames">逻辑帧数。</param>
        /// <returns>叶子节点。</returns>
        public static BtWaitFrames WaitFrames(int frames) => new BtWaitFrames(frames);

        /// <summary>创建按定点时间等待。</summary>
        /// <param name="duration">时长。</param>
        /// <returns>叶子节点。</returns>
        public static BtWaitTime WaitTime(FP duration) => new BtWaitTime(duration);

        /// <summary>创建委托动作。</summary>
        /// <param name="tick">Tick 回调。</param>
        /// <param name="onReset">Reset 回调。</param>
        /// <param name="onAbort">Abort 回调。</param>
        /// <returns>叶子节点。</returns>
        public static BtAction Action(
            Func<BtContext, BtStatus> tick,
            Action<BtContext> onReset = null,
            Action<BtContext> onAbort = null) =>
            new BtAction(tick, onReset, onAbort);

        /// <summary>创建委托条件。</summary>
        /// <param name="predicate">条件。</param>
        /// <returns>叶子节点。</returns>
        public static BtCondition Condition(Func<BtContext, bool> predicate) =>
            new BtCondition(predicate);
    }
}
