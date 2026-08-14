using System;

namespace Framework.BehaviourTree
{
    /// <summary>运行时加载行为树配置的便捷入口。</summary>
    public static class BtTreeLoader
    {
        /// <summary>从 ScriptableObject 加载并编译。</summary>
        /// <param name="asset">行为树资产；不可为 null。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>运行时行为树实例。</returns>
        public static BehaviourTree Load(
            BtTreeAsset asset,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            return BtTreeCompiler.Compile(asset, customRegistry, subtrees);
        }

        /// <summary>从 JSON 文本加载并编译。</summary>
        /// <param name="json">JSON 配置。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>运行时行为树。</returns>
        public static BehaviourTree LoadFromJson(
            string json,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            var definition = BtTreeSerializer.FromJson(json);
            return BtTreeCompiler.Compile(definition, customRegistry, subtrees);
        }
    }
}
