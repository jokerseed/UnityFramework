using cfg;

namespace Framework.Config
{
    /// <summary>配置模块对外静态访问：运行时 Tables 缓存。</summary>
    public static class ConfigService
    {
        /// <summary>当前运行时 Tables；需先初始化 <see cref="ConfigModule"/>。</summary>
        public static Tables Tables => ConfigModule.Instance?.Tables;

#if UNITY_EDITOR
        /// <summary>Editor 直读 bin 目录（未走 Res）；Player 不可用。</summary>
        /// <returns>Luban Tables。</returns>
        public static Tables LoadEditorDefault() => ConfigLoader.LoadDefault();
#endif
    }
}
