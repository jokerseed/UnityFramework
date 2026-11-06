using System;
using System.Collections.Generic;

namespace Framework.UI
{
    /// <summary>不依赖 Unity 的窗口身份、缓存、层内顺序与遮挡规则；不调用窗口生命周期。</summary>
    internal sealed class WindowRegistry<TWindow> where TWindow : class
    {
        internal const int LayerOrderStep = 50;
        internal const int WindowOrderStep = 10;
        internal const int LayerCapacity = 500;

        readonly List<TWindow> _stack = new List<TWindow>(16);
        readonly Dictionary<Type, TWindow> _opened = new Dictionary<Type, TWindow>(16);
        readonly Dictionary<Type, TWindow> _cached = new Dictionary<Type, TWindow>(8);
        readonly Func<TWindow, UILayer> _layer;
        readonly Func<TWindow, bool> _fullScreen;

        internal WindowRegistry(Func<TWindow, UILayer> layer, Func<TWindow, bool> fullScreen)
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _fullScreen = fullScreen ?? throw new ArgumentNullException(nameof(fullScreen));
            Stack = _stack.AsReadOnly();
        }

        internal IReadOnlyList<TWindow> Stack { get; }
        internal bool TryGetOpened(Type type, out TWindow window) => _opened.TryGetValue(type, out window);
        internal bool TryGetCached(Type type, out TWindow window) => _cached.TryGetValue(type, out window);
        internal bool IsVisible(TWindow window) => _stack.Contains(window);

        internal void Register(TWindow window)
        {
            BringToTop(window);
            _cached.Remove(window.GetType());
            _opened[window.GetType()] = window;
        }

        internal void BringToTop(TWindow window)
        {
            EnsureLayerCapacity(window);
            _stack.Remove(window);
            var index = _stack.Count;
            while (index > 0 && (int)_layer(_stack[index - 1]) > (int)_layer(window)) index--;
            _stack.Insert(index, window);
        }

        internal void EnsureLayerCapacity(TWindow window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            var layer = _layer(window);
            if (!Enum.IsDefined(typeof(UILayer), layer))
                throw new InvalidOperationException($"[UI] Unsupported layer: {layer}.");
            var count = 0;
            foreach (var existing in _stack)
                if (!ReferenceEquals(existing, window) && _layer(existing) == layer) count++;
            if (count >= LayerCapacity)
                throw new InvalidOperationException("[UI] A layer supports at most 500 open windows.");
        }

        // HideOnly 保留 opened 身份，但不参与可见栈、遮挡或返回键。
        internal void Hide(TWindow window) => _stack.Remove(window);

        internal void Cache(TWindow window)
        {
            Remove(window);
            _cached[window.GetType()] = window;
        }

        internal void Remove(TWindow window)
        {
            var type = window.GetType();
            if (_opened.TryGetValue(type, out var opened) && ReferenceEquals(opened, window)) _opened.Remove(type);
            if (_cached.TryGetValue(type, out var cached) && ReferenceEquals(cached, window)) _cached.Remove(type);
            _stack.Remove(window);
        }

        internal TWindow[] Drain()
        {
            var windows = new HashSet<TWindow>(_opened.Values);
            windows.UnionWith(_cached.Values);
            var result = new TWindow[windows.Count];
            windows.CopyTo(result);
            _stack.Clear();
            _opened.Clear();
            _cached.Clear();
            return result;
        }

        /// <summary>生成稳定布局快照，Unity 激活回调改变注册表时不会破坏正在遍历的集合。</summary>
        internal WindowLayout<TWindow>[] SnapshotLayout()
        {
            var layout = new WindowLayout<TWindow>[_stack.Count];
            var counters = new Dictionary<UILayer, int>();
            for (var i = 0; i < _stack.Count; i++)
            {
                var window = _stack[i];
                var layer = _layer(window);
                counters.TryGetValue(layer, out var count);
                layout[i] = new WindowLayout<TWindow>(window, (int)layer * LayerOrderStep + count * WindowOrderStep, false);
                counters[layer] = count + 1;
            }

            var blocked = false;
            for (var i = layout.Length - 1; i >= 0; i--)
            {
                layout[i] = new WindowLayout<TWindow>(layout[i].Window, layout[i].SortingOrder, blocked);
                blocked |= _fullScreen(layout[i].Window);
            }
            return layout;
        }
    }

    internal readonly struct WindowLayout<TWindow>
    {
        internal WindowLayout(TWindow window, int sortingOrder, bool occluded)
        {
            Window = window;
            SortingOrder = sortingOrder;
            Occluded = occluded;
        }
        internal TWindow Window { get; }
        internal int SortingOrder { get; }
        internal bool Occluded { get; }
    }
}
