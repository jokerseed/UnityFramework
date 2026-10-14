using System;
using System.Collections.Generic;

namespace Framework.BehaviourTree
{
    /// <summary>将 BtTreeDefinition 编译为运行时 BehaviourTree。</summary>
    public static class BtTreeCompiler
    {
        static readonly BtBuiltinNodeRegistry DefaultBuiltin = new BtBuiltinNodeRegistry();

        /// <summary>从 ScriptableObject 资产编译。</summary>
        /// <param name="asset">行为树资产；不可为 null。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>运行时行为树。</returns>
        public static BehaviourTree Compile(
            BtTreeAsset asset,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            return Compile(asset.Definition, customRegistry, subtrees);
        }

        /// <summary>从配置定义编译为运行时实例。</summary>
        /// <param name="definition">树定义；不可为 null。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>运行时行为树。</returns>
        public static BehaviourTree Compile(
            BtTreeDefinition definition,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            return new BehaviourTree(CompileToTemplate(definition, customRegistry, subtrees));
        }

        /// <summary>从 ScriptableObject 编译为可共享模板。</summary>
        /// <param name="asset">行为树资产；不可为 null。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>共享拓扑模板。</returns>
        public static BtTreeTemplate CompileToTemplate(
            BtTreeAsset asset,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            return CompileToTemplate(asset.Definition, customRegistry, subtrees);
        }

        /// <summary>从配置定义编译为可共享模板（多 Agent 可 <see cref="BehaviourTree.Instantiate"/>）。</summary>
        /// <param name="definition">树定义；不可为 null。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>共享拓扑模板。</returns>
        public static BtTreeTemplate CompileToTemplate(
            BtTreeDefinition definition,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            ThrowIfInvalid(definition, customRegistry, subtrees);

            if (string.IsNullOrEmpty(definition.RootNodeId))
            {
                throw new InvalidOperationException("Behaviour tree root node id is empty.");
            }

            var map = BuildNodeMap(definition);
            if (!map.TryGetValue(definition.RootNodeId, out var rootConfig))
            {
                throw new InvalidOperationException("Root node not found: " + definition.RootNodeId);
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var root = BuildNode(rootConfig, map, customRegistry, subtrees, visited);
            return BehaviourTree.CreateTemplate(root, definition.TreeName);
        }

        static void ThrowIfInvalid(
            BtTreeDefinition definition,
            IBtNodeRegistry customRegistry,
            IBtSubtreeResolver subtrees)
        {
            var lint = new List<BtLintMessage>();
            BtTreeValidator.Validate(definition, customRegistry, subtrees, lint);
            string errors = null;
            for (var i = 0; i < lint.Count; i++)
            {
                if (lint[i].Severity != BtLintSeverity.Error)
                {
                    continue;
                }

                errors = errors == null ? lint[i].Message : errors + "\n" + lint[i].Message;
            }

            if (errors != null)
            {
                throw new InvalidOperationException(errors);
            }
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
                    throw new InvalidOperationException("Duplicate node id: " + node.Id);
                }

                map.Add(node.Id, node);
            }

            return map;
        }

        static BtNode BuildNode(
            BtConfigNode config,
            Dictionary<string, BtConfigNode> map,
            IBtNodeRegistry customRegistry,
            IBtSubtreeResolver subtrees,
            HashSet<string> visitedSubtrees)
        {
            if (config.Kind == BtNodeKind.Subtree)
            {
                return BuildSubtree(config, customRegistry, subtrees, visitedSubtrees);
            }

            if (BtTreeValidator.IsComposite(config.Kind))
            {
                var composite = CreateComposite(config);
                ApplyMeta(composite, config);
                if (config.ChildIds != null)
                {
                    for (var i = 0; i < config.ChildIds.Count; i++)
                    {
                        var childId = config.ChildIds[i];
                        if (string.IsNullOrEmpty(childId))
                        {
                            continue;
                        }

                        if (!map.TryGetValue(childId, out var childConfig))
                        {
                            throw new InvalidOperationException("Child node not found: " + childId);
                        }

                        composite.AddChild(BuildNode(childConfig, map, customRegistry, subtrees, visitedSubtrees));
                    }
                }

                return composite;
            }

            if (BtTreeValidator.IsDecorator(config.Kind))
            {
                if (config.ChildIds == null || config.ChildIds.Count == 0 || string.IsNullOrEmpty(config.ChildIds[0]))
                {
                    throw new InvalidOperationException("Decorator " + config.Id + " requires one child.");
                }

                if (!map.TryGetValue(config.ChildIds[0], out var childConfig))
                {
                    throw new InvalidOperationException("Decorator child not found: " + config.ChildIds[0]);
                }

                var child = BuildNode(childConfig, map, customRegistry, subtrees, visitedSubtrees);
                var decorator = CreateDecorator(config, child);
                ApplyMeta(decorator, config);
                return decorator;
            }

            if (TryCreateLeaf(config, customRegistry, out var leaf))
            {
                ApplyMeta(leaf, config);
                return leaf;
            }

            throw new InvalidOperationException(
                "Unable to create node " + config.Id + " (" + config.Kind + ").");
        }

        static BtNode BuildSubtree(
            BtConfigNode config,
            IBtNodeRegistry customRegistry,
            IBtSubtreeResolver subtrees,
            HashSet<string> visitedSubtrees)
        {
            var subtreeId = config.ResolveSubtreeId();
            if (string.IsNullOrEmpty(subtreeId))
            {
                throw new InvalidOperationException("Subtree id is empty: " + config.Id);
            }

            if (subtrees == null || !subtrees.TryResolve(subtreeId, out var subDef) || subDef == null)
            {
                throw new InvalidOperationException("Unresolved subtree: " + subtreeId);
            }

            if (!visitedSubtrees.Add(subtreeId))
            {
                throw new InvalidOperationException("Subtree cycle: " + subtreeId);
            }

            if (string.IsNullOrEmpty(subDef.RootNodeId))
            {
                throw new InvalidOperationException("Subtree root is empty: " + subtreeId);
            }

            var subMap = BuildNodeMap(subDef);
            if (!subMap.TryGetValue(subDef.RootNodeId, out var subRoot))
            {
                throw new InvalidOperationException("Subtree root not found: " + subtreeId);
            }

            var inner = BuildNode(subRoot, subMap, customRegistry, subtrees, visitedSubtrees);
            visitedSubtrees.Remove(subtreeId);
            var wrapper = new BtSubtree(inner, subtreeId);
            ApplyMeta(wrapper, config);
            return wrapper;
        }

        static bool TryCreateLeaf(BtConfigNode config, IBtNodeRegistry customRegistry, out BtNode node)
        {
            if (customRegistry != null && customRegistry.TryCreate(config, out node))
            {
                return true;
            }

            if (BtNodeFactory.Default.TryCreate(config, out node))
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
                    return new BtSequence { AbortType = config.AbortType };
                case BtNodeKind.Selector:
                    return new BtSelector { AbortType = config.AbortType };
                case BtNodeKind.ActiveSelector:
                    return new BtActiveSelector();
                case BtNodeKind.RandomSelector:
                    return new BtRandomSelector();
                case BtNodeKind.WeightedSelector:
                    return new BtWeightedSelector(ToWeightArray(config));
                case BtNodeKind.Parallel:
                    return new BtParallel(config.ParallelPolicy, config.FailFast, config.SucceedFast);
                default:
                    throw new InvalidOperationException("Not a composite kind: " + config.Kind);
            }
        }

        static BtNode CreateDecorator(BtConfigNode config, BtNode child)
        {
            switch (config.Kind)
            {
                case BtNodeKind.Inverter:
                    return new BtInverter(child);
                case BtNodeKind.Repeater:
                    return new BtRepeater(child, config.IntParam, config.RepeatOnFailure);
                case BtNodeKind.ForceSuccess:
                    return new BtForceSuccess(child);
                case BtNodeKind.ForceFailure:
                    return new BtForceFailure(child);
                case BtNodeKind.UntilSuccess:
                    return new BtUntilSuccess(child);
                case BtNodeKind.Cooldown:
                    return new BtCooldown(child, config.ResolveDuration());
                case BtNodeKind.Timeout:
                    return new BtTimeout(child, config.ResolveDuration());
                case BtNodeKind.TimeLimit:
                    return new BtTimeLimit(child, config.ResolveDuration());
                default:
                    throw new InvalidOperationException("Not a decorator kind: " + config.Kind);
            }
        }

        static int[] ToWeightArray(BtConfigNode config)
        {
            if (config.Weights == null || config.Weights.Count == 0)
            {
                return null;
            }

            var arr = new int[config.Weights.Count];
            for (var i = 0; i < arr.Length; i++)
            {
                arr[i] = config.Weights[i];
            }

            return arr;
        }

        static void ApplyMeta(BtNode node, BtConfigNode config)
        {
            node.ConfigId = config.Id;
            node.Breakpoint = config.Breakpoint;
            if (config.AbortType != BtAbortType.None && !(node is BtActiveSelector))
            {
                node.AbortType = config.AbortType;
            }

            if (!string.IsNullOrEmpty(config.DisplayName))
            {
                node.Name = config.DisplayName;
            }
            else
            {
                node.Name = config.Kind.ToString();
            }

            var typeId = config.ResolveTypeId();
            if (!string.IsNullOrEmpty(typeId))
            {
                node.TypeId = typeId;
            }
        }
    }
}
