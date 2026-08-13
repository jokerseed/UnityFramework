using System;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 行为树运行时宿主。一棵树实例对应一个 Agent；每逻辑帧调用一次 <see cref="Tick"/>。
    /// </summary>
    public sealed class BehaviourTree
    {
        /// <summary>
        /// 创建行为树。
        /// </summary>
        /// <param name="root">根节点；不可为 null。</param>
        /// <param name="name">调试名；可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> 为 null。</exception>
        public BehaviourTree(BtNode root, string name = null)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Name = name;
        }

        /// <summary>根节点。</summary>
        public BtNode Root { get; }

        /// <summary>调试名。</summary>
        public string Name { get; }

        /// <summary>上一帧 Tick 结果。</summary>
        public BtStatus LastStatus { get; private set; } = BtStatus.Failure;

        /// <summary>
        /// 推进一逻辑帧。
        /// </summary>
        /// <param name="context">上下文；不可为 null。</param>
        /// <returns>本帧根节点状态。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 null。</exception>
        public BtStatus Tick(BtContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            LastStatus = Root.Tick(context);
            return LastStatus;
        }

        /// <summary>
        /// 重置整棵树运行时状态。
        /// </summary>
        /// <param name="context">上下文；不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 null。</exception>
        public void Reset(BtContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Root.Reset(context);
            LastStatus = BtStatus.Failure;
        }

        /// <summary>
        /// 中止当前执行并重置。
        /// </summary>
        /// <param name="context">上下文；不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 null。</exception>
        public void Abort(BtContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Root.Abort(context);
            LastStatus = BtStatus.Failure;
        }
    }
}
