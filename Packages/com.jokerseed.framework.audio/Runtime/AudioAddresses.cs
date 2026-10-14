namespace Framework.Audio
{
    /// <summary>音频资源 YooAsset 寻址辅助；路径规则与 <see cref="Framework.Res.ResourceAddresses"/> 一致。</summary>
    public static class AudioAddresses
    {
        /// <summary>音频资源根路径（对应 YooAsset Collector 中的 Assets/Bundles/Audio）。</summary>
        public const string AudioRoot = "Bundles/Audio";

        /// <summary>
        /// 将音频资源名转换为 YooAsset 寻址字符串。
        /// 例如：<c>bgm_main</c> → <c>bundles/audio/bgm_main.unity3d</c>
        /// </summary>
        /// <param name="clipName">音频资源名（不含扩展名）。</param>
        /// <returns>对应的 YooAsset 寻址字符串（全小写）。</returns>
        public static string Clip(string clipName)
        {
            return $"{AudioRoot}/{clipName}.unity3d".ToLower();
        }
    }
}
