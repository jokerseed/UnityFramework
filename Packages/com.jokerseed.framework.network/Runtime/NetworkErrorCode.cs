namespace Framework.Network
{
    /// <summary>网络错误分类。</summary>
    public enum NetworkErrorCode
    {
        /// <summary>未知错误。</summary>
        Unknown = 0,

        /// <summary>地址解析失败。</summary>
        AddressError = 1,

        /// <summary>连接失败。</summary>
        ConnectError = 2,

        /// <summary>发送失败。</summary>
        SendError = 3,

        /// <summary>接收失败。</summary>
        ReceiveError = 4,

        /// <summary>包头或包体解析失败。</summary>
        DeserializeError = 5,
    }
}
