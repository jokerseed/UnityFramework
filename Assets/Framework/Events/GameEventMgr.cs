using Framework.Core.Events;

namespace Framework.Events
{
    /// <summary>全局事件管理器（参考 TEngine EventMgr）。</summary>
    public sealed class GameEventMgr
    {
        public static GameEventMgr Instance { get; } = new GameEventMgr();

        public IEventBus Bus => GlobalEventBus.Instance;

        public void Send<TEvent>(in TEvent evt) where TEvent : struct
        {
            GlobalEventBus.Instance.Publish(evt);
        }

        public void Clear()
        {
            GlobalEventBus.Instance.Clear();
        }
    }
}
