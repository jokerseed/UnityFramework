using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;
using UnityEngine;

namespace Framework.Localization
{
    /// <summary>
    /// 本地化模块：注册文本来源并设置初始语言（未指定时取系统语言）。
    /// </summary>
    public sealed class LocalizationModule : IGameModule
    {
        readonly string _language;
        readonly string _fallbackLanguage;
        readonly ILocalizationSource[] _sources;

        /// <summary>创建本地化模块。</summary>
        /// <param name="language">初始语言代码；为 null 时由 <see cref="Application.systemLanguage"/> 推断。</param>
        /// <param name="fallbackLanguage">回退语言代码；为 null 表示不回退。</param>
        /// <param name="sources">文本来源，后面的优先。</param>
        public LocalizationModule(string language = null, string fallbackLanguage = "en", params ILocalizationSource[] sources)
        {
            _language = language;
            _fallbackLanguage = fallbackLanguage;
            _sources = sources ?? Array.Empty<ILocalizationSource>();
        }

        /// <inheritdoc />
        public string Name => "Localization";

        /// <inheritdoc />
        public ModulePhase Phase => ModulePhase.Data;

        /// <inheritdoc />
        public IReadOnlyList<Type> Dependencies => new[] { typeof(LoggingModule) };

        /// <inheritdoc />
        public void Initialize()
        {
            var manager = LocalizationManager.Instance;
            manager.FallbackLanguage = _fallbackLanguage;
            for (var i = 0; i < _sources.Length; i++)
            {
                manager.AddSource(_sources[i]);
            }

            manager.SetLanguage(_language ?? LocalizationManager.FromSystemLanguage(Application.systemLanguage));
            GameLog.Info(LogCategories.Localization, $"Ready  Language={LogStyle.Value(manager.Language)} Sources={LogStyle.Value(_sources.Length)}");
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            if (LocalizationManager.HasInstance)
            {
                LocalizationManager.Instance.Shutdown();
                LocalizationManager.DestroyInstance();
            }

            GameLog.Info(LogCategories.Localization, LogStyle.Muted("shut down"));
        }
    }
}
