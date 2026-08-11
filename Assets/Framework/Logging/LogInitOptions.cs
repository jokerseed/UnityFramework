using System;

namespace Framework.Logging
{
    /// <summary>日志模块初始化选项。</summary>
    [Serializable]
    public sealed class LogInitOptions
    {
        public LogLevel MinLevel = LogLevel.Info;
        public bool UnityConsoleEnabled = true;
        public LogCategoryFilter[] CategoryFilters = Array.Empty<LogCategoryFilter>();
    }
}
