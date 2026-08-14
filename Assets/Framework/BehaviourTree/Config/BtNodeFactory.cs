using System;
using System.Collections.Generic;

namespace Framework.BehaviourTree
{
    /// <summary>统一节点工厂：TypeId → 创建函数。代码树与资产编译共用。</summary>
    public sealed class BtNodeFactory : IBtNodeRegistry
    {
        readonly Dictionary<string, Func<BtConfigNode, BtNode>> _byTypeId =
            new Dictionary<string, Func<BtConfigNode, BtNode>>(StringComparer.Ordinal);

        /// <summary>进程内默认工厂。</summary>
        public static BtNodeFactory Default { get; } = new BtNodeFactory();

        /// <summary>注册自定义叶子/节点。</summary>
        /// <param name="typeId">类型 id；不可为空。</param>
        /// <param name="factory">工厂；不可为 null。</param>
        public void Register(string typeId, Func<BtConfigNode, BtNode> factory)
        {
            if (string.IsNullOrEmpty(typeId))
            {
                throw new ArgumentException("Type id must be non-empty.", nameof(typeId));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _byTypeId[typeId] = factory;
        }

        /// <summary>是否已注册该 TypeId。</summary>
        /// <param name="typeId">类型 id。</param>
        /// <returns>已注册则为 true。</returns>
        public bool IsRegistered(string typeId) =>
            !string.IsNullOrEmpty(typeId) && _byTypeId.ContainsKey(typeId);

        /// <inheritdoc />
        public bool TryCreate(BtConfigNode config, out BtNode node)
        {
            node = null;
            if (config == null)
            {
                return false;
            }

            var typeId = config.ResolveTypeId();
            if (string.IsNullOrEmpty(typeId) || !_byTypeId.TryGetValue(typeId, out var factory))
            {
                return false;
            }

            node = factory(config);
            if (node != null)
            {
                node.TypeId = typeId;
            }

            return node != null;
        }
    }
}
