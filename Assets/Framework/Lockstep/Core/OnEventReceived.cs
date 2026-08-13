namespace Framework.Lockstep
{
    /// <summary>自定义网络事件接收回调。</summary>
    /// <param name="eventCode">事件码。</param>
    /// <param name="content">事件内容。</param>
    public delegate void OnEventReceived(byte eventCode, object content);
}
