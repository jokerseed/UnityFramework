using System;
using System.Collections.Generic;

namespace Framework.Core.Events
{
    public sealed class EventBus : IEventBus
    {
        readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
        readonly List<Delegate> _publishScratch = new List<Delegate>(16);

        public void Publish<TEvent>(TEvent evt) where TEvent : struct
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list) || list.Count == 0)
            {
                return;
            }

            _publishScratch.Clear();
            _publishScratch.AddRange(list);
            for (var i = 0; i < _publishScratch.Count; i++)
            {
                ((Action<TEvent>)_publishScratch[i]).Invoke(evt);
            }
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            var type = typeof(TEvent);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _handlers[type] = list;
            }

            list.Add(handler);
            return new Subscription(this, type, handler);
        }

        public void Clear() => _handlers.Clear();

        void Unsubscribe(Type type, Delegate handler)
        {
            if (_handlers.TryGetValue(type, out var list))
            {
                list.Remove(handler);
            }
        }

        sealed class Subscription : IDisposable
        {
            readonly EventBus _bus;
            readonly Type _type;
            readonly Delegate _handler;
            bool _disposed;

            public Subscription(EventBus bus, Type type, Delegate handler)
            {
                _bus = bus;
                _type = type;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _bus.Unsubscribe(_type, _handler);
            }
        }
    }
}
