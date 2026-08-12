using System;

namespace Framework.Coroutine
{
    /// <summary>协程句柄：查询运行状态并停止。</summary>
    public interface ICoroutineHandle : IDisposable
    {
        /// <summary>协程是否仍在运行。</summary>
        bool IsRunning { get; }

        /// <summary>停止该协程；已结束时无操作。</summary>
        void Stop();
    }
}
