using UnityEngine;

namespace Framework.Logging
{
    /// <summary>输出到 Unity Console 的默认 Sink。</summary>
    public sealed class UnityConsoleLogSink : ILogSink
    {
        readonly LogFormatter _formatter;

        /// <summary>构造 Unity Console Sink。</summary>
        /// <param name="formatter">自定义格式化委托；为 null 时使用 <see cref="GameLog.DefaultFormatter"/>。</param>
        public UnityConsoleLogSink(LogFormatter formatter = null)
        {
            _formatter = formatter ?? GameLog.DefaultFormatter;
        }

        /// <summary>将日志条目写入 Unity Console，根据级别使用对应的 Debug.Log / LogWarning / LogError。</summary>
        /// <param name="entry">要写入的日志条目。</param>
        public void Write(in LogEntry entry)
        {
            var text = _formatter(entry);
            if (entry.Exception != null)
            {
                text = $"{text}\n{entry.Exception}";
            }

            switch (entry.Level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(text);
                    break;
                case LogLevel.Error:
                case LogLevel.Exception:
                    Debug.LogError(text);
                    break;
                default:
                    Debug.Log(text);
                    break;
            }
        }
    }
}
