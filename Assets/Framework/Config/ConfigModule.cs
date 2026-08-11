using System;
using System.Collections;
using System.Collections.Generic;
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

        public void Initialize()
        {
            BattleConfigBootstrap.LoadRuntimeTables(ResourceManager.Instance);
            Debug.Log("[Config] Module ready.");
        }

        public IEnumerator InitializeAsync()
        {
            Initialize();
            yield break;
        }

        public void Shutdown()
        {
            BattleConfigBootstrap.UnloadRuntimeTables();
        }
    }
}
