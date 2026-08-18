using Framework.Res;
using UnityEngine;
using UnityEngine.UI;

namespace Framework.UI
{
    /// <summary>
    /// 窗口级 UI：由 <see cref="UIManager"/> 管理打开/关闭、层级与显示遮挡。
    /// </summary>
    public abstract class UIWindow : UIBase
    {
        Canvas _canvas;
        GraphicRaycaster _raycaster;

        /// <summary>窗口所在层级。</summary>
        public UILayer Layer { get; internal set; }

        /// <summary>是否为全屏窗口。</summary>
        public bool FullScreen { get; internal set; }

        /// <summary>窗口 Canvas 排序深度。</summary>
        public int SortingOrder { get; internal set; }

        /// <summary>预制体资源句柄；窗口关闭前保持有效以维持资源引用。</summary>
        internal ResourceAssetHandle AssetHandle { get; set; }

        /// <summary>关闭释放策略（由 <see cref="UIManager"/> 在打开时写入）。</summary>
        internal UIReleasePolicy ReleasePolicy { get; set; }

        /// <summary>延迟卸载秒数（仅 <see cref="UIReleasePolicy.HideAndDelayUnload"/>）。</summary>
        internal float DelayUnloadSeconds { get; set; }

        /// <summary>设置窗口可见性（不销毁实例）。</summary>
        /// <param name="visible">为 true 时显示并启用射线检测。</param>
        public void SetVisible(bool visible)
        {
            if (GameObject != null)
            {
                GameObject.SetActive(visible);
            }

            if (_raycaster != null)
            {
                _raycaster.enabled = visible;
            }
        }

        internal void SetupWindow(Transform layerRoot, UILayer layer, bool fullScreen, int sortingOrder)
        {
            Layer = layer;
            FullScreen = fullScreen;
            SortingOrder = sortingOrder;

            if (Transform != null)
            {
                Transform.SetParent(layerRoot, false);
                StretchToParent(RectTransform);
            }

            EnsureCanvas(sortingOrder);
        }

        static void StretchToParent(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        void EnsureCanvas(int sortingOrder)
        {
            if (GameObject == null)
            {
                return;
            }

            _canvas = GameObject.GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = GameObject.AddComponent<Canvas>();
            }

            _canvas.overrideSorting = true;
            _canvas.sortingOrder = sortingOrder;

            _raycaster = GameObject.GetComponent<GraphicRaycaster>();
            if (_raycaster == null)
            {
                _raycaster = GameObject.AddComponent<GraphicRaycaster>();
            }
        }
    }
}
