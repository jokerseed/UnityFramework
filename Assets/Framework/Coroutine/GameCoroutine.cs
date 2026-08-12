using System.Collections;
using UnityEngine;

namespace Framework.Coroutine
{
    /// <summary>
    /// 协程静态入口（对齐 <c>GameEvent</c> 风格）。
    /// 须先完成 <see cref="CoroutineModule"/> 初始化（或直接访问 <see cref="CoroutineManager.Instance"/>）。
    /// </summary>
    public static class GameCoroutine
    {
        /// <summary>在全局作用域启动协程（跨场景）。</summary>
        /// <param name="routine">协程迭代器，不可为 null。</param>
        /// <returns>协程句柄。</returns>
        public static ICoroutineHandle StartGlobal(IEnumerator routine)
        {
            return CoroutineManager.Instance.StartGlobal(routine);
        }

        /// <summary>在当前场景作用域启动协程（切场景自动停）。</summary>
        /// <param name="routine">协程迭代器，不可为 null。</param>
        /// <returns>协程句柄。</returns>
        public static ICoroutineHandle StartScene(IEnumerator routine)
        {
            return CoroutineManager.Instance.StartScene(routine);
        }

        /// <summary>在指定 GameObject 上启动协程（销毁时自动停）。</summary>
        /// <param name="host">宿主对象。</param>
        /// <param name="routine">协程迭代器。</param>
        /// <returns>协程句柄。</returns>
        public static ICoroutineHandle Start(GameObject host, IEnumerator routine)
        {
            return CoroutineManager.Instance.StartOnGameObject(host, routine);
        }

        /// <summary>在指定 MonoBehaviour 所在对象上启动协程。</summary>
        /// <param name="host">宿主组件。</param>
        /// <param name="routine">协程迭代器。</param>
        /// <returns>协程句柄。</returns>
        public static ICoroutineHandle Start(MonoBehaviour host, IEnumerator routine)
        {
            return CoroutineManager.Instance.StartOnBehaviour(host, routine);
        }

        /// <summary>按作用域启动协程（仅 <see cref="CoroutineScope.Global"/> / <see cref="CoroutineScope.Scene"/>）。</summary>
        /// <param name="scope">生命周期作用域。</param>
        /// <param name="routine">协程迭代器。</param>
        /// <returns>协程句柄。</returns>
        public static ICoroutineHandle Start(CoroutineScope scope, IEnumerator routine)
        {
            return scope == CoroutineScope.Scene
                ? StartScene(routine)
                : StartGlobal(routine);
        }

        /// <summary>停止句柄对应协程。</summary>
        /// <param name="handle">协程句柄；可为 null。</param>
        public static void Stop(ICoroutineHandle handle)
        {
            CoroutineManager.Instance.Stop(handle);
        }

        /// <summary>停止全部全局协程。</summary>
        public static void StopAllGlobal()
        {
            CoroutineManager.Instance.StopAllGlobal();
        }

        /// <summary>停止全部场景协程。</summary>
        public static void StopAllScene()
        {
            CoroutineManager.Instance.StopAllScene();
        }

        /// <summary>停止指定 GameObject 上由本模块启动的全部协程。</summary>
        /// <param name="host">宿主对象。</param>
        public static void StopAll(GameObject host)
        {
            CoroutineManager.Instance.StopAllOnGameObject(host);
        }

        /// <summary>查询句柄是否仍在运行。</summary>
        /// <param name="handle">协程句柄；可为 null。</param>
        /// <returns>正在运行返回 true。</returns>
        public static bool IsRunning(ICoroutineHandle handle)
        {
            return handle != null && handle.IsRunning;
        }
    }
}
