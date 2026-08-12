using System;

namespace Framework.Events
{
    /// <summary>全局零 GC 事件总线，供 <see cref="GameEvent"/> / <see cref="GameEventMgr"/> 使用。</summary>
    public sealed class GlobalEventBus : IEventBus
    {
        internal static readonly GlobalEventBus Instance = new GlobalEventBus();

        GlobalEventBus()
        {
        }

        /// <summary>发布一个全局事件，调用该类型所有已注册的处理器。热路径无字典查找开销。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="evt">要发布的事件实例。</param>
        public void Publish<TEvent>(TEvent evt) where TEvent : struct
        {
            GlobalEventChannel<TEvent>.Handlers.Invoke(in evt);
        }

        /// <summary>订阅指定事件类型，返回可用于取消订阅的凭证。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="handler">事件处理委托，不可为 null。</param>
        /// <returns>订阅凭证；调用 Dispose 时自动取消订阅。</returns>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            GlobalEventChannel<TEvent>.Handlers.Add(handler);
            return new Subscription<TEvent>(handler);
        }

        /// <summary>取消订阅指定事件类型的处理器。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="handler">要移除的处理委托；为 null 时忽略。</param>
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            GlobalEventChannel<TEvent>.Handlers.Remove(handler);
        }

        /// <summary>清空所有已注册全局事件通道的订阅。</summary>
        public void Clear()
        {
            GlobalEventChannelRegistry.ClearAll();
        }

        readonly struct Subscription<TEvent> : IDisposable where TEvent : struct
        {
            readonly Action<TEvent> _handler;

            public Subscription(Action<TEvent> handler)
            {
                _handler = handler;
            }

            public void Dispose()
            {
                if (_handler != null)
                {
                    GlobalEventChannel<TEvent>.Handlers.Remove(_handler);
                }
            }
        }
    }
}
