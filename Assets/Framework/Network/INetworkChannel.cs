using System;
using System.Net;
using System.Net.Sockets;

namespace Framework.Network
{
    /// <summary>网络频道：连接、收发与消息分发。</summary>
    public interface INetworkChannel
    {
        /// <summary>频道名称。</summary>
        string Name { get; }

        /// <summary>当前连接状态。</summary>
        NetworkChannelState State { get; }

        /// <summary>是否已连接。</summary>
        bool Connected { get; }

        /// <summary>底层 Socket；未连接时为 null。</summary>
        Socket Socket { get; }

        /// <summary>服务类型。</summary>
        NetworkServiceType ServiceType { get; }

        /// <summary>心跳间隔（秒）；小于等于 0 表示关闭心跳。</summary>
        float HeartBeatInterval { get; set; }

        /// <summary>收到任意消息时是否重置心跳计时。</summary>
        bool ResetHeartBeatElapseSecondsWhenReceivePacket { get; set; }

        /// <summary>连续丢失心跳次数。</summary>
        int MissHeartBeatCount { get; }

        /// <summary>累计已发送消息数。</summary>
        int SentPacketCount { get; }

        /// <summary>累计已接收消息数。</summary>
        int ReceivedPacketCount { get; }

        /// <summary>连接到远程主机。</summary>
        /// <param name="host">IP 或主机名。</param>
        /// <param name="port">端口。</param>
        void Connect(string host, int port);

        /// <summary>连接到远程主机。</summary>
        /// <param name="address">IP 地址。</param>
        /// <param name="port">端口。</param>
        void Connect(IPAddress address, int port);

        /// <summary>关闭连接。</summary>
        void Close();

        /// <summary>发送消息包；内部会归还 <paramref name="packet"/> 到内存池。</summary>
        /// <param name="packet">消息包，不可为 null。</param>
        void Send(NetworkPacket packet);

        /// <summary>按消息号发送载荷。</summary>
        /// <param name="messageId">消息号。</param>
        /// <param name="payload">消息体；可为 null。</param>
        void Send(ushort messageId, byte[] payload);

        /// <summary>注册消息处理器；同一消息号后注册覆盖先注册。</summary>
        /// <param name="handler">处理器，不可为 null。</param>
        void RegisterHandler(INetworkPacketHandler handler);

        /// <summary>注册指定消息号的委托处理。</summary>
        /// <param name="messageId">消息号。</param>
        /// <param name="handler">处理委托，不可为 null。</param>
        void RegisterHandler(ushort messageId, Action<INetworkChannel, NetworkPacket> handler);

        /// <summary>设置未匹配消息号时的默认处理。</summary>
        /// <param name="handler">默认处理；为 null 时清除。</param>
        void SetDefaultHandler(Action<INetworkChannel, NetworkPacket> handler);
    }
}
