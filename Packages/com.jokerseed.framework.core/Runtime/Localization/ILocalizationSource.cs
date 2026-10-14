using System;
using System.Collections.Generic;

namespace Framework.Localization
{
    /// <summary>
    /// 本地化文本来源。Luban 表、JSON、远端下发等实现本接口后注册到 <see cref="LocalizationManager"/>。
    /// </summary>
    public interface ILocalizationSource
    {
        /// <summary>查找文本。</summary>
        /// <param name="language">语言代码（如 <c>zh-Hans</c>、<c>en</c>）。</param>
        /// <param name="key">文本键。</param>
        /// <param name="value">找到时输出文本。</param>
        /// <returns>找到返回 true。</returns>
        bool TryGet(string language, string key, out string value);
    }

    /// <summary>内存字典来源；适合测试、少量内置文本，或由业务把配置表灌进来。</summary>
    public sealed class DictionaryLocalizationSource : ILocalizationSource
    {
        readonly Dictionary<string, Dictionary<string, string>> _tables =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        /// <summary>添加或覆盖一条文本。</summary>
        /// <param name="language">语言代码。</param>
        /// <param name="key">文本键。</param>
        /// <param name="value">文本。</param>
        /// <returns>当前实例，便于链式调用。</returns>
        public DictionaryLocalizationSource Set(string language, string key, string value)
        {
            if (!_tables.TryGetValue(language, out var table))
            {
                table = new Dictionary<string, string>(StringComparer.Ordinal);
                _tables.Add(language, table);
            }

            table[key] = value;
            return this;
        }

        /// <summary>批量添加或覆盖某语言的文本。</summary>
        /// <param name="language">语言代码。</param>
        /// <param name="entries">键值对。</param>
        /// <returns>当前实例，便于链式调用。</returns>
        public DictionaryLocalizationSource SetRange(string language, IEnumerable<KeyValuePair<string, string>> entries)
        {
            foreach (var pair in entries)
            {
                Set(language, pair.Key, pair.Value);
            }

            return this;
        }

        /// <inheritdoc />
        public bool TryGet(string language, string key, out string value)
        {
            if (_tables.TryGetValue(language, out var table) && table.TryGetValue(key, out value))
            {
                return true;
            }

            value = null;
            return false;
        }
    }
}
