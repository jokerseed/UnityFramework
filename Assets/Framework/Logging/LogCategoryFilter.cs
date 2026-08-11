using System;

namespace Framework.Logging
{
    /// <summary>单个分类的过滤规则。</summary>
    [Serializable]
    public sealed class LogCategoryFilter
    {
        public string Category = string.Empty;
        public bool Enabled = true;
        public bool UseCustomMinLevel;
        public LogLevel MinLevel = LogLevel.Info;
    }
}
