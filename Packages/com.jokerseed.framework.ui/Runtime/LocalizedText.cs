using Framework.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Framework.UI
{
    /// <summary>
    /// 把 uGUI <see cref="Text"/> 绑定到本地化键：启用时刷新，并在切换语言时自动刷新。
    /// 运行中改键调用 <see cref="SetKey"/>。
    /// </summary>
    [RequireComponent(typeof(Text))]
    [DisallowMultipleComponent]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] string _key;

        Text _text;
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
            _text = GetComponent<Text>();
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
