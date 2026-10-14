namespace Framework.BehaviourTree
{
    /// <summary>
    /// 运行时加载行为树配置的便捷入口（JSON 文本）。
    /// 从 <c>BtTreeAsset</c> 或 <c>TextAsset</c> 加载见 <c>Framework.BehaviourTree.Unity</c> 的 <c>BtAssetLoader</c>。
    /// </summary>
    public static class BtTreeLoader
    {
        /// <summary>从 JSON 文本加载并编译为实例。</summary>
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

        /// <summary>从 JSON 文本编译为共享模板。</summary>
        /// <param name="json">JSON 配置。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>共享拓扑模板。</returns>
        public static BtTreeTemplate CompileToTemplateFromJson(
            string json,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            var definition = BtTreeSerializer.FromJson(json);
            return BtTreeCompiler.CompileToTemplate(definition, customRegistry, subtrees);
        }
    }
}
