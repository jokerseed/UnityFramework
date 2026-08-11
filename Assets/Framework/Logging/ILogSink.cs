namespace Framework.Logging
{
    /// <summary>日志输出目标（可自定义文件、网络、第三方等）。</summary>
    public interface ILogSink
    {
        void Write(in LogEntry entry);
    }
}
