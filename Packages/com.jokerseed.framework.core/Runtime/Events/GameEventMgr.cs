namespace Framework.Events
{
    /// <summary>全局事件管理器（参考 TEngine EventMgr）。</summary>
    public sealed class GameEventMgr
    {
        /// <summary>获取全局唯一的 <see cref="GameEventMgr"/> 实例。</summary>
        public static GameEventMgr Instance { get; } = new GameEventMgr();

        /// <summary>获取底层全局事件总线。</summary>
        public IEventBus Bus => GlobalEventBus.Instance;

        /// <summary>通过全局总线发布一个事件。</summary>
        /// <typeparam name="TEvent">事件类型，必须为值类型（struct）。</typeparam>
        /// <param name="evt">要发布的事件实例（通过 in 传递，避免拷贝）。</param>
        public void Send<TEvent>(in TEvent evt) where TEvent : struct
        {
            GlobalEventBus.Instance.Publish(evt);
        }

        /// <summary>清空所有已注册全局事件通道的订阅。</summary>
        public void Clear()
        {
            GlobalEventBus.Instance.Clear();
        }
    }
}
