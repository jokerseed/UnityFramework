using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;
using Framework.Res;
using Luban;

namespace Framework.Config
{
    /// <summary>
    /// 配置模块：确保 <see cref="ConfigManager"/> 可用并注册表工厂；<b>不</b>在初始化时读表。
    /// 读表请在业务侧调用 <see cref="ConfigManager.LoadTables{TTables}"/>。
    /// </summary>
    public sealed class ConfigModule : IGameModule
    {
        readonly Action<ConfigManager> _register;

        /// <summary>不注册表工厂；须在读表前自行调用 <see cref="ConfigManager.RegisterTables{TTables}"/>。</summary>
        public ConfigModule()
        {
        }

        ConfigModule(Action<ConfigManager> register)
        {
            _register = register;
        }

        /// <summary>创建在初始化时注册表工厂的配置模块。</summary>
        /// <typeparam name="TTables">Luban 生成的表聚合类型（如 <c>cfg.CfgTables</c>）。</typeparam>
        /// <param name="factory">表构造工厂，如 <c>loader =&gt; new cfg.CfgTables(loader)</c>。</param>
        /// <returns>配置模块。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> 为 null。</exception>
        public static ConfigModule Create<TTables>(Func<Func<string, ByteBuf>, TTables> factory) where TTables : class
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            return new ConfigModule(manager => manager.RegisterTables(factory));
        }

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
            var manager = ConfigManager.Instance;
            _register?.Invoke(manager);
            var tables = manager.TablesType != null ? manager.TablesType.Name : "none";
            GameLog.Info(LogCategories.Config, $"{LogStyle.Name(Name)} {LogStyle.Ok("ready")} (tables={tables}, lazy)");
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
