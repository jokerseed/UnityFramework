namespace Framework.BehaviourTree
{
    /// <summary>按 id 解析子树配置，供编译期内联 <see cref="BtNodeKind.Subtree"/>。</summary>
    public interface IBtSubtreeResolver
    {
        /// <summary>尝试解析子树。</summary>
        /// <param name="subtreeId">子树 id（通常为资产名或配置 id）。</param>
        /// <param name="definition">解析到的定义；失败时为 null。</param>
        /// <returns>找到则可编译的定义时返回 true。</returns>
        bool TryResolve(string subtreeId, out BtTreeDefinition definition);
    }
}
