using System.Collections;
using UnityEngine;

namespace Framework.Res{
    /// <summary>
    /// 调度队列中的一次资源请求。可用于等待完成、取消尚未开始的工作，或 <c>yield return request</c> 协程等待。
    /// </summary>
    public sealed class ResourceRequestHandle : IEnumerator
    {
        /// <summary>请求序号。</summary>
        public int Id { get; internal set; }

        /// <summary>当前状态。</summary>
        public ResourceRequestStatus Status { get; internal set; }

        /// <summary>是否已结束（成功、失败或取消）。</summary>
        public bool IsDone =>
            Status == ResourceRequestStatus.Succeeded
            || Status == ResourceRequestStatus.Failed
            || Status == ResourceRequestStatus.Cancelled;

        /// <summary>加载成功时的资源句柄；失败或取消时无效。</summary>
        public ResourceAssetHandle AssetHandle { get; internal set; }

        /// <summary>实例化成功时的场景对象；失败或取消时为 null。</summary>
        public GameObject Instance { get; internal set; }

        /// <summary>失败信息；未失败时为空字符串。</summary>
        public string Error { get; internal set; } = string.Empty;

        /// <summary>所属调度器；取消时回调。</summary>
        internal ResourceScheduler Scheduler { get; set; }

        /// <summary>
        /// 取消尚未完成的请求。已在执行中的 Instantiate 无法中断；
        /// 尚未 Start 的 Load 不会发起；InFlight 或已完成尚未回调的 Load 会释放底层句柄。
        /// 同地址合并加载时，取消本请求不影响其他等待者；全部取消后才释放底层 YooAsset 句柄。
        /// </summary>
        public void Cancel()
        {
            Scheduler?.Cancel(this);
        }

        /// <inheritdoc />
        object IEnumerator.Current => null;

        /// <inheritdoc />
        bool IEnumerator.MoveNext() => !IsDone;

        /// <inheritdoc />
        void IEnumerator.Reset()
        {
        }
    }
}
