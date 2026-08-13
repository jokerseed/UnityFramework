namespace Framework.BehaviourTree
{
    /// <summary>
    /// 自定义节点工厂注册表。GamePlay 等业务程序集实现并注入 <see cref="BtTreeCompiler"/>。
    /// </summary>
    public interface IBtNodeRegistry
    {
        /// <summary>
        /// 尝试根据配置创建运行时节点。
        /// </summary>
        /// <param name="config">节点配置；不可为 null。</param>
        /// <param name="node">创建的节点；失败时为 null。</param>
        /// <returns>本注册表能处理该配置则为 true。</returns>
        bool TryCreate(BtConfigNode config, out BtNode node);
    }
}
