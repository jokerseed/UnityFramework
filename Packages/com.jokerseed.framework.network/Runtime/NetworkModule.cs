using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;
using Framework.MemoryPool;

namespace Framework.Network
{
    /// <summary>网络模块：初始化 <see cref="NetworkManager"/>，Shutdown 时关闭全部频道。</summary>
    public sealed class NetworkModule : IGameModule
    {
        /// <inheritdoc />
        public string Name => "Network";

        /// <inheritdoc />
        public ModulePhase Phase => ModulePhase.Infrastructure;

        /// <inheritdoc />
        public IReadOnlyList<Type> Dependencies => new[]
        {
            typeof(LoggingModule),
            typeof(MemoryPoolModule),
        };

        /// <inheritdoc />
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <inheritdoc />
        public void Initialize()
        {
            var manager = NetworkManager.Instance;
            GameLog.Info(LogCategories.Network, $"Ready  Channels={LogStyle.Value(manager.ChannelCount)}");
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
            if (NetworkManager.HasInstance)
            {
                NetworkManager.Instance.Shutdown();
                NetworkManager.DestroyInstance();
            }

            GameLog.Info(LogCategories.Network, LogStyle.Muted("module shut down"));
        }
    }
}
