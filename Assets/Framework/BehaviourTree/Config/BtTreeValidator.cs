using System;
using System.Collections.Generic;

namespace Framework.BehaviourTree
{
    /// <summary>校验问题级别。</summary>
    public enum BtLintSeverity
    {
        /// <summary>信息。</summary>
        Info = 0,

        /// <summary>警告，仍可编译。</summary>
        Warning = 1,

        /// <summary>错误，编译应失败。</summary>
        Error = 2,
    }

    /// <summary>单条校验消息。</summary>
    public sealed class BtLintMessage
    {
        /// <summary>创建消息。</summary>
        /// <param name="severity">级别。</param>
        /// <param name="nodeId">相关节点 id；可为 null。</param>
        /// <param name="message">文本。</param>
        public BtLintMessage(BtLintSeverity severity, string nodeId, string message)
        {
            Severity = severity;
            NodeId = nodeId;
            Message = message ?? string.Empty;
        }

        /// <summary>级别。</summary>
        public BtLintSeverity Severity { get; }

        /// <summary>节点 id。</summary>
        public string NodeId { get; }

        /// <summary>文本。</summary>
        public string Message { get; }
    }

    /// <summary>行为树配置静态校验。</summary>
    public static class BtTreeValidator
    {
        /// <summary>校验定义并写入 results。</summary>
        /// <param name="definition">定义；可为 null。</param>
        /// <param name="registry">自定义注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <param name="results">输出；不可为 null。</param>
        public static void Validate(
            BtTreeDefinition definition,
            IBtNodeRegistry registry,
            IBtSubtreeResolver subtrees,
            List<BtLintMessage> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            if (definition == null)
            {
                results.Add(new BtLintMessage(BtLintSeverity.Error, null, "Definition is null."));
                return;
            }

            if (definition.Nodes == null || definition.Nodes.Count == 0)
            {
                results.Add(new BtLintMessage(BtLintSeverity.Error, null, "Tree has no nodes."));
                return;
            }

            if (string.IsNullOrEmpty(definition.RootNodeId))
            {
                results.Add(new BtLintMessage(BtLintSeverity.Error, null, "Root node id is empty."));
            }

            var map = new Dictionary<string, BtConfigNode>();
            for (var i = 0; i < definition.Nodes.Count; i++)
            {
                var node = definition.Nodes[i];
                if (node == null || string.IsNullOrEmpty(node.Id))
                {
                    results.Add(new BtLintMessage(BtLintSeverity.Error, null, "Node with empty id."));
                    continue;
                }

                if (map.ContainsKey(node.Id))
                {
                    results.Add(new BtLintMessage(BtLintSeverity.Error, node.Id, "Duplicate node id."));
                    continue;
                }

                map.Add(node.Id, node);
            }

            if (!string.IsNullOrEmpty(definition.RootNodeId) && !map.ContainsKey(definition.RootNodeId))
            {
                results.Add(new BtLintMessage(BtLintSeverity.Error, definition.RootNodeId, "Root node not found."));
            }

            var reachable = new HashSet<string>();
            if (!string.IsNullOrEmpty(definition.RootNodeId) && map.ContainsKey(definition.RootNodeId))
            {
                WalkReachable(definition.RootNodeId, map, reachable, new HashSet<string>(), results);
            }

            foreach (var pair in map)
            {
                if (!reachable.Contains(pair.Key))
                {
                    results.Add(new BtLintMessage(BtLintSeverity.Warning, pair.Key, "Unconnected node."));
                }

                LintNode(pair.Value, map, registry, subtrees, results);
            }
        }

        /// <summary>是否为组合 Kind。</summary>
        /// <param name="kind">类型。</param>
        /// <returns>组合则为 true。</returns>
        public static bool IsComposite(BtNodeKind kind)
        {
            return kind == BtNodeKind.Sequence
                || kind == BtNodeKind.Selector
                || kind == BtNodeKind.Parallel
                || kind == BtNodeKind.RandomSelector
                || kind == BtNodeKind.WeightedSelector
                || kind == BtNodeKind.ActiveSelector;
        }

        /// <summary>是否为装饰 Kind。</summary>
        /// <param name="kind">类型。</param>
        /// <returns>装饰则为 true。</returns>
        public static bool IsDecorator(BtNodeKind kind)
        {
            return kind == BtNodeKind.Inverter
                || kind == BtNodeKind.Repeater
                || kind == BtNodeKind.ForceSuccess
                || kind == BtNodeKind.ForceFailure
                || kind == BtNodeKind.UntilSuccess
                || kind == BtNodeKind.Cooldown
                || kind == BtNodeKind.Timeout
                || kind == BtNodeKind.TimeLimit;
        }

        static void WalkReachable(
            string id,
            Dictionary<string, BtConfigNode> map,
            HashSet<string> reachable,
            HashSet<string> stack,
            List<BtLintMessage> results)
        {
            if (!map.TryGetValue(id, out var node))
            {
                return;
            }

            if (!stack.Add(id))
            {
                results.Add(new BtLintMessage(BtLintSeverity.Error, id, "Cycle in child links."));
                return;
            }

            reachable.Add(id);
            if (node.ChildIds != null)
            {
                for (var i = 0; i < node.ChildIds.Count; i++)
                {
                    var childId = node.ChildIds[i];
                    if (string.IsNullOrEmpty(childId))
                    {
                        continue;
                    }

                    if (!map.ContainsKey(childId))
                    {
                        results.Add(new BtLintMessage(BtLintSeverity.Error, id, "Missing child " + childId + "."));
                        continue;
                    }

                    WalkReachable(childId, map, reachable, stack, results);
                }
            }

            stack.Remove(id);
        }

        static void LintNode(
            BtConfigNode node,
            Dictionary<string, BtConfigNode> map,
            IBtNodeRegistry registry,
            IBtSubtreeResolver subtrees,
            List<BtLintMessage> results)
        {
            var childCount = node.ChildIds != null ? node.ChildIds.Count : 0;
            if (IsDecorator(node.Kind))
            {
                if (childCount == 0)
                {
                    results.Add(new BtLintMessage(BtLintSeverity.Error, node.Id, "Decorator requires one child."));
                }
                else if (childCount > 1)
                {
                    results.Add(new BtLintMessage(BtLintSeverity.Warning, node.Id, "Decorator extra children are ignored."));
                }
            }
            else if (IsComposite(node.Kind) && childCount == 0)
            {
                results.Add(new BtLintMessage(BtLintSeverity.Warning, node.Id, "Composite has no children."));
            }

            if (node.Kind == BtNodeKind.CustomAction || node.Kind == BtNodeKind.CustomCondition)
            {
                var typeId = node.ResolveTypeId();
                if (string.IsNullOrEmpty(typeId))
                {
                    results.Add(new BtLintMessage(BtLintSeverity.Error, node.Id, "Custom node missing TypeId."));
                }
                else if (!IsTypeRegistered(typeId, registry))
                {
                    results.Add(new BtLintMessage(BtLintSeverity.Error, node.Id, "Unregistered TypeId '" + typeId + "'."));
                }
            }

            if (node.Kind == BtNodeKind.Subtree)
            {
                var subtreeId = node.ResolveSubtreeId();
                if (string.IsNullOrEmpty(subtreeId))
                {
                    results.Add(new BtLintMessage(BtLintSeverity.Error, node.Id, "Subtree id is empty."));
                }
                else if (subtrees == null || !subtrees.TryResolve(subtreeId, out _))
                {
                    results.Add(new BtLintMessage(BtLintSeverity.Error, node.Id, "Unresolved subtree '" + subtreeId + "'."));
                }
            }

            if ((node.Kind == BtNodeKind.WaitTime ||
                 node.Kind == BtNodeKind.Cooldown ||
                 node.Kind == BtNodeKind.Timeout ||
                 node.Kind == BtNodeKind.TimeLimit) &&
                node.ResolveDuration() < Framework.FixedMath.FP.Zero)
            {
                results.Add(new BtLintMessage(BtLintSeverity.Error, node.Id, "Duration must be >= 0."));
            }

            if (node.Kind == BtNodeKind.WeightedSelector &&
                node.Weights != null &&
                node.ChildIds != null &&
                node.Weights.Count != 0 &&
                node.Weights.Count != node.ChildIds.Count)
            {
                results.Add(new BtLintMessage(BtLintSeverity.Warning, node.Id, "Weights count does not match children."));
            }
        }

        static bool IsTypeRegistered(string typeId, IBtNodeRegistry registry)
        {
            if (BtNodeFactory.Default.IsRegistered(typeId))
            {
                return true;
            }

            if (registry == null)
            {
                return false;
            }

            var probe = new BtConfigNode
            {
                Kind = BtNodeKind.CustomAction,
                TypeId = typeId,
                StringParam = typeId
            };
            return registry.TryCreate(probe, out _);
        }
    }
}
