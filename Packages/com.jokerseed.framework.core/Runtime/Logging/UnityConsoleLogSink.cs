#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace Framework.Logging
{
    /// <summary>
    /// 默认 Sink：在 Unity 里写入 Console（<c>Debug.Log</c> / <c>LogWarning</c> / <c>LogError</c>）；
    /// 在纯 .NET（服务端模拟、dotnet test）里写标准输出 / 标准错误。
    /// </summary>
    public sealed class UnityConsoleLogSink : ILogSink
    {
        readonly LogFormatter _formatter;

        /// <summary>构造默认 Sink。</summary>
        /// <param name="formatter">自定义格式化委托；为 null 时始终使用当前 <see cref="GameLog.Formatter"/>。</param>
        public UnityConsoleLogSink(LogFormatter formatter = null)
        {
            _formatter = formatter;
        }

        /// <summary>按级别写入日志条目。</summary>
        /// <param name="entry">要写入的日志条目。</param>
        public void Write(in LogEntry entry)
        {
            var formatter = _formatter ?? GameLog.Formatter;
            var text = formatter(entry);
            if (entry.Exception != null)
            {
                text = $"{text}\n{entry.Exception}";
            }

#if UNITY_5_3_OR_NEWER
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
#else
            if (entry.Level >= LogLevel.Warning)
            {
                System.Console.Error.WriteLine(text);
            }
            else
            {
                System.Console.WriteLine(text);
            }
#endif
        }
    }
}
