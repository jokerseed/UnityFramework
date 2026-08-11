using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Bootstrap;
using Framework.Logging;
using UnityEngine;

namespace Framework.Res
{
    public sealed class ResourceModule : IGameModule
    {
        readonly ResourceInitOptions _options;

        public ResourceModule(ResourceInitOptions options = null)
        {
            _options = options ?? new ResourceInitOptions();
        }

        public string Name => "Resource";
        public ModulePhase Phase => ModulePhase.Infrastructure;
        public IReadOnlyList<Type> Dependencies => new[] { typeof(LoggingModule) };
        public ModuleInitMode InitMode => ModuleInitMode.Asynchronous;

        public void Initialize()
        {
        }

        public IEnumerator InitializeAsync()
        {
            ApplyPlatformDefaults(_options);

            var manager = ResourceManager.Instance;
            yield return manager.InitializeAsync(_options);
            GameLog.Info(LogCategories.Resource, "Module ready.");
        }

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
