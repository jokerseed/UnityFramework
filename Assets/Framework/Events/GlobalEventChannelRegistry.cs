using System.Collections.Generic;

namespace Framework.Events
{
    /// <summary>注册全局泛型事件通道，供 <see cref="GlobalEventBus.Clear"/> 使用。</summary>
    internal static class GlobalEventChannelRegistry
    {
        static readonly List<IEventChannel> Channels = new List<IEventChannel>(32);

        internal static void Register(IEventChannel channel)
        {
            Channels.Add(channel);
        }

        internal static void ClearAll()
        {
            for (var i = 0; i < Channels.Count; i++)
            {
                Channels[i].Clear();
            }
        }
    }
}
