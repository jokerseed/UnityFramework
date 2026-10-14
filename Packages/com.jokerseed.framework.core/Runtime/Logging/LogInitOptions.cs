using System;

namespace Framework.Logging
{
    /// <summary>日志模块初始化选项。</summary>
    [Serializable]
    public sealed class LogInitOptions
    {
        /// <summary>全局最低日志级别，低于该级别的日志将被过滤掉。</summary>
        public LogLevel MinLevel = LogLevel.Info;

        /// <summary>是否启用 Unity Console 输出（<see cref="UnityConsoleLogSink"/>）。</summary>
        public bool UnityConsoleEnabled = true;

        /// <summary>是否使用富文本（级别/分类颜色、加粗重点）。关闭后输出纯文本，便于文件 Sink。</summary>
        public bool UseRichText = true;

        /// <summary>分类过滤规则列表；可为空数组，空时不做额外过滤。</summary>
        public LogCategoryFilter[] CategoryFilters = Array.Empty<LogCategoryFilter>();
    }
}
