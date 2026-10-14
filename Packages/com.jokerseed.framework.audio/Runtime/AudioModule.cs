using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Core;
using Framework.Coroutine;
using Framework.Logging;
using Framework.Res;

namespace Framework.Audio
{
    /// <summary>音频模块：初始化 <see cref="AudioManager"/>，Shutdown 时停止全部音频并释放缓存。</summary>
    public sealed class AudioModule : IGameModule
    {
        /// <inheritdoc />
        public string Name => "Audio";

        /// <inheritdoc />
        public ModulePhase Phase => ModulePhase.Presentation;

        /// <inheritdoc />
        public IReadOnlyList<Type> Dependencies => new[]
        {
            typeof(LoggingModule),
            typeof(ResourceModule),
            typeof(CoroutineModule),
        };

        /// <inheritdoc />
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <inheritdoc />
        public void Initialize()
        {
            AudioManager.Instance.Initialize();
            GameLog.Info(LogCategories.Audio, $"Module {LogStyle.Ok("ready")}");
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
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.Shutdown();
                AudioManager.DestroyInstance();
            }

            GameLog.Info(LogCategories.Audio, LogStyle.Muted("module shut down"));
        }
    }
}
