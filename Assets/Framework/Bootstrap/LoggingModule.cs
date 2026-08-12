using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Bootstrap;

namespace Framework.Logging
{
    /// <summary>
    /// 日志模块：置于 Bootstrap 程序集，避免与 GameLog 所在程序集循环依赖。
    /// </summary>
    public sealed class LoggingModule : IGameModule
    {
        readonly LogInitOptions _options;

        /// <summary>使用指定初始化选项创建日志模块；<paramref name="options"/> 为 null 时使用默认配置。</summary>
        /// <param name="options">日志初始化选项；可为 null，表示使用 <see cref="LogInitOptions"/> 默认值。</param>
        public LoggingModule(LogInitOptions options = null)
        {
            _options = options ?? new LogInitOptions();
        }

        /// <summary>获取模块名称。</summary>
        public string Name => "Logging";

        /// <summary>获取模块所在启动阶段（基础设施层）。</summary>
        public ModulePhase Phase => ModulePhase.Infrastructure;

        /// <summary>获取模块依赖列表；日志模块无依赖项。</summary>
        public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

        /// <summary>获取初始化执行方式（同步）。</summary>
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <summary>同步初始化日志系统，调用 <see cref="GameLog.Configure"/> 并输出就绪日志。</summary>
        public void Initialize()
        {
            GameLog.Configure(_options);
            GameLog.Info(LogCategories.Bootstrap, "Logging module ready.");
        }

        /// <summary>异步初始化协程（内部直接调用同步 <see cref="Initialize"/>）。</summary>
        /// <returns>协程枚举器。</returns>
        public IEnumerator InitializeAsync()
        {
            Initialize();
            yield break;
        }

        /// <summary>关闭日志系统，调用 <see cref="GameLog.Shutdown"/>。</summary>
        public void Shutdown()
        {
            GameLog.Shutdown();
        }
    }
}
