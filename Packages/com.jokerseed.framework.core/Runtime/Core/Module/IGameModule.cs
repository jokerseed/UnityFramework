using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Framework.Core
{
    /// <summary>
    /// 游戏模块契约：生命周期、依赖声明与初始化模式。
    /// 由 <c>Framework.Bootstrap.GameBootstrap</c> 调度；实现类放在各业务程序集。
    /// 同步模块只需实现 <see cref="Initialize"/>；异步模块把 <see cref="InitMode"/> 设为 <see cref="ModuleInitMode.Asynchronous"/> 并重写 <see cref="InitializeAsync"/>。
    /// </summary>
    public interface IGameModule
    {
        /// <summary>获取模块名称，用于日志与调试。</summary>
        string Name { get; }

        /// <summary>获取模块的启动阶段，影响同波次内的排序。</summary>
        ModulePhase Phase { get; }

        /// <summary>
        /// 获取该模块直接依赖的模块类型列表；拓扑排序据此决定初始化顺序。
        /// 可以写具体类型或接口。同一个依赖能匹配到多个模块时排序失败。
        /// 其它已经 Ready 的组里的模块也算已满足。
        /// </summary>
        IReadOnlyList<Type> Dependencies { get; }

        /// <summary>同步初始化；仅在 <see cref="InitMode"/> 为 <see cref="ModuleInitMode.Synchronous"/> 时由 Bootstrap 调用。</summary>
        void Initialize();

        /// <summary>
        /// 异步初始化；仅在 <see cref="InitMode"/> 为 <see cref="ModuleInitMode.Asynchronous"/> 时由 Bootstrap 等待。
        /// 默认实现直接调用 <see cref="Initialize"/>。
        /// </summary>
        /// <param name="cancellationToken">组初始化失败、组被重置或 Bootstrap 销毁时取消。</param>
        /// <returns>初始化完成的任务；抛出的异常会让所在组进入 Failed。</returns>
        UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            Initialize();
            return UniTask.CompletedTask;
        }

        /// <summary>获取模块初始化执行方式；默认同步。</summary>
        ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <summary>获取该模块是否允许与同波次其他模块并发初始化；默认不允许。</summary>
        bool AllowConcurrentInitialization => false;

        /// <summary>关闭并清理模块资源；由 Bootstrap 在逆序销毁时调用。</summary>
        void Shutdown();
    }
}
