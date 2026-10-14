namespace Framework.Logging
{
    /// <summary>将 <see cref="LogEntry"/> 格式化为文本字符串的委托。</summary>
    /// <param name="entry">要格式化的日志条目。</param>
    /// <returns>格式化后的文本内容。</returns>
    public delegate string LogFormatter(in LogEntry entry);
}
