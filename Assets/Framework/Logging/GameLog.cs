using System;
using System.Collections.Generic;

namespace Framework.Logging
{
    /// <summary>
    /// 全局日志入口：级别/分类过滤、格式化、自定义 Sink。
    /// 由 <see cref="LoggingModule"/> 初始化，也可在编辑器下直接 Configure。
    /// </summary>
    public static class GameLog
    {
        static readonly List<ILogSink> Sinks = new List<ILogSink>();
        static readonly Dictionary<string, LogCategoryFilter> CategoryFilters =
            new Dictionary<string, LogCategoryFilter>(StringComparer.Ordinal);

        static LogLevel _minLevel = LogLevel.Info;
        static LogFormatter _formatter = DefaultFormatter;
        static bool _configured;
        static ILogSink _fallbackSink;

        public static bool IsConfigured => _configured;
        public static LogLevel MinLevel => _minLevel;

        public static LogFormatter Formatter
        {
            get => _formatter;
            set => _formatter = value ?? DefaultFormatter;
        }

        public static string DefaultFormatter(in LogEntry entry)
        {
            return $"[{entry.Category}] {entry.Message}";
        }

        public static void Configure(LogInitOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            Shutdown();

            _minLevel = options.MinLevel;
            _configured = true;

            CategoryFilters.Clear();
            if (options.CategoryFilters != null)
            {
                for (var i = 0; i < options.CategoryFilters.Length; i++)
                {
                    var filter = options.CategoryFilters[i];
                    if (filter == null || string.IsNullOrWhiteSpace(filter.Category))
                    {
                        continue;
                    }

                    CategoryFilters[filter.Category] = filter;
                }
            }

            if (options.UnityConsoleEnabled)
            {
                AddSink(new UnityConsoleLogSink(_formatter));
            }
        }

        public static void AddSink(ILogSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            if (!Sinks.Contains(sink))
            {
                Sinks.Add(sink);
            }
        }

        public static void RemoveSink(ILogSink sink)
        {
            if (sink == null)
            {
                return;
            }

            Sinks.Remove(sink);
        }

        public static void SetMinLevel(LogLevel level)
        {
            _minLevel = level;
        }

        public static void SetCategoryEnabled(string category, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return;
            }

            if (!CategoryFilters.TryGetValue(category, out var filter))
            {
                filter = new LogCategoryFilter { Category = category };
                CategoryFilters[category] = filter;
            }

            filter.Enabled = enabled;
        }

        public static void SetCategoryMinLevel(string category, LogLevel level)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return;
            }

            if (!CategoryFilters.TryGetValue(category, out var filter))
            {
                filter = new LogCategoryFilter { Category = category };
                CategoryFilters[category] = filter;
            }

            filter.UseCustomMinLevel = true;
            filter.MinLevel = level;
        }

        public static bool IsEnabled(string category, LogLevel level)
        {
            if (!_configured)
            {
                return true;
            }

            if (level < _minLevel)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(category) ||
                !CategoryFilters.TryGetValue(category, out var filter))
            {
                return true;
            }

            if (!filter.Enabled)
            {
                return false;
            }

            if (filter.UseCustomMinLevel && level < filter.MinLevel)
            {
                return false;
            }

            return true;
        }

        public static void Trace(string category, string message) => Write(LogLevel.Trace, category, message);
        public static void Debug(string category, string message) => Write(LogLevel.Debug, category, message);
        public static void Info(string category, string message) => Write(LogLevel.Info, category, message);
        public static void Warning(string category, string message) => Write(LogLevel.Warning, category, message);
        public static void Error(string category, string message) => Write(LogLevel.Error, category, message);

        public static void Exception(string category, Exception exception, string message = null)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            Write(LogLevel.Exception, category, message ?? exception.Message, exception);
        }

        public static void Shutdown()
        {
            Sinks.Clear();
            CategoryFilters.Clear();
            _minLevel = LogLevel.Info;
            _formatter = DefaultFormatter;
            _configured = false;
            _fallbackSink = null;
        }

        static void Write(LogLevel level, string category, string message, Exception exception = null)
        {
            if (!IsEnabled(category, level))
            {
                return;
            }

            var entry = new LogEntry(level, category, message, exception, DateTime.UtcNow);
            if (Sinks.Count == 0)
            {
                _fallbackSink ??= new UnityConsoleLogSink(_formatter);
                _fallbackSink.Write(in entry);
                return;
            }

            for (var i = 0; i < Sinks.Count; i++)
            {
                Sinks[i].Write(in entry);
            }
        }
    }
}
