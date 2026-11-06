namespace Framework.Core
{
    /// <summary>
    /// 全局服务模块标记：同一具体模块类型只能由 Bootstrap 的一个组注册，直到该组重置。
    /// 用于包装单例或静态服务的模块；不改变独立、非全局模块的多实例能力。
    /// </summary>
    public interface IExclusiveGameModule : IGameModule { }
}
