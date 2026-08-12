using System;

namespace Framework.Logging
{
    /// <summary>单个分类的过滤规则。</summary>
    [Serializable]
    public sealed class LogCategoryFilter
    {
        /// <summary>目标分类名称；与 <see cref="LogCategories"/> 中的常量对应。</summary>
        public string Category = string.Empty;

        /// <summary>是否启用该分类的日志输出；false 时该分类所有日志均被过滤。</summary>
        public bool Enabled = true;

        /// <summary>是否为该分类使用自定义最低日志级别。</summary>
        public bool UseCustomMinLevel;

        /// <summary>该分类的自定义最低日志级别，仅在 <see cref="UseCustomMinLevel"/> 为 true 时生效。</summary>
        public LogLevel MinLevel = LogLevel.Info;
    }
}
