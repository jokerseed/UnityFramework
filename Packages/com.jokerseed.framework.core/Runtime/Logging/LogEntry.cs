using System;

namespace Framework.Logging
{
    /// <summary>单条日志记录。</summary>
    public readonly struct LogEntry
    {
        /// <summary>构造一条日志记录。</summary>
        /// <param name="level">日志级别。</param>
        /// <param name="category">日志分类；为 null 时使用空字符串。</param>
        /// <param name="message">日志消息；为 null 时使用空字符串。</param>
        /// <param name="exception">关联的异常；无异常时为 null。</param>
        /// <param name="utcTime">日志产生的 UTC 时间。</param>
        public LogEntry(LogLevel level, string category, string message, Exception exception, DateTime utcTime)
        {
            Level = level;
            Category = category ?? string.Empty;
            Message = message ?? string.Empty;
            Exception = exception;
            UtcTime = utcTime;
        }

        /// <summary>日志级别。</summary>
        public LogLevel Level { get; }

        /// <summary>日志分类标识，如 "Bootstrap"、"Resource" 等。</summary>
        public string Category { get; }

        /// <summary>日志消息正文。</summary>
        public string Message { get; }

        /// <summary>关联的异常，无异常时为 null。</summary>
        public Exception Exception { get; }

        /// <summary>日志产生的 UTC 时间。</summary>
        public DateTime UtcTime { get; }
    }
}
