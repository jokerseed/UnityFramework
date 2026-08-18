using System;
using UnityEngine;

namespace Framework.Res
{
    /// <summary>
    /// 资源分帧调度预算。时间（毫秒）是主控，个数上限是安全阀。
    /// </summary>
    [Serializable]
    public sealed class ResourceSchedulerOptions
    {
        /// <summary>本帧调度总时间上限（毫秒）。</summary>
        [Min(0.1f)]
        public float MaxFrameBudgetMs = 4f;

        /// <summary>每帧最多新发起多少次异步加载。</summary>
        [Min(1)]
        public int MaxLoadStartsPerFrame = 2;

        /// <summary>同时进行中的异步加载上限（InFlight）。</summary>
        [Min(1)]
        public int MaxLoadInFlight = 8;

        /// <summary>每帧最多派发多少个加载完成回调。</summary>
        [Min(1)]
        public int MaxCallbacksPerFrame = 5;

        /// <summary>加载完成回调的时间预算（毫秒）。</summary>
        [Min(0.1f)]
        public float CallbackBudgetMs = 1f;

        /// <summary>每帧最多执行多少次 Instantiate。</summary>
        [Min(1)]
        public int MaxInstantiatesPerFrame = 3;

        /// <summary>Instantiate 专用时间预算（毫秒）。</summary>
        [Min(0.1f)]
        public float InstantiateBudgetMs = 3f;

        /// <summary>每帧最多启动多少次 UnloadUnusedAssets（通常为 0 或 1）。</summary>
        [Min(0)]
        public int MaxUnloadPerFrame = 1;

        /// <summary>
        /// 为 true 时仅在 Load / Instantiate 队列为空后才执行 Unload。
        /// </summary>
        public bool UnloadOnlyWhenIdle = true;
    }
}
