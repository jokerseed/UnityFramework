namespace Framework.Logging
{
    /// <summary>将 <see cref="LogEntry"/> 格式化为文本。</summary>
    public delegate string LogFormatter(in LogEntry entry);
}
