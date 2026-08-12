using System.Collections.Generic;
using Framework.Core;
using Framework.GAS.Events;
using Framework.GAS.Tags;

namespace Framework.GAS.Abilities.Tasks
{
    /// <summary>等待指定 GameplayEvent Tag。</summary>
    public sealed class WaitGameplayEventTask : AbilityTask
    {
        readonly string _eventTag;
        readonly Queue<GameplayEventData> _eventQueue;

        /// <summary>构造 WaitGameplayEvent Task。</summary>
        /// <param name="eventTag">要等待的事件 Tag。</param>
        /// <param name="eventQueue">ASC 或 GamePlayFramework 共享的事件队列。</param>
        public WaitGameplayEventTask(string eventTag, Queue<GameplayEventData> eventQueue)
        {
            _eventTag = eventTag;
            _eventQueue = eventQueue;
        }

        /// <inheritdoc/>
        public override void Tick(float deltaTime)
        {
            if (IsDone || IsCancelled || _eventQueue == null)
            {
                return;
            }

            while (_eventQueue.Count > 0)
            {
                var data = _eventQueue.Dequeue();
                if (data.EventTag == _eventTag)
                {
                    Instance.ActivationInfo.TriggerTag = new GameplayTag(data.EventTag);
                    Finish();
                    return;
                }
            }
        }
    }
}
