using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Bootstrap;
using Framework.Logging;
using Framework.Res;

namespace Framework.Config
{
    /// <summary>配置模块：加载 Luban Tables 并供战斗层使用。</summary>
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
            BattleConfigBootstrap.LoadRuntimeTables(ResourceManager.Instance);
            GameLog.Info(LogCategories.Config, $"{LogStyle.Name(Name)} {LogStyle.Ok("ready")}");
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
            BattleConfigBootstrap.UnloadRuntimeTables();
        }
    }
}
