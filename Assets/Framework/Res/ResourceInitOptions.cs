namespace Framework.Res
{
    [System.Serializable]
    public sealed class ResourceInitOptions
    {
        public const string DefaultPackageName = "DefaultPackage";

        public string PackageName = DefaultPackageName;
        public ResourcePlayMode PlayMode = ResourcePlayMode.EditorSimulate;
        public string HostServerUrl = "http://127.0.0.1/CDN";
        public string FallbackHostServerUrl;
        public int ManifestLoadTimeoutSeconds = 60;
    }
}
