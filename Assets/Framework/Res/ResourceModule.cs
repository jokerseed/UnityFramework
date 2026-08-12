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
    /// 在 <c>GameBootstrap</c> 中注册后，其他依赖资源的模块须在 <c>Dependencies</c> 中声明对本模块的依赖。
    /// </summary>
    public sealed class ResourceModule : IGameModule
    {
        readonly ResourceInitOptions _options;

        /// <summary>使用指定选项创建资源模块。</summary>
        /// <param name="options">资源初始化选项；为 null 时使用默认选项。</param>
        public ResourceModule(ResourceInitOptions options = null)
        {
            _options = options ?? new ResourceInitOptions();
        }

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

        /// <summary>异步初始化：应用平台默认配置后驱动 <see cref="ResourceManager"/> 完成包初始化。</summary>
        public IEnumerator InitializeAsync()
        {
            ApplyPlatformDefaults(_options);

            var manager = ResourceManager.Instance;
            yield return manager.InitializeAsync(_options);
            GameLog.Info(LogCategories.Resource, "Module ready.");
        }

        /// <summary>关闭资源管理器，释放所有已缓存的资源句柄。</summary>
        public void Shutdown()
        {
            if (!ResourceManager.HasInstance)
            {
                return;
            }

            var manager = ResourceManager.Instance;
            if (manager != null && manager.IsInitialized)
            {
                manager.Shutdown();
            }
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
