namespace Framework.Logging
{
    /// <summary>常用日志分类常量。</summary>
    public static class LogCategories
    {
        /// <summary>启动与 Bootstrap 模块编排。</summary>
        public const string Bootstrap = "Bootstrap";

        /// <summary>资源模块。</summary>
        public const string Resource = "Resource";

        /// <summary>Launch 入口脚本。</summary>
        public const string Launch = "Launch";

        /// <summary>GAS 战斗规则。</summary>
        public const string Gas = "GAS";

        /// <summary>ECS 模拟。</summary>
        public const string Ecs = "ECS";

        /// <summary>玩法模块（GAS + ECS 编排）。</summary>
        public const string GamePlay = "GamePlay";

        /// <summary>编辑器工具。</summary>
        public const string Editor = "Editor";

        /// <summary>Luban 打表。</summary>
        public const string Luban = "Luban";

        /// <summary>YooAsset 资源管线。</summary>
        public const string YooAsset = "YooAsset";

        /// <summary>内存池。</summary>
        public const string MemoryPool = "MemoryPool";

        /// <summary>对象池。</summary>
        public const string ObjectPool = "ObjectPool";

        /// <summary>协程模块。</summary>
        public const string Coroutine = "Coroutine";

        /// <summary>配置模块。</summary>
        public const string Config = "Config";

        /// <summary>UI 模块。</summary>
        public const string UI = "UI";

        /// <summary>网络模块。</summary>
        public const string Network = "Network";

        /// <summary>音频模块。</summary>
        public const string Audio = "Audio";

        /// <summary>事件总线。</summary>
        public const string Events = "Events";

        /// <summary>流程状态机。</summary>
        public const string Procedure = "Procedure";

        /// <summary>定时器。</summary>
        public const string Timer = "Timer";

        /// <summary>存档与设置。</summary>
        public const string Save = "Save";

        /// <summary>本地化。</summary>
        public const string Localization = "Localization";

        /// <summary>调试控制台。</summary>
        public const string Debug = "Debug";

        /// <summary>资源补丁（版本检查与下载）。</summary>
        public const string Patch = "Patch";
    }
}
