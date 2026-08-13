using System;

namespace Framework.Lockstep
{
    /// <summary>
    /// 帧同步网络通讯抽象（原 TrueSync ICommunicator）。由具体网关适配器实现。
    /// </summary>
    public interface ICommunicator
    {
        /// <summary>本地玩家到服务器的往返时延。</summary>
        /// <returns>RTT（实现自定义单位，通常为毫秒）。</returns>
        int RoundTripTime();

        /// <summary>向其他玩家广播自定义事件。</summary>
        /// <param name="eventCode">事件码。</param>
        /// <param name="message">事件体。</param>
        /// <param name="reliable">是否可靠投递。</param>
        /// <param name="toPlayers">目标玩家；实现可约定 null 表示全体。</param>
        void OpRaiseEvent(byte eventCode, object message, bool reliable, int[] toPlayers);

        /// <summary>注册自定义事件监听。</summary>
        /// <param name="onEventReceived">回调。</param>
        void AddEventListener(OnEventReceived onEventReceived);
    }
}
