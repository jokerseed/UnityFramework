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

        /// <summary>
        /// 追加子节点。
        /// </summary>
        /// <param name="child">子节点；不可为 null。</param>
        /// <returns>当前组合节点，便于链式调用。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="child"/> 为 null。</exception>
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

            Reset(context);
        }
    }
}
