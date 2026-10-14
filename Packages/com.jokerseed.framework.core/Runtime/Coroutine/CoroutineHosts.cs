using UnityEngine;

namespace Framework.Coroutine
{
    /// <summary>全局协程宿主（挂在 DontDestroyOnLoad 管理器上）。</summary>
    sealed class GlobalCoroutineRunner : MonoBehaviour
    {
        /// <summary>停止该宿主上全部协程。</summary>
        public void HaltAll() => StopAllCoroutines();
    }

    /// <summary>场景级协程宿主（位于当前场景，随场景销毁）。</summary>
    sealed class SceneCoroutineRunner : MonoBehaviour
    {
        /// <summary>停止该宿主上全部协程。</summary>
        public void HaltAll() => StopAllCoroutines();
    }

    /// <summary>挂在业务 GameObject 上的协程宿主，随 GO 销毁自动结束。</summary>
    public sealed class CoroutineBehaviour : MonoBehaviour
    {
        /// <summary>停止该宿主上全部协程。</summary>
        public void HaltAll() => StopAllCoroutines();
    }
}
