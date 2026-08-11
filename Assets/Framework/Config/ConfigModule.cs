using System;
using System.Collections;
using System.Collections.Generic;
using cfg;
using Framework.Bootstrap;
using Framework.Res;
using UnityEngine;

namespace Framework.Config
{
    public sealed class ConfigModule : IGameModule
    {
        public string Name => "Config";
        public ModulePhase Phase => ModulePhase.Data;
        public IReadOnlyList<Type> Dependencies => new[] { typeof(ResourceModule) };
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        public void Initialize(ModuleContext context)
        {
            var resourceManager = context.GetService<ResourceManager>();
            var tables = BattleConfigBootstrap.LoadTables(resourceManager);
            context.RegisterService(tables);
            Debug.Log("[Config] Module ready.");
        }

        public IEnumerator InitializeAsync(ModuleContext context)
        {
            Initialize(context);
            yield break;
        }

        public void Shutdown(ModuleContext context)
        {
        }
    }
}
