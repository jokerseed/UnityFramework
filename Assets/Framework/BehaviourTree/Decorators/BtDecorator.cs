using System;

namespace Framework.BehaviourTree
{
    /// <summary>装饰节点基类（单一子节点）。</summary>
    public abstract class BtDecorator : BtNode
    {
        /// <summary>
        /// 创建装饰节点。
        /// </summary>
        /// <param name="child">子节点；不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="child"/> 为 null。</exception>
        protected BtDecorator(BtNode child)
        {
            Child = child ?? throw new ArgumentNullException(nameof(child));
        }

        /// <summary>被装饰的子节点。</summary>
        public BtNode Child { get; }

        /// <inheritdoc />
        public override void Reset(BtContext context)
        {
            Child.Reset(context);
        }

        /// <inheritdoc />
        public override void Abort(BtContext context)
        {
            Child.Abort(context);
            Reset(context);
        }
    }
}
