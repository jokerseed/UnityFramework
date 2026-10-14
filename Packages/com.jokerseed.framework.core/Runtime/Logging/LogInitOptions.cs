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

        /// <summary>是否写滚动日志文件（<see cref="FileLogSink"/>）。WebGL 下忽略。</summary>
        public bool FileEnabled;

        /// <summary>日志目录；为空时使用 <c>Application.persistentDataPath/Logs</c>。</summary>
        public string FileDirectory = "";

        /// <summary>单个日志文件大小上限（KB）。</summary>
        public int FileMaxKilobytes = 4096;

        /// <summary>保留的日志文件个数（含当前文件）。</summary>
        public int FileMaxCount = 5;

        /// <summary>是否把未处理异常（<c>AppDomain.UnhandledException</c>、未观察的 Task 异常、Unity 主线程异常）转写到 <see cref="GameLog"/>。</summary>
        public bool CaptureUnhandledExceptions = true;
    }
}
