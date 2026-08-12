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

        public void Publish<TEvent>(TEvent evt) where TEvent : struct
        {
            if (_channels.TryGetValue(typeof(TEvent), out var channel))
            {
                ((HandlerList<TEvent>)channel).Invoke(in evt);
            }
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            GetOrCreateHandlers<TEvent>().Add(handler);
            return new Subscription<TEvent>(this, handler);
        }

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
