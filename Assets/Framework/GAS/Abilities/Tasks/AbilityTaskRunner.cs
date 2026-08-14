using System.Collections.Generic;

namespace Framework.GAS.Abilities
{
    /// <summary>管理 <see cref="ActiveAbilityInstance"/> 上的 AbilityTask 列表并驱动 Tick。</summary>
    public sealed class AbilityTaskRunner
    {
        readonly List<Tasks.AbilityTask> _tasks = new List<Tasks.AbilityTask>();

        /// <summary>当前任务数量。</summary>
        public int Count => _tasks.Count;

        /// <summary>是否所有任务均已完成。</summary>
        public bool AllDone
        {
            get
            {
                for (var i = 0; i < _tasks.Count; i++)
                {
                    if (!_tasks[i].IsDone)
                    {
                        return false;
                    }
                }

                return _tasks.Count > 0;
            }
        }

        /// <summary>添加并启动任务。</summary>
        /// <param name="instance">所属激活实例。</param>
        /// <param name="task">任务实例。</param>
        public void AddTask(ActiveAbilityInstance instance, Tasks.AbilityTask task)
        {
            task.Bind(instance);
            task.ActivateTask();
            _tasks.Add(task);
            instance.AddTask(task);
        }

        /// <summary>Tick 所有未完成任务；移除已完成任务。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public void Tick(float deltaTime)
        {
            for (var i = _tasks.Count - 1; i >= 0; i--)
            {
                var task = _tasks[i];
                if (task.IsDone || task.IsCancelled)
                {
                    _tasks.RemoveAt(i);
                    continue;
                }

                task.Tick(deltaTime);
                if (task.IsDone)
                {
                    _tasks.RemoveAt(i);
                }
            }
        }

        /// <summary>取消所有任务。</summary>
        public void CancelAll()
        {
            for (var i = 0; i < _tasks.Count; i++)
            {
                _tasks[i].CancelTask();
            }

            _tasks.Clear();
        }

        /// <summary>将 GameplayEvent 分发给尚未结束的任务。</summary>
        /// <param name="eventData">事件数据。</param>
        public void HandleGameplayEvent(in Framework.GAS.Events.GameplayEventData eventData)
        {
            for (var i = 0; i < _tasks.Count; i++)
            {
                var task = _tasks[i];
                if (task.IsDone || task.IsCancelled)
                {
                    continue;
                }

                task.HandleGameplayEvent(eventData);
            }
        }
    }
}
