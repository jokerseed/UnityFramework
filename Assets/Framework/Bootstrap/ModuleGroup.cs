using System;
using System.Collections.Generic;
using Framework.Core;

namespace Framework.Bootstrap
{
    /// <summary>一组相关模块的容器，跟踪初始化状态并暴露进度/完成/失败事件。</summary>
    public sealed class ModuleGroup
    {
        internal readonly List<IGameModule> Modules = new List<IGameModule>();
        internal readonly List<IGameModule> Initialized = new List<IGameModule>();

        internal Exception LastError;

        /// <summary>使用指定名称创建模块组，初始状态为 <see cref="ModuleGroupState.Idle"/>。</summary>
        /// <param name="name">组名，不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> 为 null。</exception>
        public ModuleGroup(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            State = ModuleGroupState.Idle;
        }

        /// <summary>获取模块组名称。</summary>
        public string Name { get; }

        /// <summary>获取模块组当前生命周期状态。</summary>
        public ModuleGroupState State { get; private set; }

        /// <summary>获取模块组是否已全部初始化完成。</summary>
        public bool IsReady => State == ModuleGroupState.Ready;

        /// <summary>获取模块组是否正在初始化中。</summary>
        public bool IsRunning => State == ModuleGroupState.Running;

        /// <summary>获取模块组初始化是否失败。</summary>
        public bool IsFailed => State == ModuleGroupState.Failed;

        /// <summary>获取最后一次失败时的异常；未失败时为 null。</summary>
        public Exception Error => LastError;

        /// <summary>状态发生变化时触发；参数为当前模块组实例。</summary>
        public event Action<ModuleGroup> StateChanged;

        /// <summary>全部模块初始化完成时触发；参数为当前模块组实例。</summary>
        public event Action<ModuleGroup> Ready;

        /// <summary>初始化过程发生错误时触发；参数为模块组实例与异常对象。</summary>
        public event Action<ModuleGroup, Exception> Failed;

        /// <summary>初始化进度变化时触发；参数依次为模块组实例、已完成数、总数。</summary>
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
