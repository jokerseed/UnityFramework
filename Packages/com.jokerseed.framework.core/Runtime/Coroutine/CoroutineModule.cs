using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;

namespace Framework.Coroutine
{
    /// <summary>协程模块：初始化 <see cref="CoroutineManager"/>，Shutdown 时停止托管协程。</summary>
    public sealed class CoroutineModule : IGameModule
    {
        /// <inheritdoc />
        public string Name => "Coroutine";

        /// <inheritdoc />
        public ModulePhase Phase => ModulePhase.Infrastructure;

        /// <inheritdoc />
        public IReadOnlyList<Type> Dependencies => new[] { typeof(LoggingModule) };

        /// <inheritdoc />
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <inheritdoc />
        public void Initialize()
        {
            var manager = CoroutineManager.Instance;
            GameLog.Info(LogCategories.Coroutine, $"Module {LogStyle.Ok("ready")}");
            _ = manager;
        }

        /// <inheritdoc />
        public IEnumerator InitializeAsync()
        {
            Initialize();
            yield break;
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            if (CoroutineManager.HasInstance)
            {
                CoroutineManager.Instance.Shutdown();
                CoroutineManager.DestroyInstance();
            }

            GameLog.Info(LogCategories.Coroutine, $"Module {LogStyle.Muted("shut down")}");
        }
    }
}
