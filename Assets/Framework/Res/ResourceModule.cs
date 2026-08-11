using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Bootstrap;
using UnityEngine;
using YooAsset;

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
        public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();
        public ModuleInitMode InitMode => ModuleInitMode.Asynchronous;

        public void Initialize(ModuleContext context)
        {
        }

        public IEnumerator InitializeAsync(ModuleContext context)
        {
            ApplyPlatformDefaults(_options);

            var manager = ResourceManager.Instance;
            yield return manager.InitializeAsync(_options);
            context.RegisterService(manager);
            Debug.Log("[Resource] Module ready.");
        }

        public void Shutdown(ModuleContext context)
        {
            if (context.TryGetService<ResourceManager>(out var manager))
            {
                manager.Shutdown();
            }
        }

        static void ApplyPlatformDefaults(ResourceInitOptions options)
        {
#if UNITY_EDITOR
            return;
#else
            if (options.PlayMode == EPlayMode.EditorSimulateMode)
            {
                options.PlayMode = EPlayMode.OfflinePlayMode;
            }
#endif
        }
    }
}
