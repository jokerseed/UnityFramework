namespace Framework.Logging
{
    /// <summary>日志输出目标（可自定义文件、网络、第三方等）。</summary>
    public interface ILogSink
    {
        /// <summary>将日志条目写入输出目标。</summary>
        /// <param name="entry">要写入的日志条目。</param>
        void Write(in LogEntry entry);
    }
}
