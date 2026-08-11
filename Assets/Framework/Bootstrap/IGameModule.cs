using System;
using System.Collections;
using System.Collections.Generic;

namespace Framework.Bootstrap
{
    public interface IGameModule
    {
        string Name { get; }
        ModulePhase Phase { get; }
        IReadOnlyList<Type> Dependencies { get; }

        void Initialize();
        IEnumerator InitializeAsync();
        ModuleInitMode InitMode => ModuleInitMode.Synchronous;
        bool AllowConcurrentInitialization => false;
        void Shutdown();
    }
}
