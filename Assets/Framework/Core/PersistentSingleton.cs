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

        public static bool HasInstance => _instance != null;

        public static T Instance
        {
            get
            {
                if (_applicationQuitting)
                {
                    return null;
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

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }
    }
}
