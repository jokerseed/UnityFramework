namespace Framework.Res
{
    /// <summary>资源包运行模式。</summary>
    public enum ResourcePlayMode
    {
        /// <summary>Editor 模拟模式，无需打真实 Bundle。</summary>
        EditorSimulate = 0,

        /// <summary>本地离线包。</summary>
        Offline = 1,

        /// <summary>CDN 热更新模式。</summary>
        Host = 2,
    }
}
