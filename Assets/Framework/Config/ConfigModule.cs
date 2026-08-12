using System;
using System.Collections;
using System.Collections.Generic;
using cfg;
using Framework.Bootstrap;
using Framework.Logging;
using Framework.Res;

namespace Framework.Config
{
    /// <summary>配置模块：通过 <see cref="ResourceManager"/> 加载并持有 Luban <see cref="Tables"/>。</summary>
    public sealed class ConfigModule : IGameModule
    {
        static ConfigModule s_instance;

        /// <summary>当前已初始化的配置模块；未初始化时为 null。</summary>
        public static ConfigModule Instance => s_instance;

        /// <summary>运行时 Luban 表；<see cref="Initialize"/> 后可用。</summary>
        public Tables Tables { get; private set; }

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
            s_instance = this;
            Tables = ResourceManager.Instance.LoadLubanTables();
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
            Tables = null;

            if (ResourceManager.HasInstance && ResourceManager.Instance.IsInitialized)
            {
                ResourceManager.Instance.ReleaseCache();
            }

            if (s_instance == this)
            {
                s_instance = null;
            }
        }
    }
}
