using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// 把所在 RectTransform 的锚点约束到 <see cref="Screen.safeArea"/>（刘海、圆角、手势条）。
    /// 挂在窗口预制体内的内容节点上，背景仍放在窗口根节点以铺满全屏；屏幕尺寸或安全区变化时自动重算。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class UISafeArea : MonoBehaviour
    {
        [SerializeField] bool _left = true;
        [SerializeField] bool _right = true;
        [SerializeField] bool _top = true;
        [SerializeField] bool _bottom = true;

        RectTransform _rect;
        Rect _appliedArea;
        Vector2Int _appliedScreen;

        void Awake()
        {
            _rect = (RectTransform)transform;
        }

        void OnEnable()
        {
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != _appliedArea || Screen.width != _appliedScreen.x || Screen.height != _appliedScreen.y)
            {
                Apply();
            }
        }

        /// <summary>立即按当前安全区重算锚点。</summary>
        public void Apply()
        {
            var area = Screen.safeArea;
            var width = Screen.width;
            var height = Screen.height;
            _appliedArea = area;
            _appliedScreen = new Vector2Int(width, height);
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var min = new Vector2(_left ? area.xMin / width : 0f, _bottom ? area.yMin / height : 0f);
            var max = new Vector2(_right ? area.xMax / width : 1f, _top ? area.yMax / height : 1f);
            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
