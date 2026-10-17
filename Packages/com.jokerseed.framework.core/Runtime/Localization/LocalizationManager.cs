using System;
using System.Collections.Generic;
using System.Globalization;
using Framework.Core;
using Framework.Logging;
using UnityEngine;

namespace Framework.Localization
{
    /// <summary>
    /// 本地化入口：当前语言、回退语言与多个文本来源。
    /// 查找顺序：当前语言 → 回退语言；来源按注册顺序，后注册的优先。都找不到时返回键本身并记一次警告。
    /// </summary>
    public sealed class LocalizationManager : PersistentSingleton<LocalizationManager>
    {
        readonly List<ILocalizationSource> _sources = new List<ILocalizationSource>();
        readonly HashSet<string> _reportedMissing = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>当前语言代码。</summary>
        public string Language { get; private set; } = "en";

        /// <summary>当前语言缺失时使用的语言代码；为 null 表示不回退。</summary>
        public string FallbackLanguage { get; set; } = "en";

        /// <summary>切换语言后触发，参数为新语言代码。</summary>
        public event Action<string> LanguageChanged;

        /// <summary>注册文本来源；后注册的来源优先。</summary>
        /// <param name="source">文本来源。</param>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 null。</exception>
        public void AddSource(ILocalizationSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _sources.Add(source);
            _reportedMissing.Clear();
        }

        /// <summary>移除文本来源。</summary>
        /// <param name="source">文本来源。</param>
        /// <returns>存在并移除返回 true。</returns>
        public bool RemoveSource(ILocalizationSource source) => _sources.Remove(source);

        /// <summary>切换语言并触发 <see cref="LanguageChanged"/>；与当前语言相同时不触发。</summary>
        /// <param name="language">语言代码。</param>
        /// <exception cref="ArgumentException"><paramref name="language"/> 为空。</exception>
        public void SetLanguage(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                throw new ArgumentException("Language is required.", nameof(language));
            }

            if (string.Equals(Language, language, StringComparison.Ordinal))
            {
                return;
            }

            Language = language;
            GameLog.Info(LogCategories.Localization, $"Language → {LogStyle.Value(language)}");
            LanguageChanged?.Invoke(language);
        }

        /// <summary>查找文本，不记警告。</summary>
        /// <param name="key">文本键。</param>
        /// <param name="value">找到时输出文本。</param>
        /// <returns>当前语言或回退语言中找到返回 true。</returns>
        public bool TryGet(string key, out string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                value = string.Empty;
                return false;
            }

            if (TryGetIn(Language, key, out value))
            {
                return true;
            }

            return FallbackLanguage != null
                && !string.Equals(FallbackLanguage, Language, StringComparison.Ordinal)
                && TryGetIn(FallbackLanguage, key, out value);
        }

        /// <summary>获取文本；找不到时返回键本身并记一次警告。</summary>
        /// <param name="key">文本键。</param>
        /// <returns>文本。</returns>
        public string Get(string key)
        {
            if (TryGet(key, out var value))
            {
                return value;
            }

            if (!string.IsNullOrEmpty(key) && _reportedMissing.Add(Language + "\n" + key))
            {
                GameLog.Warning(LogCategories.Localization, $"Missing text: {LogStyle.Value(key)} ({Language})");
            }

            return key ?? string.Empty;
        }

        /// <summary>
        /// 获取文本并按当前语言的区域格式化。
        /// 翻译里的占位符写错（例如 <c>{1}</c> 缺参数、花括号不配对）时不抛异常：返回未格式化的原文并记一次警告。
        /// </summary>
        /// <param name="key">文本键。</param>
        /// <param name="args">格式化参数。</param>
        /// <returns>格式化后的文本；格式串无效时为原文。</returns>
        public string Format(string key, params object[] args)
        {
            var template = Get(key);
            try
            {
                return string.Format(GetCulture(), template, args ?? Array.Empty<object>());
            }
            catch (FormatException)
            {
                if (_reportedMissing.Add(Language + "\nformat\n" + key))
                {
                    GameLog.Warning(LogCategories.Localization, $"Invalid format text: {LogStyle.Value(key)} ({Language})");
                }

                return template;
            }
        }

        /// <summary>移除全部来源与事件订阅。</summary>
        public void Shutdown()
        {
            _sources.Clear();
            _reportedMissing.Clear();
            LanguageChanged = null;
        }

        /// <summary>把 Unity 系统语言映射为语言代码（未知时为 <c>en</c>）。</summary>
        /// <param name="language">系统语言。</param>
        /// <returns>语言代码。</returns>
        public static string FromSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                    return "zh-Hans";
                case SystemLanguage.ChineseTraditional:
                    return "zh-Hant";
                case SystemLanguage.Japanese:
                    return "ja";
                case SystemLanguage.Korean:
                    return "ko";
                case SystemLanguage.French:
                    return "fr";
                case SystemLanguage.German:
                    return "de";
                case SystemLanguage.Spanish:
                    return "es";
                case SystemLanguage.Portuguese:
                    return "pt";
                case SystemLanguage.Russian:
                    return "ru";
                case SystemLanguage.Thai:
                    return "th";
                case SystemLanguage.Vietnamese:
                    return "vi";
                case SystemLanguage.Indonesian:
                    return "id";
                default:
                    return "en";
            }
        }

        bool TryGetIn(string language, string key, out string value)
        {
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                if (_sources[i].TryGet(language, key, out value))
                {
                    return true;
                }
            }

            value = null;
            return false;
        }

        CultureInfo GetCulture()
        {
            try
            {
                return CultureInfo.GetCultureInfo(Language);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }
}
