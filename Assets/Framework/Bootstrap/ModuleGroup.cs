using System;
using System.Collections.Generic;

namespace Framework.Bootstrap
{
    public sealed class ModuleGroup
    {
        internal readonly List<IGameModule> Modules = new List<IGameModule>();
        internal readonly List<IGameModule> Initialized = new List<IGameModule>();

        public ModuleContext Context { get; internal set; }
        internal Exception LastError;

        public ModuleGroup(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            State = ModuleGroupState.Idle;
        }

        public string Name { get; }
        public ModuleGroupState State { get; private set; }
        public bool IsReady => State == ModuleGroupState.Ready;
        public bool IsRunning => State == ModuleGroupState.Running;
        public bool IsFailed => State == ModuleGroupState.Failed;
        public Exception Error => LastError;

        public event Action<ModuleGroup> StateChanged;
        public event Action<ModuleGroup> Ready;
        public event Action<ModuleGroup, Exception> Failed;
        public event Action<ModuleGroup, int, int> ProgressChanged;

        internal void Configure(IEnumerable<IGameModule> modules)
        {
            if (State == ModuleGroupState.Running)
            {
                throw new InvalidOperationException($"Group '{Name}' is running.");
            }

            Modules.Clear();
            if (modules == null)
            {
                return;
            }

            foreach (var module in modules)
            {
                Modules.Add(module);
            }

            if (State == ModuleGroupState.Failed)
            {
                SetState(ModuleGroupState.Idle);
            }
        }

        internal void SetState(ModuleGroupState state, Exception error = null)
        {
            State = state;
            LastError = error;
            StateChanged?.Invoke(this);

            if (state == ModuleGroupState.Ready)
            {
                Ready?.Invoke(this);
            }
            else if (state == ModuleGroupState.Failed)
            {
                Failed?.Invoke(this, error);
            }
        }

        internal void ReportProgress(int current, int total)
        {
            ProgressChanged?.Invoke(this, current, total);
        }
    }
}
