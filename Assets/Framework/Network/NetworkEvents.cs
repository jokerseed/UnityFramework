using System.Net.Sockets;

namespace Framework.Network
{
    /// <summary>网络频道连接成功。</summary>
    public struct NetworkConnectedEvent
    {
        /// <summary>频道名称。</summary>
        public string ChannelName;
    }

    /// <summary>网络频道关闭。</summary>
    public struct NetworkClosedEvent
    {
        /// <summary>频道名称。</summary>
        public string ChannelName;
    }

    /// <summary>网络频道错误。</summary>
    public struct NetworkErrorEvent
    {
        /// <summary>频道名称。</summary>
        public string ChannelName;

        /// <summary>错误分类。</summary>
        public NetworkErrorCode ErrorCode;

        /// <summary>Socket 错误码。</summary>
        public SocketError SocketError;

        /// <summary>错误描述。</summary>
        public string Message;
    }

    /// <summary>心跳超时。</summary>
    public struct NetworkMissHeartBeatEvent
    {
        /// <summary>频道名称。</summary>
        public string ChannelName;

        /// <summary>连续丢失次数。</summary>
        public int MissCount;
    }
}
