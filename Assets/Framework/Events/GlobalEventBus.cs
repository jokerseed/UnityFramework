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

        public void Publish<TEvent>(TEvent evt) where TEvent : struct
        {
            GlobalEventChannel<TEvent>.Handlers.Invoke(in evt);
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            GlobalEventChannel<TEvent>.Handlers.Add(handler);
            return new Subscription<TEvent>(handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            GlobalEventChannel<TEvent>.Handlers.Remove(handler);
        }

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
