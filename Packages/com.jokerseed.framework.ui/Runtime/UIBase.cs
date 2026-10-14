using System;
using System.Collections.Generic;
using Framework.Events;
using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// UI 基类：纯 C# 驱动，不依赖 MonoBehaviour 生命周期。
    /// 子类在 <see cref="ScriptGenerator"/> 中绑定控件，在 <see cref="OnRefresh"/> 中刷新显示。
    /// </summary>
    public abstract class UIBase
    {
        readonly List<IDisposable> _eventSubscriptions = new List<IDisposable>(4);

        /// <summary>根节点 GameObject。</summary>
        public GameObject GameObject { get; private set; }

        /// <summary>根节点 Transform。</summary>
        public Transform Transform { get; private set; }

        /// <summary>根节点 RectTransform；非 UI 节点时为 null。</summary>
        public RectTransform RectTransform { get; private set; }

        /// <summary>所属 UI 管理器。</summary>
        public UIManager Manager { get; private set; }

        /// <summary>父级 UI；窗口根节点为 null。</summary>
        public UIBase Parent { get; private set; }

        /// <summary>打开时传入的用户数据。</summary>
        public object UserData { get; internal set; }

        /// <summary>是否已完成创建流程。</summary>
        public bool IsCreated { get; private set; }

        /// <summary>所属顶层窗口；当前对象为窗口时返回自身。</summary>
        public UIWindow OwnerWindow
        {
            get
            {
                var current = this;
                while (current != null)
                {
                    if (current is UIWindow window)
                    {
                        return window;
                    }

                    current = current.Parent;
                }

                return null;
            }
        }

        internal void InternalCreate(UIManager manager, GameObject gameObject, UIBase parent, object userData)
        {
            Manager = manager;
            GameObject = gameObject;
            Transform = gameObject != null ? gameObject.transform : null;
            RectTransform = gameObject != null ? gameObject.GetComponent<RectTransform>() : null;
            Parent = parent;
            UserData = userData;
            IsCreated = true;

            OnCreate();
            ScriptGenerator();
            BindMemberProperty();
            RegisterEvent();
            OnRefresh();
        }

        internal void InternalDestroy()
        {
            if (!IsCreated)
            {
                return;
            }

            ClearUIEvents();
            OnDestroy();
            IsCreated = false;
            GameObject = null;
            Transform = null;
            RectTransform = null;
            Parent = null;
            UserData = null;
            Manager = null;
        }

        /// <summary>UI 创建后首次调用，可读取 <see cref="UserData"/>。</summary>
        protected virtual void OnCreate()
        {
        }

        /// <summary>绑定控件引用，通常由代码生成工具填充。</summary>
        public virtual void ScriptGenerator()
        {
        }

        /// <summary>创建子 <see cref="UIWidget"/> 等成员属性。</summary>
        public virtual void BindMemberProperty()
        {
        }

        /// <summary>注册事件监听，推荐使用 <see cref="AddUIEvent{TEvent}"/>。</summary>
        public virtual void RegisterEvent()
        {
        }

        /// <summary>刷新界面显示。</summary>
        public virtual void OnRefresh()
        {
        }

        /// <summary>每帧更新；默认无操作。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public virtual void OnUpdate(float deltaTime)
        {
        }

        /// <summary>销毁前调用。</summary>
        protected virtual void OnDestroy()
        {
        }

        /// <summary>按相对路径查找子节点。</summary>
        /// <param name="path">相对当前根节点的层级路径，使用 <c>/</c> 分隔。</param>
        /// <returns>找到的 Transform；未找到则返回 null。</returns>
        protected Transform FindChild(string path)
        {
            if (Transform == null || string.IsNullOrEmpty(path))
            {
                return null;
            }

            return Transform.Find(path);
        }

        /// <summary>按相对路径查找子节点上的组件。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <param name="path">相对当前根节点的层级路径。</param>
        /// <returns>找到的组件；节点或组件不存在时返回 null。</returns>
        protected T FindChildComponent<T>(string path) where T : Component
        {
            var child = FindChild(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        /// <summary>
        /// 订阅全局事件；UI 销毁时自动取消订阅。
        /// </summary>
        /// <typeparam name="TEvent">事件类型，须为 struct。</typeparam>
        /// <param name="handler">事件处理委托，不可为 null。</param>
        protected void AddUIEvent<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _eventSubscriptions.Add(GameEvent.Subscribe(handler));
        }

        /// <summary>创建并绑定子 Widget。</summary>
        /// <typeparam name="TWidget">Widget 类型，须有无参构造。</typeparam>
        /// <param name="target">Widget 根节点 GameObject，不可为 null。</param>
        /// <returns>已创建并初始化的 Widget。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> 为 null。</exception>
        protected TWidget CreateWidget<TWidget>(GameObject target) where TWidget : UIWidget, new()
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var widget = new TWidget();
            widget.InternalCreate(Manager, target, this, null);
            return widget;
        }

        /// <summary>按路径创建并绑定子 Widget。</summary>
        /// <typeparam name="TWidget">Widget 类型，须有无参构造。</typeparam>
        /// <param name="path">相对当前根节点的层级路径。</param>
        /// <returns>已创建并初始化的 Widget；路径无效时返回 null。</returns>
        protected TWidget CreateWidget<TWidget>(string path) where TWidget : UIWidget, new()
        {
            var child = FindChild(path);
            return child != null ? CreateWidget<TWidget>(child.gameObject) : null;
        }

        void ClearUIEvents()
        {
            for (var i = 0; i < _eventSubscriptions.Count; i++)
            {
                _eventSubscriptions[i]?.Dispose();
            }

            _eventSubscriptions.Clear();
        }
    }
}
