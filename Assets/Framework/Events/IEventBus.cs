using System;

namespace Framework.Events
{
    /// <summary>事件总线接口：发布、订阅、取消订阅与清空。</summary>
    public interface IEventBus
    {
        /// <summary>发布一个事件，所有订阅该事件类型的处理器都会被调用。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="evt">要发布的事件实例。</param>
        void Publish<TEvent>(TEvent evt) where TEvent : struct;

        /// <summary>订阅指定事件类型，返回可用于取消订阅的 <see cref="IDisposable"/>。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="handler">事件处理委托，不可为 null。</param>
        /// <returns>订阅凭证；调用 Dispose 时自动取消订阅。</returns>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;

        /// <summary>取消订阅指定事件类型的处理器。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="handler">要移除的处理委托；为 null 时忽略。</param>
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;

        /// <summary>清空所有事件类型的订阅。</summary>
        void Clear();
    }
}
