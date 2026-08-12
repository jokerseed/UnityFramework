namespace Framework.Res
{
    /// <summary>资源包初始化选项，控制包名、运行模式及网络配置。</summary>
    [System.Serializable]
    public sealed class ResourceInitOptions
    {
        /// <summary>默认资源包名称。</summary>
        public const string DefaultPackageName = "DefaultPackage";

        /// <summary>资源包名称；默认为 <see cref="DefaultPackageName"/>。</summary>
        public string PackageName = DefaultPackageName;

        /// <summary>资源包运行模式。</summary>
        public ResourcePlayMode PlayMode = ResourcePlayMode.EditorSimulate;

        /// <summary>主 CDN 服务器地址；仅 <see cref="ResourcePlayMode.Host"/> 模式下使用。</summary>
        public string HostServerUrl = "http://127.0.0.1/CDN";

        /// <summary>备用 CDN 服务器地址；为空或 null 时回退到 <see cref="HostServerUrl"/>。</summary>
        public string FallbackHostServerUrl;

        /// <summary>Manifest 加载超时时间（秒）。</summary>
        public int ManifestLoadTimeoutSeconds = 60;
    }
}
