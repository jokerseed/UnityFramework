using System.Collections;
using Framework.Core;
using Framework.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Framework.Coroutine
{
    /// <summary>
    /// 协程管理器：按 Global / Scene / GameObject 生命周期启动与停止 Unity 协程。
    /// </summary>
    public sealed class CoroutineManager : PersistentSingleton<CoroutineManager>
    {
        GlobalCoroutineRunner _globalRunner;
        SceneCoroutineRunner _sceneRunner;
        bool _listeningSceneUnload;

        /// <summary>确保全局 Runner 已创建。</summary>
        void EnsureGlobalRunner()
        {
            if (_globalRunner != null)
            {
                return;
            }

            _globalRunner = gameObject.GetComponent<GlobalCoroutineRunner>();
            if (_globalRunner == null)
            {
                _globalRunner = gameObject.AddComponent<GlobalCoroutineRunner>();
            }
        }

        /// <summary>确保当前场景 Runner 已创建。</summary>
        void EnsureSceneRunner()
        {
            EnsureSceneUnloadHook();

            if (_sceneRunner != null)
            {
                return;
            }

            var go = new GameObject("[Framework.SceneCoroutineRunner]");
            _sceneRunner = go.AddComponent<SceneCoroutineRunner>();
        }

        void EnsureSceneUnloadHook()
        {
            if (_listeningSceneUnload)
            {
                return;
            }

            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _listeningSceneUnload = true;
        }

        void OnSceneUnloaded(Scene scene)
        {
            if (_sceneRunner == null)
            {
                return;
            }

            // 场景卸载会销毁场景内对象；清空引用即可
            if (_sceneRunner != null)
            {
                _sceneRunner.HaltAll();
            }

            _sceneRunner = null;
            GameLog.Debug(LogCategories.Coroutine, $"Scene coroutines cleared on unload: {LogStyle.Name(scene.name)}");
        }

        /// <summary>在全局作用域启动协程。</summary>
        /// <param name="routine">协程迭代器，不可为 null。</param>
        /// <returns>协程句柄。</returns>
        public ICoroutineHandle StartGlobal(IEnumerator routine)
        {
            return StartOnRunner(GetGlobalRunner(), routine);
        }

        /// <summary>在当前场景作用域启动协程。</summary>
        /// <param name="routine">协程迭代器，不可为 null。</param>
        /// <returns>协程句柄。</returns>
        public ICoroutineHandle StartScene(IEnumerator routine)
        {
            return StartOnRunner(GetSceneRunner(), routine);
        }

        /// <summary>在指定 GameObject 上启动协程（GO 销毁时自动结束）。</summary>
        /// <param name="host">宿主对象，不可为 null 或已销毁。</param>
        /// <param name="routine">协程迭代器，不可为 null。</param>
        /// <returns>协程句柄；宿主无效时返回无效句柄。</returns>
        public ICoroutineHandle StartOnGameObject(GameObject host, IEnumerator routine)
        {
            if (host == null)
            {
                GameLog.Warning(LogCategories.Coroutine, $"StartOnGameObject {LogStyle.Fail("failed")}: host is null or destroyed.");
                return CoroutineHandle.Invalid;
            }

            var behaviour = host.GetComponent<CoroutineBehaviour>();
            if (behaviour == null)
            {
                behaviour = host.AddComponent<CoroutineBehaviour>();
            }

            return StartOnRunner(behaviour, routine);
        }

        /// <summary>在指定 MonoBehaviour 所在对象上启动协程。</summary>
        /// <param name="host">宿主组件，不可为 null 或已销毁。</param>
        /// <param name="routine">协程迭代器，不可为 null。</param>
        /// <returns>协程句柄；宿主无效时返回无效句柄。</returns>
        public ICoroutineHandle StartOnBehaviour(MonoBehaviour host, IEnumerator routine)
        {
            if (host == null)
            {
                GameLog.Warning(LogCategories.Coroutine, $"StartOnBehaviour {LogStyle.Fail("failed")}: host is null or destroyed.");
                return CoroutineHandle.Invalid;
            }

            return StartOnGameObject(host.gameObject, routine);
        }

        /// <summary>停止句柄对应协程。</summary>
        /// <param name="handle">协程句柄；可为 null。</param>
        public void Stop(ICoroutineHandle handle)
        {
            handle?.Stop();
        }

        /// <summary>停止全部全局协程。</summary>
        public void StopAllGlobal()
        {
            if (_globalRunner != null)
            {
                _globalRunner.HaltAll();
            }
        }

        /// <summary>停止全部场景协程。</summary>
        public void StopAllScene()
        {
            if (_sceneRunner != null)
            {
                _sceneRunner.HaltAll();
            }
        }

        /// <summary>停止指定 GameObject 上由本模块启动的全部协程。</summary>
        /// <param name="host">宿主对象。</param>
        public void StopAllOnGameObject(GameObject host)
        {
            if (host == null)
            {
                return;
            }

            var behaviour = host.GetComponent<CoroutineBehaviour>();
            behaviour?.HaltAll();
        }

        /// <summary>停止全局与场景宿主上的全部协程（不含各业务 GO 上的 Behaviour）。</summary>
        public void StopAllManaged()
        {
            StopAllGlobal();
            StopAllScene();
        }

        /// <summary>模块关闭：停掉托管协程并取消场景监听。</summary>
        public void Shutdown()
        {
            StopAllManaged();
            if (_listeningSceneUnload)
            {
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
                _listeningSceneUnload = false;
            }

            if (_sceneRunner != null)
            {
                Destroy(_sceneRunner.gameObject);
                _sceneRunner = null;
            }
        }

        MonoBehaviour GetGlobalRunner()
        {
            EnsureGlobalRunner();
            return _globalRunner;
        }

        MonoBehaviour GetSceneRunner()
        {
            EnsureSceneRunner();
            return _sceneRunner;
        }

        static ICoroutineHandle StartOnRunner(MonoBehaviour runner, IEnumerator routine)
        {
            if (routine == null)
            {
                GameLog.Warning(LogCategories.Coroutine, $"Start {LogStyle.Fail("failed")}: routine is null.");
                return CoroutineHandle.Invalid;
            }

            if (runner == null)
            {
                GameLog.Warning(LogCategories.Coroutine, $"Start {LogStyle.Fail("failed")}: runner is null.");
                return CoroutineHandle.Invalid;
            }

            var handle = CoroutineHandle.CreatePending();
            var unityCo = runner.StartCoroutine(Wrap(routine, handle));
            handle.Bind(runner, unityCo);
            return handle;
        }

        static IEnumerator Wrap(IEnumerator routine, CoroutineHandle handle)
        {
            while (routine.MoveNext())
            {
                yield return routine.Current;
            }

            handle.MarkCompleted();
        }

        protected override void OnDestroy()
        {
            if (_listeningSceneUnload)
            {
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
                _listeningSceneUnload = false;
            }

            base.OnDestroy();
        }
    }
}
