using UnityEngine;

namespace Framework.Logging
{
    /// <summary>输出到 Unity Console 的默认 Sink。</summary>
    public sealed class UnityConsoleLogSink : ILogSink
    {
        readonly LogFormatter _formatter;

        public UnityConsoleLogSink(LogFormatter formatter = null)
        {
            _formatter = formatter ?? GameLog.DefaultFormatter;
        }

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
