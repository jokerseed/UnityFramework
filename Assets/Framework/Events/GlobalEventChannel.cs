namespace Framework.Events
{
    /// <summary>全局事件通道：按 struct 类型隔离，Publish 热路径无字典查找。</summary>
    internal static class GlobalEventChannel<T> where T : struct
    {
        internal static readonly HandlerList<T> Handlers = Create();

        static HandlerList<T> Create()
        {
            var handlers = new HandlerList<T>();
            GlobalEventChannelRegistry.Register(handlers);
            return handlers;
        }
    }
}
