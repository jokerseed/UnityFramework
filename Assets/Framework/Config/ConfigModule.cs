using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Bootstrap;
using Framework.Logging;
using Framework.Res;

namespace Framework.Config
{
    /// <summary>
    /// 配置模块：确保 <see cref="ConfigManager"/> 可用；<b>不</b>在初始化时读表。
    /// 读表请在业务侧调用 <see cref="ConfigManager.LoadTables"/>。
    /// </summary>
    public sealed class ConfigModule : IGameModule
    {
        /// <inheritdoc/>
        public string Name => "Config";

        /// <inheritdoc/>
        public ModulePhase Phase => ModulePhase.Data;

        /// <inheritdoc/>
        public IReadOnlyList<Type> Dependencies => new[] { typeof(ResourceModule) };

        /// <inheritdoc/>
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <inheritdoc/>
        public void Initialize()
        {
            _ = ConfigManager.Instance;
            GameLog.Info(LogCategories.Config, $"{LogStyle.Name(Name)} {LogStyle.Ok("ready")} (tables lazy)");
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
            if (ConfigManager.HasInstance)
            {
                ConfigManager.Instance.Shutdown();
            }
        }
    }
}
