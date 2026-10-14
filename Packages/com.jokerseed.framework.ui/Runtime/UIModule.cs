using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Core;
using Framework.Coroutine;
using Framework.Logging;
using Framework.Res;

namespace Framework.UI
{
    /// <summary>UI 模块：初始化 <see cref="UIManager"/>，Shutdown 时关闭全部窗口。</summary>
    public sealed class UIModule : IGameModule
    {
        /// <inheritdoc/>
        public string Name => "UI";

        /// <inheritdoc/>
        public ModulePhase Phase => ModulePhase.Presentation;

        /// <inheritdoc/>
        public IReadOnlyList<Type> Dependencies => new[]
        {
            typeof(LoggingModule),
            typeof(ResourceModule),
            typeof(CoroutineModule),
        };

        /// <inheritdoc/>
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <inheritdoc/>
        public void Initialize()
        {
            UIManager.Instance.Initialize();
            GameLog.Info(LogCategories.UI, $"Module {LogStyle.Ok("ready")}");
        }

        /// <inheritdoc/>
        public IEnumerator InitializeAsync()
        {
            Initialize();
            yield break;
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            if (UIManager.HasInstance)
            {
                UIManager.Instance.Shutdown();
                UIManager.DestroyInstance();
            }

            GameLog.Info(LogCategories.UI, LogStyle.Muted("module shut down"));
        }
    }
}
