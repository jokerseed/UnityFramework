using YooAsset;

namespace Framework.Res
{
    [System.Serializable]
    public sealed class ResourceInitOptions
    {
        public const string DefaultPackageName = "DefaultPackage";

        public string PackageName = DefaultPackageName;
        public EPlayMode PlayMode = EPlayMode.EditorSimulateMode;
        public string HostServerUrl = "http://127.0.0.1/CDN";
        public string FallbackHostServerUrl;
        public int ManifestLoadTimeoutSeconds = 60;
    }
}
