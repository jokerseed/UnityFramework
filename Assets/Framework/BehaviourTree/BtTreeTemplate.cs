using System;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 共享、无 Agent 状态的树拓扑。多 Agent 可对同一模板 <see cref="BehaviourTree.Instantiate"/>。
    /// 代码闭包叶子若捕获了单个 Agent，不要跨 Agent 共享该模板。
    /// </summary>
    public sealed class BtTreeTemplate
    {
        readonly BtNode[] _nodes;

        /// <summary>从已绑定下标的根节点创建模板。</summary>
        /// <param name="root">根节点；不可为 null。</param>
        /// <param name="name">调试名；可为 null。</param>
        /// <param name="nodes">按下标排列的节点；不可为 null。</param>
        /// <exception cref="ArgumentNullException">root 或 nodes 为 null。</exception>
        public BtTreeTemplate(BtNode root, string name, BtNode[] nodes)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            Name = name;
            NodeCount = _nodes.Length;
        }

        /// <summary>根节点。</summary>
        public BtNode Root { get; }

        /// <summary>调试名。</summary>
        public string Name { get; }

        /// <summary>节点数量。</summary>
        public int NodeCount { get; }

        /// <summary>按下标取节点；越界返回 null。</summary>
        /// <param name="index">节点下标。</param>
        /// <returns>节点。</returns>
        public BtNode GetNode(int index) =>
            (uint)index < (uint)_nodes.Length ? _nodes[index] : null;

        /// <summary>按配置 id 查找节点下标；未找到返回 -1。</summary>
        /// <param name="configId">配置节点 id。</param>
        /// <returns>下标或 -1。</returns>
        public int FindIndexByConfigId(string configId)
        {
            if (string.IsNullOrEmpty(configId))
            {
                return -1;
            }

            for (var i = 0; i < _nodes.Length; i++)
            {
                if (_nodes[i].ConfigId == configId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
