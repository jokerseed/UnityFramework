using UnityEngine;

namespace Framework.Core
{
    /// <summary>
    /// 常驻 MonoBehaviour 单例：懒加载 + DontDestroyOnLoad + 重复实例保护。
    /// </summary>
    public abstract class PersistentSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        static T _instance;
        static readonly object Lock = new object();
        static bool _applicationQuitting;

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
                    DontDestroyOnLoad(go);
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
            DontDestroyOnLoad(gameObject);
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
