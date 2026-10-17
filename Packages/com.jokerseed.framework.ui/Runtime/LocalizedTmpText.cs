#if FRAMEWORK_TMP
using Framework.Localization;
using TMPro;
using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// 把 TextMesh Pro 文本（<see cref="TMP_Text"/>，含 TextMeshProUGUI 与 3D TextMeshPro）绑定到本地化键：
    /// 启用时刷新，并在切换语言时自动刷新。运行中改键调用 <see cref="SetKey"/>。
    /// 仅在工程安装 <c>com.unity.textmeshpro</c> 时编译（asmdef 的 versionDefines 定义 <c>FRAMEWORK_TMP</c>）。
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public sealed class LocalizedTmpText : MonoBehaviour
    {
        [SerializeField] string _key;

        TMP_Text _text;
        LocalizationManager _subscribed;

        /// <summary>当前本地化键。</summary>
        public string Key => _key;

        /// <summary>更换本地化键并立即刷新。</summary>
        /// <param name="key">本地化键。</param>
        public void SetKey(string key)
        {
            _key = key;
            Refresh();
        }

        void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            _subscribed = LocalizationManager.Instance;
            _subscribed.LanguageChanged += OnLanguageChanged;
            Refresh();
        }

        void OnDisable()
        {
            if (_subscribed != null)
            {
                _subscribed.LanguageChanged -= OnLanguageChanged;
                _subscribed = null;
            }
        }

        void OnLanguageChanged(string language) => Refresh();

        void Refresh()
        {
            if (_subscribed != null && !string.IsNullOrEmpty(_key))
            {
                _text.text = _subscribed.Get(_key);
            }
        }
    }
}
#endif
