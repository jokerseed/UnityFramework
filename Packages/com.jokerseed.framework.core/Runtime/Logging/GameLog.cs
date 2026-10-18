using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Framework.Logging
{
    /// <summary>
    /// 全局日志入口：级别/分类过滤、格式化、自定义 Sink。
    /// 由 <see cref="LoggingModule"/> 初始化，也可在编辑器下直接 Configure。
    /// 可在任意线程写日志：Sink 列表与分类过滤表都是只读快照，修改时在锁内整体替换（写时复制），读路径不加锁。
    /// 单个 Sink 抛出的异常会被吞掉，日志系统不会把异常抛给调用方。
    /// </summary>
    public static class GameLog
    {
        /// <summary>分类过滤规则的不可变副本；不引用调用方传入、之后还可能被修改的 <see cref="LogCategoryFilter"/>。</summary>
        sealed class FilterState
        {
            public readonly bool Enabled;
            public readonly bool UseCustomMinLevel;
            public readonly LogLevel MinLevel;

            public FilterState(bool enabled, bool useCustomMinLevel, LogLevel minLevel)
            {
                Enabled = enabled;
                UseCustomMinLevel = useCustomMinLevel;
                MinLevel = minLevel;
            }
        }

        static readonly object Gate = new object();
        static readonly Dictionary<string, FilterState> EmptyFilters =
            new Dictionary<string, FilterState>(StringComparer.Ordinal);

        static volatile ILogSink[] _sinks = Array.Empty<ILogSink>();
        static volatile Dictionary<string, FilterState> _filters = EmptyFilters;
        static volatile LogFormatter _formatter = DefaultFormatter;
        static ILogSink _fallbackSink;
        static volatile bool _configured;
        static volatile bool _useRichText = true;
        static int _minLevel = (int)LogLevel.Info;

        /// <summary>是否已通过 <see cref="Configure"/> 完成初始化配置。</summary>
        public static bool IsConfigured => _configured;

        /// <summary>当前全局最低日志级别。</summary>
        public static LogLevel MinLevel => (LogLevel)Volatile.Read(ref _minLevel);

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

            var filters = new Dictionary<string, FilterState>(StringComparer.Ordinal);
            if (options.CategoryFilters != null)
            {
                for (var i = 0; i < options.CategoryFilters.Length; i++)
                {
                    var filter = options.CategoryFilters[i];
                    if (filter == null || string.IsNullOrWhiteSpace(filter.Category))
                    {
                        continue;
                    }

                    filters[filter.Category] = new FilterState(filter.Enabled, filter.UseCustomMinLevel, filter.MinLevel);
                }
            }

            lock (Gate)
            {
                Volatile.Write(ref _minLevel, (int)options.MinLevel);
                _useRichText = options.UseRichText;
                _filters = filters;
                _configured = true;
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

            lock (Gate)
            {
                var current = _sinks;
                if (Array.IndexOf(current, sink) >= 0)
                {
                    return;
                }

                var next = new ILogSink[current.Length + 1];
                Array.Copy(current, next, current.Length);
                next[current.Length] = sink;
                _sinks = next;
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

            lock (Gate)
            {
                var current = _sinks;
                var index = Array.IndexOf(current, sink);
                if (index < 0)
                {
                    return;
                }

                var next = new ILogSink[current.Length - 1];
                Array.Copy(current, 0, next, 0, index);
                Array.Copy(current, index + 1, next, index, current.Length - index - 1);
                _sinks = next;
            }
        }

        /// <summary>动态设置全局最低日志级别。</summary>
        /// <param name="level">新的最低日志级别。</param>
        public static void SetMinLevel(LogLevel level)
        {
            Volatile.Write(ref _minLevel, (int)level);
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

            UpdateFilter(category, old => new FilterState(
                enabled,
                old != null && old.UseCustomMinLevel,
                old != null ? old.MinLevel : LogLevel.Info));
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

            UpdateFilter(category, old => new FilterState(old == null || old.Enabled, true, level));
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

            if ((int)level < Volatile.Read(ref _minLevel))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(category) ||
                !_filters.TryGetValue(category, out var filter))
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

        /// <summary>
        /// 输出 Trace 级别日志。
        /// 只在 Editor、Development Build 或定义了 <c>FRAMEWORK_LOG_VERBOSE</c> 时编译进调用方；
        /// Release 包里整条调用（包括参数里的字符串拼接）都会被编译器移除。
        /// </summary>
        /// <param name="category">日志分类。</param>
        /// <param name="message">日志消息。</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("FRAMEWORK_LOG_VERBOSE")]
        public static void Trace(string category, string message) => Write(LogLevel.Trace, category, message);

        /// <summary>
        /// 输出 Debug 级别日志。
        /// 只在 Editor、Development Build 或定义了 <c>FRAMEWORK_LOG_VERBOSE</c> 时编译进调用方；
        /// Release 包里整条调用（包括参数里的字符串拼接）都会被编译器移除。
        /// </summary>
        /// <param name="category">日志分类。</param>
        /// <param name="message">日志消息。</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("FRAMEWORK_LOG_VERBOSE")]
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
            lock (Gate)
            {
                _sinks = Array.Empty<ILogSink>();
                _filters = EmptyFilters;
                Volatile.Write(ref _minLevel, (int)LogLevel.Info);
                _formatter = DefaultFormatter;
                _useRichText = true;
                _configured = false;
                Volatile.Write(ref _fallbackSink, null);
            }
        }

        static void UpdateFilter(string category, Func<FilterState, FilterState> update)
        {
            lock (Gate)
            {
                var next = new Dictionary<string, FilterState>(_filters, StringComparer.Ordinal);
                next.TryGetValue(category, out var old);
                next[category] = update(old);
                _filters = next;
            }
        }

        static void Write(LogLevel level, string category, string message, Exception exception = null)
        {
            if (!IsEnabled(category, level))
            {
                return;
            }

            var entry = new LogEntry(level, category, message, exception, DateTime.UtcNow);
            var sinks = _sinks;
            if (sinks.Length == 0)
            {
                var fallback = Volatile.Read(ref _fallbackSink);
                if (fallback == null)
                {
                    // 不固定格式化器：兜底 Sink 始终跟随当前 Formatter。
                    Interlocked.CompareExchange(ref _fallbackSink, new UnityConsoleLogSink(), null);
                    fallback = Volatile.Read(ref _fallbackSink);
                }

                WriteSafe(fallback, in entry);
                return;
            }

            for (var i = 0; i < sinks.Length; i++)
            {
                WriteSafe(sinks[i], in entry);
            }
        }

        static void WriteSafe(ILogSink sink, in LogEntry entry)
        {
            try
            {
                sink.Write(in entry);
            }
            catch
            {
                // 一个 Sink 出错不影响其它 Sink，也不把异常抛给写日志的业务代码。
            }
        }
    }
}
