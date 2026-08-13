using System;
using Framework.FixedMath;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 单次行为树 Tick 的上下文。必须由逻辑帧驱动，禁止填入 <c>Time.deltaTime</c>。
    /// </summary>
    public sealed class BtContext
    {
        /// <summary>
        /// 创建上下文。
        /// </summary>
        /// <param name="blackboard">黑板；不可为 null。多个 Agent 可共享或各自持有。</param>
        /// <param name="owner">宿主对象（如怪物控制器）；可为 null。</param>
        /// <param name="random">确定性随机；可为 null（节点勿调用随机）。</param>
        /// <exception cref="ArgumentNullException"><paramref name="blackboard"/> 为 null。</exception>
        public BtContext(BtBlackboard blackboard, object owner = null, TSRandom random = null)
        {
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
            Owner = owner;
            Random = random;
        }

        /// <summary>当前逻辑帧序号（由外部推进）。</summary>
        public int FrameIndex { get; set; }

        /// <summary>本逻辑帧固定步长（与锁步 <c>lockedTimeStep</c> 一致）。</summary>
        public FP DeltaTime { get; set; }

        /// <summary>黑板。</summary>
        public BtBlackboard Blackboard { get; }

        /// <summary>宿主对象；业务节点自行转型。</summary>
        public object Owner { get; set; }

        /// <summary>确定性随机源；须用 <see cref="TSRandom.New"/> 创建。</summary>
        public TSRandom Random { get; set; }

        /// <summary>
        /// 推进到下一逻辑帧。
        /// </summary>
        /// <param name="deltaTime">固定步长。</param>
        public void AdvanceFrame(FP deltaTime)
        {
            FrameIndex++;
            DeltaTime = deltaTime;
        }

        /// <summary>
        /// 按类型获取宿主。
        /// </summary>
        /// <typeparam name="T">宿主类型。</typeparam>
        /// <returns>转型后的宿主；失败则为 null。</returns>
        public T GetOwner<T>() where T : class => Owner as T;
    }
}
