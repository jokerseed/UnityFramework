using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Core
{
    /// <summary>
    /// 关闭 Domain Reload 时复位各 <see cref="PersistentSingleton{T}"/> 的静态退出标志。
    /// 泛型类型上的 <c>RuntimeInitializeOnLoadMethod</c> 不会被 Unity 收集，所以登记在这个非泛型类型上。
    /// </summary>
    static class PersistentSingletonReset
    {
        static readonly List<Action> Callbacks = new List<Action>();

        /// <summary>进入 Play Mode 前清掉退出标志，避免第二次 Play 无法重建单例。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            for (var i = 0; i < Callbacks.Count; i++)
            {
                Callbacks[i]();
            }
        }

        /// <summary>登记某个单例类型的静态复位。</summary>
        /// <param name="callback">复位回调；为 null 时忽略。</param>
        public static void Register(Action callback)
        {
            if (callback != null)
            {
                Callbacks.Add(callback);
            }
        }
    }

    /// <summary>
    /// 常驻 MonoBehaviour 单例：懒加载 + DontDestroyOnLoad + 重复实例保护。
    /// </summary>
    public abstract class PersistentSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        static T _instance;
        static readonly object Lock = new object();
        static bool _applicationQuitting;

        static PersistentSingleton()
        {
            PersistentSingletonReset.Register(ResetStaticState);
        }

        /// <summary>
        /// 清除退出标志。若静态引用已经指向被销毁的对象，一并置空。
        /// 关闭 Domain Reload 时由 SubsystemRegistration 调用；测试也可直接调用。
        /// </summary>
        public static void ResetStaticState()
        {
            _applicationQuitting = false;
            if (_instance == null)
            {
                _instance = null;
            }
        }

        /// <summary>获取单例是否已被创建（不触发懒加载）。</summary>
        public static bool HasInstance => _instance != null;

        /// <summary>
        /// 获取单例实例；若尚未创建则自动创建并标记为 DontDestroyOnLoad。
        /// 应用退出阶段返回现有实例（可能为 null），不再新建。
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_applicationQuitting)
                {
                    return _instance;
                }

                if (_instance != null)
                {
                    return _instance;
                }

                lock (Lock)
                {
                    if (_instance != null)
                    {
                        return _instance;
                    }

                    var go = new GameObject(typeof(T).Name);
                    _instance = go.AddComponent<T>();
                    if (Application.isPlaying)
                    {
                        DontDestroyOnLoad(go);
                    }
                }

                return _instance;
            }
        }

        /// <summary>销毁单例的 GameObject，并将内部引用置 null。</summary>
        public static void DestroyInstance()
        {
            if (_instance == null)
            {
                return;
            }

            var target = _instance.gameObject;
            _instance = null;
            if (target != null)
            {
                Destroy(target);
            }
        }

        /// <summary>Unity 生命周期：检测重复实例并注册 DontDestroyOnLoad。</summary>
        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>Unity 生命周期：实例被销毁时清除静态引用。</summary>
        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>Unity 生命周期：应用退出时设置标志，防止退出阶段重建单例。</summary>
        protected virtual void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }
    }
}
