using System.Collections.Generic;
using UnityEngine;

namespace Framework.Coroutine
{
    /// <summary>
    /// 协程宿主基类：登记在自己身上启动的句柄。
    /// Unity 在 <c>StopAllCoroutines</c>、宿主失活或销毁时直接丢弃协程，既不走迭代器的 finally，也不通知句柄；
    /// 这里在这些时机把句柄标记为已停止，保证 <see cref="ICoroutineHandle.IsRunning"/> 与协程实际状态一致。
    /// </summary>
    public abstract class CoroutineRunnerBase : MonoBehaviour
    {
        readonly List<CoroutineHandle> _handles = new List<CoroutineHandle>();

        /// <summary>停止该宿主上全部协程，并把它们的句柄标记为已停止。</summary>
        public void HaltAll()
        {
            StopAllCoroutines();
            MarkAllStopped();
        }

        internal void Track(CoroutineHandle handle)
        {
            _handles.Add(handle);
        }

        internal void Untrack(CoroutineHandle handle)
        {
            _handles.Remove(handle);
        }

        /// <summary>GameObject 失活会让 Unity 停掉其上全部协程；只禁用组件不会，此时什么都不做。</summary>
        protected virtual void OnDisable()
        {
            if (!gameObject.activeInHierarchy)
            {
                MarkAllStopped();
            }
        }

        /// <summary>宿主销毁时协程随之结束。</summary>
        protected virtual void OnDestroy()
        {
            MarkAllStopped();
        }

        void MarkAllStopped()
        {
            if (_handles.Count == 0)
            {
                return;
            }

            var handles = _handles.ToArray();
            _handles.Clear();
            for (var i = 0; i < handles.Length; i++)
            {
                handles[i].MarkStopped();
            }
        }
    }

    /// <summary>全局协程宿主（挂在 DontDestroyOnLoad 管理器上）。</summary>
    sealed class GlobalCoroutineRunner : CoroutineRunnerBase
    {
    }

    /// <summary>场景级协程宿主（位于当前场景，随场景销毁）。</summary>
    sealed class SceneCoroutineRunner : CoroutineRunnerBase
    {
    }

    /// <summary>挂在业务 GameObject 上的协程宿主，随 GO 销毁自动结束。</summary>
    public sealed class CoroutineBehaviour : CoroutineRunnerBase
    {
    }
}
