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
        static bool _useRichText = true;
        static ILogSink _fallbackSink;

        /// <summary>是否已通过 <see cref="Configure"/> 完成初始化配置。</summary>
        public static bool IsConfigured => _configured;

        /// <summary>当前全局最低日志级别。</summary>
        public static LogLevel MinLevel => _minLevel;

        /// <summary>是否对默认格式使用 Unity 富文本（颜色 / 加粗）。</summary>
        public static bool UseRichText => _useRichText;

        /// <summary>当前日志格式化委托；设为 null 时自动回退到 <see cref="DefaultFormatter"/>。</summary>
        public static LogFormatter Formatter
        {
            get => _formatter;
            set => _formatter = value ?? DefaultFormatter;
        }

        /// <summary>默认格式化：富文本为「时间 │ 级别 │ 分类 │ 正文」，纯文本同结构用 | 分隔。</summary>
        /// <param name="entry">要格式化的日志条目。</param>
        /// <returns>格式化后的文本。</returns>
        public static string DefaultFormatter(in LogEntry entry)
        {
            return _useRichText ? LogStyle.FormatRich(in entry) : LogStyle.FormatPlain(in entry);
        }

        /// <summary>
        /// 根据选项初始化日志系统；会先调用 <see cref="Shutdown"/> 清除旧配置。
        /// </summary>
        /// <param name="options">初始化选项，不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> 为 null。</exception>
        public static void Configure(LogInitOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            Shutdown();

            _minLevel = options.MinLevel;
            _useRichText = options.UseRichText;
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
                AddSink(new UnityConsoleLogSink());
            }
        }

        /// <summary>添加自定义日志 Sink；同一实例不会重复添加。</summary>
        /// <param name="sink">要添加的 Sink，不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="sink"/> 为 null。</exception>
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

        /// <summary>移除已添加的日志 Sink；传入 null 时无操作。</summary>
        /// <param name="sink">要移除的 Sink；为 null 时无操作。</param>
        public static void RemoveSink(ILogSink sink)
        {
            if (sink == null)
            {
                return;
            }

            Sinks.Remove(sink);
        }

        /// <summary>动态设置全局最低日志级别。</summary>
        /// <param name="level">新的最低日志级别。</param>
        public static void SetMinLevel(LogLevel level)
        {
            _minLevel = level;
        }

        /// <summary>启用或禁用指定分类的日志输出。</summary>
        /// <param name="category">分类名称；为空或空白字符串时无操作。</param>
        /// <param name="enabled">true 为启用，false 为禁用。</param>
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

        /// <summary>为指定分类设置自定义最低日志级别。</summary>
        /// <param name="category">分类名称；为空或空白字符串时无操作。</param>
        /// <param name="level">该分类的自定义最低级别。</param>
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

        /// <summary>判断指定分类与级别的日志是否应当输出。</summary>
        /// <param name="category">日志分类名称。</param>
        /// <param name="level">日志级别。</param>
        /// <returns>应输出则返回 true，被过滤则返回 false。</returns>
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

        /// <summary>输出 Trace 级别日志。</summary>
        /// <param name="category">日志分类。</param>
        /// <param name="message">日志消息。</param>
        public static void Trace(string category, string message) => Write(LogLevel.Trace, category, message);

        /// <summary>输出 Debug 级别日志。</summary>
        /// <param name="category">日志分类。</param>
        /// <param name="message">日志消息。</param>
        public static void Debug(string category, string message) => Write(LogLevel.Debug, category, message);

        /// <summary>输出 Info 级别日志。</summary>
        /// <param name="category">日志分类。</param>
        /// <param name="message">日志消息。</param>
        public static void Info(string category, string message) => Write(LogLevel.Info, category, message);

        /// <summary>输出 Warning 级别日志。</summary>
        /// <param name="category">日志分类。</param>
        /// <param name="message">日志消息。</param>
        public static void Warning(string category, string message) => Write(LogLevel.Warning, category, message);

        /// <summary>输出 Error 级别日志。</summary>
        /// <param name="category">日志分类。</param>
        /// <param name="message">日志消息。</param>
        public static void Error(string category, string message) => Write(LogLevel.Error, category, message);

        /// <summary>输出异常日志（<see cref="LogLevel.Exception"/> 级别）。</summary>
        /// <param name="category">日志分类。</param>
        /// <param name="exception">要记录的异常，不可为 null。</param>
        /// <param name="message">附加消息；为 null 时使用 <see cref="Exception.Message"/>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="exception"/> 为 null。</exception>
        public static void Exception(string category, Exception exception, string message = null)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            Write(LogLevel.Exception, category, message ?? exception.Message, exception);
        }

        /// <summary>关闭日志系统，清除所有 Sink、过滤规则并重置配置状态。</summary>
        public static void Shutdown()
        {
            Sinks.Clear();
            CategoryFilters.Clear();
            _minLevel = LogLevel.Info;
            _formatter = DefaultFormatter;
            _useRichText = true;
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
