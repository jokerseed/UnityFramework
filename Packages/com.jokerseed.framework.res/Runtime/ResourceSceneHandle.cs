using System;
using System.Collections;
using UnityEngine.SceneManagement;
using YooAsset;

namespace Framework.Res
{
    /// <summary>
    /// YooAsset <c>SceneHandle</c> 的只读包装，隐藏 YooAsset API，
    /// 提供场景加载状态查询与卸载入口。
    /// </summary>
    public readonly struct ResourceSceneHandle : IDisposable
    {
        /// <summary>实际为 <see cref="SceneHandle"/>；以 object 存放，使持有本结构的程序集（含 async 状态机）无需引用 YooAsset。</summary>
        readonly object _raw;

        /// <summary>使用 YooAsset 原生场景句柄构造包装。</summary>
        /// <param name="handle">YooAsset 原生场景句柄；可为 null（<see cref="IsValid"/> 将返回 false）。</param>
        internal ResourceSceneHandle(SceneHandle handle)
        {
            _raw = handle;
        }

        SceneHandle Handle => (SceneHandle)_raw;

        /// <summary>句柄是否有效（非 null 且未被释放）。</summary>
        public bool IsValid => Handle != null && Handle.IsValid;

        /// <summary>加载是否已完成（包括失败情况）。</summary>
        public bool IsDone => Handle != null && Handle.IsDone;

        /// <summary>当前加载状态。</summary>
        public ResourceLoadStatus Status => MapStatus(Handle?.Status);

        /// <summary>加载是否成功。</summary>
        public bool Succeeded => Status == ResourceLoadStatus.Succeeded;

        /// <summary>失败时的错误信息；未失败或句柄无效则为空字符串。</summary>
        public string Error => Handle != null ? Handle.Error : string.Empty;

        /// <summary>加载进度（0~1）。</summary>
        public float Progress => Handle != null ? Handle.Progress : 0f;

        /// <summary>场景名称；句柄无效时为空字符串。</summary>
        public string SceneName => Handle != null ? Handle.SceneName : string.Empty;

        /// <summary>已加载的 <see cref="Scene"/>；未完成或失败时可能无效。</summary>
        public Scene Scene => Handle != null ? Handle.SceneObject : default;

        /// <summary>异步卸载本句柄对应的场景。</summary>
        /// <returns>可 yield 的卸载操作；句柄无效时立即结束。</returns>
        public IEnumerator UnloadAsync()
        {
            if (Handle == null || !Handle.IsValid)
            {
                yield break;
            }

            var operation = Handle.UnloadSceneAsync();
            yield return operation;
        }

        /// <summary>释放底层 YooAsset 句柄引用计数（不会主动卸载场景，请优先使用 <see cref="UnloadAsync"/>）。</summary>
        public void Dispose()
        {
            Handle?.Release();
        }

        static ResourceLoadStatus MapStatus(EOperationStatus? status)
        {
            if (status == null)
            {
                return ResourceLoadStatus.None;
            }

            switch (status.Value)
            {
                case EOperationStatus.Processing:
                    return ResourceLoadStatus.Processing;
                case EOperationStatus.Succeeded:
                    return ResourceLoadStatus.Succeeded;
                case EOperationStatus.Failed:
                    return ResourceLoadStatus.Failed;
                default:
                    return ResourceLoadStatus.None;
            }
        }
    }
}
