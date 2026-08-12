using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Bootstrap;
using Framework.Logging;
using UnityEngine;

namespace Framework.Res
{
    /// <summary>
    /// 资源模块，负责驱动 <see cref="ResourceManager"/> 完成初始化与关闭。
    /// 初始化选项从 <see cref="ResourceManager.InitOptions"/> 读取。
    /// 在 <c>GameBootstrap</c> 中注册后，其他依赖资源的模块须在 <c>Dependencies</c> 中声明对本模块的依赖。
    /// </summary>
    public sealed class ResourceModule : IGameModule
    {
        /// <summary>模块名称。</summary>
        public string Name => "Resource";

        /// <summary>模块所属初始化阶段。</summary>
        public ModulePhase Phase => ModulePhase.Infrastructure;

        /// <summary>依赖的模块类型列表。</summary>
        public IReadOnlyList<Type> Dependencies => new[] { typeof(LoggingModule) };

        /// <summary>初始化模式（异步）。</summary>
        public ModuleInitMode InitMode => ModuleInitMode.Asynchronous;

        /// <summary>同步初始化（本模块无同步初始化逻辑）。</summary>
        public void Initialize()
        {
        }

        /// <summary>异步初始化：从 <see cref="ResourceManager"/> 读取选项，应用平台默认后完成包初始化。</summary>
        /// <returns>协程枚举器。</returns>
        public IEnumerator InitializeAsync()
        {
            var manager = ResourceManager.Instance;
            var options = manager.InitOptions;
            ApplyPlatformDefaults(options);

            yield return manager.InitializeAsync(options);
            GameLog.Info(LogCategories.Resource, $"Module {LogStyle.Ok("ready")}");
        }

        /// <summary>关闭资源管理器：释放缓存并销毁 YooAsset。</summary>
        public void Shutdown()
        {
            if (!ResourceManager.HasInstance)
            {
                return;
            }

            ResourceManager.Instance.Shutdown();
        }

        static void ApplyPlatformDefaults(ResourceInitOptions options)
        {
#if UNITY_EDITOR
            return;
#else
            if (options.PlayMode == ResourcePlayMode.EditorSimulate)
            {
                options.PlayMode = ResourcePlayMode.Offline;
            }
#endif
        }
    }
}
