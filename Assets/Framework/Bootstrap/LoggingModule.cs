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

        public LoggingModule(LogInitOptions options = null)
        {
            _options = options ?? new LogInitOptions();
        }

        public string Name => "Logging";
        public ModulePhase Phase => ModulePhase.Infrastructure;
        public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        public void Initialize()
        {
            GameLog.Configure(_options);
            GameLog.Info(LogCategories.Bootstrap, "Logging module ready.");
        }

        public IEnumerator InitializeAsync()
        {
            Initialize();
            yield break;
        }

        public void Shutdown()
        {
            GameLog.Shutdown();
        }
    }
}
