using System;

namespace Framework.Logging
{
    /// <summary>单条日志记录。</summary>
    public readonly struct LogEntry
    {
        public LogEntry(LogLevel level, string category, string message, Exception exception, DateTime utcTime)
        {
            Level = level;
            Category = category ?? string.Empty;
            Message = message ?? string.Empty;
            Exception = exception;
            UtcTime = utcTime;
        }

        public LogLevel Level { get; }
        public string Category { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public DateTime UtcTime { get; }
    }
}
