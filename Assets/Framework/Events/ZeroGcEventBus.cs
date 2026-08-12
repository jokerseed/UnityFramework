using System;
using System.Collections.Generic;

namespace Framework.Events
{
    /// <summary>
    /// 零 GC 实例事件总线：每个实例独立通道，适合战斗内表现层隔离。
    /// 全局事件请使用 <see cref="GameEvent"/> / <see cref="GlobalEventBus"/>。
    /// </summary>
    public sealed class ZeroGcEventBus : IEventBus
    {
        readonly Dictionary<Type, IEventChannel> _channels = new Dictionary<Type, IEventChannel>(8);

        /// <summary>发布一个事件，调用该类型所有已注册的处理器。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="evt">要发布的事件实例。</param>
        public void Publish<TEvent>(TEvent evt) where TEvent : struct
        {
            if (_channels.TryGetValue(typeof(TEvent), out var channel))
            {
                ((HandlerList<TEvent>)channel).Invoke(in evt);
            }
        }

        /// <summary>订阅指定事件类型，返回可用于取消订阅的凭证。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="handler">事件处理委托，不可为 null。</param>
        /// <returns>订阅凭证；调用 Dispose 时自动取消订阅。</returns>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            GetOrCreateHandlers<TEvent>().Add(handler);
            return new Subscription<TEvent>(this, handler);
        }

        /// <summary>取消订阅指定事件类型的处理器。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="handler">要移除的处理委托；为 null 时忽略。</param>
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null)
            {
                return;
            }

            if (_channels.TryGetValue(typeof(TEvent), out var channel))
            {
                ((HandlerList<TEvent>)channel).Remove(handler);
            }
        }

        /// <summary>清空所有事件通道及其订阅。</summary>
        public void Clear()
        {
            foreach (var channel in _channels.Values)
            {
                channel.Clear();
            }

            _channels.Clear();
        }

        HandlerList<TEvent> GetOrCreateHandlers<TEvent>() where TEvent : struct
        {
            var type = typeof(TEvent);
            if (!_channels.TryGetValue(type, out var channel))
            {
                channel = new HandlerList<TEvent>();
                _channels[type] = channel;
            }

            return (HandlerList<TEvent>)channel;
        }

        readonly struct Subscription<TEvent> : IDisposable where TEvent : struct
        {
            readonly ZeroGcEventBus _bus;
            readonly Action<TEvent> _handler;

            public Subscription(ZeroGcEventBus bus, Action<TEvent> handler)
            {
                _bus = bus;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_handler != null)
                {
                    _bus.Unsubscribe(_handler);
                }
            }
        }
    }
}
