using System;
using Framework.Core.Events;

namespace Framework.Events
{
    /// <summary>全局事件静态入口（参考 TEngine GameEvent）。</summary>
    public static class GameEvent
    {
        static readonly GameEventMgr Mgr = GameEventMgr.Instance;

        public static IEventBus Bus => Mgr.Bus;

        public static void Send<TEvent>(in TEvent evt) where TEvent : struct
        {
            Mgr.Send(in evt);
        }

        public static void Listen<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Mgr.Bus.Subscribe(handler);
        }

        public static void RemoveListener<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            Mgr.Bus.Unsubscribe(handler);
        }

        public static IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            return Mgr.Bus.Subscribe(handler);
        }

        public static void Clear()
        {
            Mgr.Clear();
        }
    }
}
