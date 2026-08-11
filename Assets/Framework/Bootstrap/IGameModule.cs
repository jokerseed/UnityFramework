using System;
using System.Collections;
using System.Collections.Generic;

namespace Framework.Bootstrap
{
    public interface IGameModule
    {
        string Name { get; }
        ModulePhase Phase { get; }
        IReadOnlyList<Type> Dependencies { get; }

        /// <summary>同步初始化；<see cref="InitMode"/> 为 Synchronous 时由 Bootstrap 调用。</summary>
        void Initialize(ModuleContext context);

        /// <summary>异步初始化；<see cref="InitMode"/> 为 Asynchronous 时由 Bootstrap 驱动协程。</summary>
        IEnumerator InitializeAsync(ModuleContext context);

        /// <summary>初始化方式，默认同步。</summary>
        ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <summary>
        /// 同一依赖波次内是否允许与其他模块并发初始化。
        /// 仅当波次内所有模块均为 true 时才会并发执行。
        /// </summary>
        bool AllowConcurrentInitialization => false;

        void Shutdown(ModuleContext context);
    }
}
