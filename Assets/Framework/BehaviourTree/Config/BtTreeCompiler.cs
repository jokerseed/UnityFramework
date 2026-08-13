using System;
using System.Collections.Generic;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 将 <see cref="BtTreeDefinition"/> 编译为运行时 <see cref="BehaviourTree"/>。
    /// </summary>
    public static class BtTreeCompiler
    {
        static readonly BtBuiltinNodeRegistry DefaultBuiltin = new BtBuiltinNodeRegistry();

        /// <summary>
        /// 从 ScriptableObject 资产编译。
        /// </summary>
        /// <param name="asset">行为树资产；不可为 null。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <returns>运行时行为树。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="asset"/> 为 null。</exception>
        public static BehaviourTree Compile(BtTreeAsset asset, IBtNodeRegistry customRegistry = null)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            return Compile(asset.Definition, customRegistry);
        }

        /// <summary>
        /// 从配置定义编译。
        /// </summary>
        /// <param name="definition">树定义；不可为 null。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <returns>运行时行为树。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">配置无效或缺少节点工厂。</exception>
        public static BehaviourTree Compile(BtTreeDefinition definition, IBtNodeRegistry customRegistry = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrEmpty(definition.RootNodeId))
            {
                throw new InvalidOperationException("Behaviour tree root node id is empty.");
            }

            var map = BuildNodeMap(definition);
            if (!map.TryGetValue(definition.RootNodeId, out var rootConfig))
            {
                throw new InvalidOperationException($"Root node not found: {definition.RootNodeId}");
            }

            var root = BuildNode(rootConfig, map, customRegistry);
            return new BehaviourTree(root, definition.TreeName);
        }

        static Dictionary<string, BtConfigNode> BuildNodeMap(BtTreeDefinition definition)
        {
            var map = new Dictionary<string, BtConfigNode>(definition.Nodes.Count);
            for (var i = 0; i < definition.Nodes.Count; i++)
            {
                var node = definition.Nodes[i];
                if (node == null || string.IsNullOrEmpty(node.Id))
                {
                    throw new InvalidOperationException("Behaviour tree contains a node with empty id.");
                }

                if (map.ContainsKey(node.Id))
                {
                    throw new InvalidOperationException($"Duplicate node id: {node.Id}");
                }

                map.Add(node.Id, node);
            }

            return map;
        }

        static BtNode BuildNode(
            BtConfigNode config,
            Dictionary<string, BtConfigNode> map,
            IBtNodeRegistry customRegistry)
        {
            if (IsComposite(config.Kind))
            {
                var composite = CreateComposite(config);
                for (var i = 0; i < config.ChildIds.Count; i++)
                {
                    var childId = config.ChildIds[i];
                    if (string.IsNullOrEmpty(childId))
                    {
                        continue;
                    }

                    if (!map.TryGetValue(childId, out var childConfig))
                    {
                        throw new InvalidOperationException($"Child node not found: {childId} (parent {config.Id})");
                    }

                    composite.AddChild(BuildNode(childConfig, map, customRegistry));
                }

                ApplyDisplayName(composite, config);
                return composite;
            }

            if (IsDecorator(config.Kind))
            {
                if (config.ChildIds == null || config.ChildIds.Count == 0 || string.IsNullOrEmpty(config.ChildIds[0]))
                {
                    throw new InvalidOperationException($"Decorator {config.Id} requires one child.");
                }

                if (!map.TryGetValue(config.ChildIds[0], out var childConfig))
                {
                    throw new InvalidOperationException($"Decorator child not found: {config.ChildIds[0]}");
                }

                var child = BuildNode(childConfig, map, customRegistry);
                var decorator = CreateDecorator(config, child);
                ApplyDisplayName(decorator, config);
                return decorator;
            }

            if (TryCreateLeaf(config, customRegistry, out var leaf))
            {
                ApplyDisplayName(leaf, config);
                return leaf;
            }

            throw new InvalidOperationException(
                $"Unable to create node {config.Id} ({config.Kind}). Register a custom factory or check parameters.");
        }

        static bool TryCreateLeaf(BtConfigNode config, IBtNodeRegistry customRegistry, out BtNode node)
        {
            if (customRegistry != null && customRegistry.TryCreate(config, out node))
            {
                return true;
            }

            return DefaultBuiltin.TryCreate(config, out node);
        }

        static BtComposite CreateComposite(BtConfigNode config)
        {
            switch (config.Kind)
            {
                case BtNodeKind.Sequence:
                    return new BtSequence();
                case BtNodeKind.Selector:
                    return new BtSelector();
                case BtNodeKind.Parallel:
                    return new BtParallel(config.ParallelPolicy);
                default:
                    throw new InvalidOperationException($"Not a composite kind: {config.Kind}");
            }
        }

        static BtNode CreateDecorator(BtConfigNode config, BtNode child)
        {
            switch (config.Kind)
            {
                case BtNodeKind.Inverter:
                    return new BtInverter(child);
                case BtNodeKind.Repeater:
                    return new BtRepeater(child, config.IntParam);
                case BtNodeKind.ForceSuccess:
                    return new BtForceSuccess(child);
                default:
                    throw new InvalidOperationException($"Not a decorator kind: {config.Kind}");
            }
        }

        static bool IsComposite(BtNodeKind kind)
        {
            return kind == BtNodeKind.Sequence
                || kind == BtNodeKind.Selector
                || kind == BtNodeKind.Parallel;
        }

        static bool IsDecorator(BtNodeKind kind)
        {
            return kind == BtNodeKind.Inverter
                || kind == BtNodeKind.Repeater
                || kind == BtNodeKind.ForceSuccess;
        }

        static void ApplyDisplayName(BtNode node, BtConfigNode config)
        {
            if (!string.IsNullOrEmpty(config.DisplayName))
            {
                node.Name = config.DisplayName;
            }
            else
            {
                node.Name = config.Kind.ToString();
            }
        }
    }
}
