using System.Collections.Generic;

namespace Framework.GAS.Abilities
{
    /// <summary>一次技能激活的运行时状态。</summary>
    public enum ActiveAbilityState
    {
        /// <summary>已创建，尚未 Commit。</summary>
        Pending,

        /// <summary>已 Commit 并执行 Activate，运行中。</summary>
        Active,

        /// <summary>正常结束。</summary>
        Ended,

        /// <summary>被取消或打断。</summary>
        Cancelled
    }

    /// <summary>单次技能激活实例，持有 Spec、上下文与 Task 运行器。</summary>
    public sealed class ActiveAbilityInstance
    {
        static int s_nextInstanceId = 1;

        readonly List<Tasks.AbilityTask> _tasks = new List<Tasks.AbilityTask>();

        /// <summary>实例唯一 ID，用于 Cancel 查找。</summary>
        public int InstanceId { get; }

        /// <summary>对应的授予技能 Spec。</summary>
        public GameplayAbilitySpec Spec { get; }

        /// <summary>当前激活状态。</summary>
        public ActiveAbilityState State { get; internal set; }

        /// <summary>激活上下文快照。</summary>
        public AbilityActivationContext Context { get; }

        /// <summary>激活附加信息。</summary>
        public AbilityActivationInfo ActivationInfo { get; }

        /// <summary>Task 运行器。</summary>
        public AbilityTaskRunner TaskRunner { get; } = new AbilityTaskRunner();

        /// <summary>本实例挂载的 AbilityTask 列表（只读）。</summary>
        public IReadOnlyList<Tasks.AbilityTask> Tasks => _tasks;

        internal ActiveAbilityInstance(
            GameplayAbilitySpec spec,
            in AbilityActivationContext context,
            AbilityActivationInfo activationInfo)
        {
            InstanceId = s_nextInstanceId++;
            Spec = spec;
            Context = context;
            ActivationInfo = activationInfo ?? new AbilityActivationInfo();
            State = ActiveAbilityState.Pending;
        }

        internal void AddTask(Tasks.AbilityTask task) => _tasks.Add(task);

        internal void ClearTasks()
        {
            TaskRunner.CancelAll();
            _tasks.Clear();
        }
    }
}
