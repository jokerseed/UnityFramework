using System;
using System.Collections.Generic;

namespace Framework.BehaviourTree
{
    /// <summary>拥有有序子节点的组合节点基类。</summary>
    public abstract class BtComposite : BtNode
    {
        readonly List<BtNode> _children = new List<BtNode>();

        /// <summary>子节点只读列表（稳定插入顺序）。</summary>
        public IReadOnlyList<BtNode> Children => _children;

        /// <inheritdoc />
        public override int ChildCount => _children.Count;

        /// <inheritdoc />
        public override BtNode GetChild(int index) =>
            (uint)index < (uint)_children.Count ? _children[index] : null;

        /// <summary>追加子节点。</summary>
        /// <param name="child">子节点；不可为 null。</param>
        /// <returns>当前组合节点，便于链式调用。</returns>
        public BtComposite AddChild(BtNode child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            _children.Add(child);
            return this;
        }

        /// <inheritdoc />
        public override void Reset(BtContext context)
        {
            ClearSlot(context);
            for (var i = 0; i < _children.Count; i++)
            {
                _children[i].Reset(context);
            }
        }

        /// <inheritdoc />
        public override void Abort(BtContext context)
        {
            for (var i = 0; i < _children.Count; i++)
            {
                _children[i].Abort(context);
            }

            ClearSlot(context);
        }

        /// <summary>中止下标 <paramref name="fromInclusive"/> 起的子节点。</summary>
        /// <param name="context">上下文。</param>
        /// <param name="fromInclusive">起始子下标。</param>
        protected void AbortChildrenFrom(BtContext context, int fromInclusive)
        {
            for (var i = fromInclusive; i < _children.Count; i++)
            {
                _children[i].Abort(context);
            }
        }

        /// <summary>中止指定子节点。</summary>
        /// <param name="context">上下文。</param>
        /// <param name="childIndex">子下标。</param>
        protected void AbortChild(BtContext context, int childIndex)
        {
            if ((uint)childIndex < (uint)_children.Count)
            {
                _children[childIndex].Abort(context);
            }
        }
    }
}
