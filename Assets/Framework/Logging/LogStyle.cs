namespace Framework.Logging
{
    /// <summary>
    /// Unity Console 富文本样式：级别/分类着色，以及正文中的重点高亮。
    /// <see cref="GameLog.UseRichText"/> 为 false 时所有方法退回纯文本。
    /// </summary>
    public static class LogStyle
    {
        const string TimeHex = "#9E9E9E";
        const string SepHex = "#616161";
        const string AccentHex = "#FFD54F";
        const string NameHex = "#80DEEA";
        const string SuccessHex = "#66BB6A";
        const string DangerHex = "#EF5350";
        const string MutedHex = "#BDBDBD";

        static readonly string[] CategoryPalette =
        {
            "#7E57C2", "#5C6BC0", "#42A5F5", "#26C6DA", "#26A69A",
            "#66BB6A", "#9CCC65", "#FFA726", "#FF7043", "#EC407A",
            "#AB47BC", "#8D6E63",
        };

        /// <summary>层级分隔符。</summary>
        public static string Sep => GameLog.UseRichText ? Colorize("│", SepHex) : "|";

        /// <summary>次要信息（时间、模式说明等）。</summary>
        /// <param name="value">要显示的内容。</param>
        /// <returns>着色或纯文本。</returns>
        public static string Muted(object value) => Paint(value, MutedHex, bold: false);

        /// <summary>标识名（组名、模块名、包名等）。</summary>
        /// <param name="value">要显示的内容。</param>
        /// <returns>加粗着色或纯文本。</returns>
        public static string Name(object value) => Paint(value, NameHex, bold: true);

        /// <summary>关键数值 / 状态字面量。</summary>
        /// <param name="value">要显示的内容。</param>
        /// <returns>加粗着色或纯文本。</returns>
        public static string Value(object value) => Paint(value, AccentHex, bold: true);

        /// <summary>成功 / 就绪。</summary>
        /// <param name="value">要显示的内容。</param>
        /// <returns>加粗着色或纯文本。</returns>
        public static string Ok(object value) => Paint(value, SuccessHex, bold: true);

        /// <summary>失败 / 错误原因。</summary>
        /// <param name="value">要显示的内容。</param>
        /// <returns>加粗着色或纯文本。</returns>
        public static string Fail(object value) => Paint(value, DangerHex, bold: true);

        /// <summary>Unity Console 富文本行：时间 │ 级别 │ 分类 │ 正文。</summary>
        /// <param name="entry">日志条目。</param>
        /// <returns>富文本字符串。</returns>
        public static string FormatRich(in LogEntry entry)
        {
            var time = Colorize(entry.UtcTime.ToLocalTime().ToString("HH:mm:ss.fff"), TimeHex);
            return $"{time} {Sep} {FormatLevel(entry.Level)} {Sep} {FormatCategory(entry.Category)} {Sep} {entry.Message}";
        }

        /// <summary>纯文本行：时间 | 级别 | 分类 | 正文。</summary>
        /// <param name="entry">日志条目。</param>
        /// <returns>纯文本字符串。</returns>
        public static string FormatPlain(in LogEntry entry)
        {
            var time = entry.UtcTime.ToLocalTime().ToString("HH:mm:ss.fff");
            return $"{time} | {LevelLabel(entry.Level),-5} | {entry.Category} | {entry.Message}";
        }

        static string FormatLevel(LogLevel level)
        {
            var label = LevelLabel(level);
            if (!GameLog.UseRichText)
            {
                return label;
            }

            switch (level)
            {
                case LogLevel.Trace:
                    return Colorize(label, "#78909C");
                case LogLevel.Debug:
                    return Colorize(label, "#4DD0E1");
                case LogLevel.Info:
                    return Bold(Colorize(label, "#42A5F5"));
                case LogLevel.Warning:
                    return Bold(Colorize(label, "#FFA726"));
                case LogLevel.Error:
                    return Bold(Colorize(label, "#EF5350"));
                case LogLevel.Exception:
                    return Bold(Colorize(label, "#FF1744"));
                default:
                    return label;
            }
        }

        static string FormatCategory(string category)
        {
            var label = string.IsNullOrEmpty(category) ? "-" : category;
            if (!GameLog.UseRichText)
            {
                return label;
            }

            return Bold(Colorize(label, ResolveCategoryHex(label)));
        }

        static string LevelLabel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Trace:
                    return "TRACE";
                case LogLevel.Debug:
                    return "DEBUG";
                case LogLevel.Info:
                    return "INFO";
                case LogLevel.Warning:
                    return "WARN";
                case LogLevel.Error:
                    return "ERROR";
                case LogLevel.Exception:
                    return "EXC";
                default:
                    return level.ToString().ToUpperInvariant();
            }
        }

        static string ResolveCategoryHex(string category)
        {
            switch (category)
            {
                case LogCategories.Bootstrap:
                    return "#7E57C2";
                case LogCategories.Launch:
                    return "#26A69A";
                case LogCategories.Resource:
                    return "#66BB6A";
                case LogCategories.Coroutine:
                    return "#29B6F6";
                case LogCategories.MemoryPool:
                    return "#8D6E63";
                case LogCategories.ObjectPool:
                    return "#5C6BC0";
                case LogCategories.YooAsset:
                    return "#26C6DA";
                case LogCategories.Luban:
                    return "#EC407A";
                case LogCategories.Gas:
                    return "#FF7043";
                case LogCategories.Ecs:
                    return "#AB47BC";
                case LogCategories.GamePlay:
                    return "#42A5F5";
                case LogCategories.Config:
                    return "#78909C";
                case LogCategories.UI:
                    return "#66BB6A";
                case LogCategories.Editor:
                    return "#90A4AE";
                default:
                    var index = (category.GetHashCode() & int.MaxValue) % CategoryPalette.Length;
                    return CategoryPalette[index];
            }
        }

        static string Paint(object value, string hex, bool bold)
        {
            var text = value != null ? value.ToString() : "null";
            if (!GameLog.UseRichText)
            {
                return text;
            }

            var colored = Colorize(text, hex);
            return bold ? Bold(colored) : colored;
        }

        static string Colorize(string text, string hex) => $"<color={hex}>{text}</color>";

        static string Bold(string text) => $"<b>{text}</b>";
    }
}
