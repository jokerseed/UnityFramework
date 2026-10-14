using System;

namespace Framework.Events
{
    /// <summary>全局事件静态入口（参考 TEngine GameEvent）。</summary>
    public static class GameEvent
    {
        static readonly GameEventMgr Mgr = GameEventMgr.Instance;

        /// <summary>获取全局事件总线实例。</summary>
        public static IEventBus Bus => Mgr.Bus;

        /// <summary>发布一个全局事件，所有订阅该类型的处理器都会被同步调用。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="evt">要发布的事件实例（通过 in 传递，避免拷贝）。</param>
        public static void Send<TEvent>(in TEvent evt) where TEvent : struct
        {
            Mgr.Send(in evt);
        }

        /// <summary>订阅指定事件类型的处理器（重复调用安全，不会重复注册）。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="handler">事件处理委托，不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 null。</exception>
        public static void Listen<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Mgr.Bus.Subscribe(handler);
        }

        /// <summary>取消订阅指定事件类型的处理器。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="handler">要移除的处理委托；为 null 时忽略。</param>
        public static void RemoveListener<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            Mgr.Bus.Unsubscribe(handler);
        }

        /// <summary>订阅指定事件类型，返回可用于自动取消订阅的凭证。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="handler">事件处理委托，不可为 null。</param>
        /// <returns>订阅凭证；调用 Dispose 时自动取消订阅。</returns>
        public static IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            return Mgr.Bus.Subscribe(handler);
        }

        /// <summary>清空所有事件类型的全局订阅。</summary>
        public static void Clear()
        {
            Mgr.Clear();
        }
    }
}
