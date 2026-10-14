using Framework.Events;
using Framework.FixedMath;
using Framework.GAS.Events;

namespace Framework.GAS.Cues
{
    /// <summary>GameplayCue 参数。坐标与方向是仿真定点值，表现层自行转成引擎类型。</summary>
    public readonly struct GameplayCueParameters
    {
        /// <summary>相关 Actor。</summary>
        public Core.ActorId Actor { get; }

        /// <summary>位置（仿真坐标）。</summary>
        public TSVector Location { get; }

        /// <summary>方向（已归一化）；传入零向量时取 <see cref="TSVector.forward"/>。</summary>
        public TSVector Direction { get; }

        /// <summary>构造 Cue 参数。</summary>
        /// <param name="actor">相关 Actor。</param>
        /// <param name="location">位置（仿真坐标）。</param>
        /// <param name="direction">方向；内部归一化，零向量时取 <see cref="TSVector.forward"/>。</param>
        public GameplayCueParameters(Core.ActorId actor, TSVector location, TSVector direction)
        {
            Actor = actor;
            Location = location;
            Direction = direction.sqrMagnitude > FP.Zero ? direction.normalized : TSVector.forward;
        }
    }

    /// <summary>GameplayCue 运行时管理接口（对应 UE Cue Executed/OnActive/Removed）。</summary>
    public interface IGameplayCueManager
    {
        /// <summary>瞬时执行 Cue。</summary>
        /// <param name="cueTag">Cue 标签。</param>
        /// <param name="parameters">Cue 参数。</param>
        void ExecuteCue(string cueTag, in GameplayCueParameters parameters);

        /// <summary>添加持续 Cue。</summary>
        /// <param name="cueTag">Cue 标签。</param>
        /// <param name="parameters">Cue 参数。</param>
        void AddCue(string cueTag, in GameplayCueParameters parameters);

        /// <summary>移除持续 Cue。</summary>
        /// <param name="cueTag">Cue 标签。</param>
        /// <param name="parameters">Cue 参数。</param>
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
            Publish(cueTag, parameters, GameplayCueAction.Execute);

        /// <inheritdoc/>
        public void AddCue(string cueTag, in GameplayCueParameters parameters) =>
            Publish(cueTag, parameters, GameplayCueAction.Add);

        /// <inheritdoc/>
        public void RemoveCue(string cueTag, in GameplayCueParameters parameters) =>
            Publish(cueTag, parameters, GameplayCueAction.Remove);

        void Publish(string cueTag, in GameplayCueParameters parameters, GameplayCueAction action)
        {
            _eventBus.Publish(new GameplayCueEvent
            {
                Actor = parameters.Actor,
                CueTag = cueTag,
                Position = parameters.Location,
                Direction = parameters.Direction,
                Action = action
            });
        }
    }
}
