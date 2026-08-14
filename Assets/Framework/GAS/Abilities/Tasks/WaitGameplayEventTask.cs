using Framework.GAS.Events;
using Framework.GAS.Tags;

namespace Framework.GAS.Abilities.Tasks
{
    /// <summary>等待指定 GameplayEvent Tag；由 ASC <c>HandleGameplayEvent</c> 立即投递。</summary>
    public sealed class WaitGameplayEventTask : AbilityTask
    {
        readonly string _eventTag;

        /// <summary>构造 WaitGameplayEvent Task。</summary>
        /// <param name="eventTag">要等待的事件 Tag。</param>
        public WaitGameplayEventTask(string eventTag)
        {
            _eventTag = eventTag;
        }

        /// <inheritdoc/>
        protected override void OnGameplayEvent(in GameplayEventData eventData)
        {
            if (IsDone || IsCancelled || eventData.EventTag != _eventTag)
            {
                return;
            }

            Instance.ActivationInfo.TriggerTag = new GameplayTag(eventData.EventTag);
            Finish();
        }
    }
}
