using Framework.Events;
using Framework.GAS.Events;
using UnityEngine;

namespace Framework.GAS.Cues
{
    /// <summary>GameplayCue 参数。</summary>
    public readonly struct GameplayCueParameters
    {
        /// <summary>相关 Actor。</summary>
        public Core.ActorId Actor { get; }

        /// <summary>位置。</summary>
        public Vector3 Location { get; }

        /// <summary>方向。</summary>
        public Vector3 Direction { get; }

        /// <summary>构造 Cue 参数。</summary>
        public GameplayCueParameters(Core.ActorId actor, Vector3 location, Vector3 direction)
        {
            Actor = actor;
            Location = location;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
        }
    }

    /// <summary>GameplayCue 运行时管理接口（对应 UE Cue Executed/OnActive/Removed）。</summary>
    public interface IGameplayCueManager
    {
        /// <summary>瞬时执行 Cue。</summary>
        void ExecuteCue(string cueTag, in GameplayCueParameters parameters);

        /// <summary>添加持续 Cue。</summary>
        void AddCue(string cueTag, in GameplayCueParameters parameters);

        /// <summary>移除持续 Cue。</summary>
        void RemoveCue(string cueTag, in GameplayCueParameters parameters);
    }

    /// <summary>将 Cue 调用转发到 <see cref="IEventBus"/> 的默认实现。</summary>
    public sealed class EventBusGameplayCueManager : IGameplayCueManager
    {
        readonly IEventBus _eventBus;

        /// <summary>构造 Cue 管理器。</summary>
        /// <param name="eventBus">表现事件总线。</param>
        public EventBusGameplayCueManager(IEventBus eventBus) => _eventBus = eventBus;

        /// <inheritdoc/>
        public void ExecuteCue(string cueTag, in GameplayCueParameters parameters) =>
            Publish(cueTag, parameters);

        /// <inheritdoc/>
        public void AddCue(string cueTag, in GameplayCueParameters parameters) =>
            Publish(cueTag, parameters);

        /// <inheritdoc/>
        public void RemoveCue(string cueTag, in GameplayCueParameters parameters) =>
            Publish(cueTag, parameters);

        void Publish(string cueTag, in GameplayCueParameters parameters)
        {
            _eventBus.Publish(new GameplayCueEvent
            {
                Actor = parameters.Actor,
                CueTag = cueTag,
                Position = parameters.Location,
                Direction = parameters.Direction
            });
        }
    }
}
