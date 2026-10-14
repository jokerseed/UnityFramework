using System.Collections.Generic;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

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

        /// <summary>
        /// 进入 Play 时清空静态通道。关闭 Domain Reload 后静态监听会留到下一次 Play。
        /// </summary>
#if UNITY_5_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnSubsystemRegistration()
        {
            ClearAll();
        }
#endif
    }
}
