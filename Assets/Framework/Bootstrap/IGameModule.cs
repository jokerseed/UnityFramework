using System;
using System.Collections;
using System.Collections.Generic;

namespace Framework.Bootstrap
{
    /// <summary>游戏模块接口，定义模块生命周期、依赖声明与初始化模式。</summary>
    public interface IGameModule
    {
        /// <summary>获取模块名称，用于日志与调试。</summary>
        string Name { get; }

        /// <summary>获取模块的启动阶段，影响同波次内的排序。</summary>
        ModulePhase Phase { get; }

        /// <summary>获取该模块直接依赖的模块类型列表；拓扑排序据此决定初始化顺序。</summary>
        IReadOnlyList<Type> Dependencies { get; }

        /// <summary>同步初始化；仅在 <see cref="InitMode"/> 为 <see cref="ModuleInitMode.Synchronous"/> 时由 Bootstrap 调用。</summary>
        void Initialize();

        /// <summary>异步初始化协程；仅在 <see cref="InitMode"/> 为 <see cref="ModuleInitMode.Asynchronous"/> 时由 Bootstrap 驱动。</summary>
        /// <returns>协程枚举器；同步模块可直接调用 Initialize() 后 yield break。</returns>
        IEnumerator InitializeAsync();

        /// <summary>获取模块初始化执行方式；默认同步。</summary>
        ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <summary>获取该模块是否允许与同波次其他模块并发初始化；默认不允许。</summary>
        bool AllowConcurrentInitialization => false;

        /// <summary>关闭并清理模块资源；由 Bootstrap 在逆序销毁时调用。</summary>
        void Shutdown();
    }
}
