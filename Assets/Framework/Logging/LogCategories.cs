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

        /// <summary>战斗 Bridge。</summary>
        public const string Bridge = "Bridge";

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
    }
}
