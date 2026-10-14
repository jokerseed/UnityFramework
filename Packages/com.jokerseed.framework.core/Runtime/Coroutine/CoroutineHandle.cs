using UnityEngine;

namespace Framework.Coroutine
{
    /// <summary>协程句柄实现。</summary>
    sealed class CoroutineHandle : ICoroutineHandle
    {
        MonoBehaviour _runner;
        UnityEngine.Coroutine _coroutine;
        bool _running;

        /// <summary>无效句柄（启动失败时返回）。</summary>
        public static CoroutineHandle Invalid { get; } = new CoroutineHandle();

        CoroutineHandle()
        {
            _running = false;
        }

        /// <summary>创建待绑定的运行中句柄。</summary>
        internal static CoroutineHandle CreatePending()
        {
            return new CoroutineHandle { _running = true };
        }

        /// <summary>绑定 Runner 与 Unity Coroutine。</summary>
        /// <param name="runner">宿主。</param>
        /// <param name="coroutine">Unity 协程实例。</param>
        internal void Bind(MonoBehaviour runner, UnityEngine.Coroutine coroutine)
        {
            _runner = runner;
            _coroutine = coroutine;
            if (runner == null || coroutine == null)
            {
                _running = false;
            }
        }

        /// <inheritdoc />
        public bool IsRunning => _running && _runner != null && _coroutine != null;

        /// <inheritdoc />
        public void Stop()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            if (_runner != null && _coroutine != null)
            {
                _runner.StopCoroutine(_coroutine);
            }

            _coroutine = null;
            _runner = null;
        }

        /// <inheritdoc />
        public void Dispose() => Stop();

        internal void MarkCompleted()
        {
            _running = false;
            _coroutine = null;
            _runner = null;
        }
    }
}
