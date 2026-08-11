using System;

namespace Framework.Core.Events
{
    public interface IEventBus
    {
        void Publish<TEvent>(TEvent evt) where TEvent : struct;
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
        void Clear();
    }
}
