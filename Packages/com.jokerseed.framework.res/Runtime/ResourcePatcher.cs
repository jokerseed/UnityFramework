using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Logging;
using YooAsset;

namespace Framework.Res
{
    /// <summary>补丁下载进度。</summary>
    public readonly struct ResourcePatchProgress
    {
        /// <summary>创建进度。</summary>
        /// <param name="downloadedCount">已下载文件数。</param>
        /// <param name="totalCount">总文件数。</param>
        /// <param name="downloadedBytes">已下载字节数。</param>
        /// <param name="totalBytes">总字节数。</param>
        public ResourcePatchProgress(int downloadedCount, int totalCount, long downloadedBytes, long totalBytes)
        {
            DownloadedCount = downloadedCount;
            TotalCount = totalCount;
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
        }

        /// <summary>已下载文件数。</summary>
        public int DownloadedCount { get; }

        /// <summary>总文件数。</summary>
        public int TotalCount { get; }

        /// <summary>已下载字节数。</summary>
        public long DownloadedBytes { get; }

        /// <summary>总字节数。</summary>
        public long TotalBytes { get; }

        /// <summary>按字节计算的进度（0~1）；无需下载时为 1。</summary>
        public float Progress => TotalBytes > 0 ? (float)((double)DownloadedBytes / TotalBytes) : 1f;
    }

    /// <summary>
    /// 资源补丁：在 <see cref="ResourceManager"/> 初始化（已拿到最新版本 Manifest）之后，
    /// 统计需要下载的 Bundle，并下载到本地缓存。非 Host 模式下没有需要下载的文件。
    /// 典型流程：<see cref="Create"/> → 展示 <see cref="TotalBytes"/> 让玩家确认 → <see cref="DownloadAsync"/>。
    /// </summary>
    public sealed class ResourcePatcher
    {
        readonly ResourceDownloaderOperation _downloader;
        bool _started;

        ResourcePatcher(ResourceDownloaderOperation downloader, string[] tags)
        {
            _downloader = downloader;
            Tags = tags ?? Array.Empty<string>();
        }

        /// <summary>本次下载的资源标签；为空表示全部资源。</summary>
        public string[] Tags { get; }

        /// <summary>需要下载的文件数。</summary>
        public int TotalCount => _downloader.TotalDownloadCount;

        /// <summary>需要下载的字节数。</summary>
        public long TotalBytes => _downloader.TotalDownloadBytes;

        /// <summary>是否有需要下载的文件。</summary>
        public bool NeedDownload => _downloader.TotalDownloadCount > 0;

        /// <summary>统计当前主资源包需要下载的文件。</summary>
        /// <param name="tags">只下载带这些标签的资源；为 null 或空表示全部。</param>
        /// <param name="maximumConcurrency">最大并发下载数。</param>
        /// <param name="retryCount">单个文件失败后的重试次数。</param>
        /// <returns>补丁对象。</returns>
        /// <exception cref="InvalidOperationException">ResourceManager 尚未初始化。</exception>
        public static ResourcePatcher Create(string[] tags = null, int maximumConcurrency = 10, int retryCount = 3)
        {
            var downloader = ResourceManager.Instance.CreateDownloader(tags, maximumConcurrency, retryCount);
            return new ResourcePatcher(downloader, tags);
        }

        /// <summary>开始下载并等待完成；没有需要下载的文件时立即返回。每个补丁对象只能下载一次。</summary>
        /// <param name="progress">进度回调；可为 null。</param>
        /// <param name="cancellationToken">取消时停止下载并抛出 <see cref="OperationCanceledException"/>；已下载的文件保留在缓存中。</param>
        /// <returns>下载完成的任务。</returns>
        /// <exception cref="InvalidOperationException">已经下载过，或下载失败（网络错误、校验失败等）。</exception>
        public async UniTask DownloadAsync(IProgress<ResourcePatchProgress> progress = null, CancellationToken cancellationToken = default)
        {
            if (_started)
            {
                throw new InvalidOperationException("[Resource] ResourcePatcher can only download once. Create a new patcher to retry.");
            }

            _started = true;
            cancellationToken.ThrowIfCancellationRequested();
            if (!NeedDownload)
            {
                progress?.Report(new ResourcePatchProgress(0, 0, 0, 0));
                return;
            }

            GameLog.Info(LogCategories.Patch, $"Download start: {LogStyle.Value(TotalCount)} files, {LogStyle.Value(FormatBytes(TotalBytes))}");
            string firstError = null;

            void OnProgress(DownloadProgressChangedEventArgs e)
            {
                progress?.Report(new ResourcePatchProgress(e.CurrentDownloadCount, e.TotalDownloadCount, e.CurrentDownloadBytes, e.TotalDownloadBytes));
            }

            void OnError(DownloadErrorEventArgs e)
            {
                firstError = firstError ?? $"{e.FileName}: {e.ErrorInfo}";
                GameLog.Warning(LogCategories.Patch, $"Download error: {LogStyle.Value(e.FileName)} {e.ErrorInfo}");
            }

            _downloader.DownloadProgressChanged += OnProgress;
            _downloader.DownloadError += OnError;
            try
            {
                _downloader.StartDownload();
                using (cancellationToken.Register(_downloader.CancelDownload))
                {
                    await ((IEnumerator)_downloader).ToUniTask();
                }
            }
            finally
            {
                _downloader.DownloadProgressChanged -= OnProgress;
                _downloader.DownloadError -= OnError;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (_downloader.Status != EOperationStatus.Succeeded)
            {
                var error = string.IsNullOrEmpty(_downloader.Error) ? firstError : _downloader.Error;
                throw new InvalidOperationException($"[Resource] Patch download failed: {error}");
            }

            GameLog.Info(LogCategories.Patch, LogStyle.Ok("Download complete"));
        }

        /// <summary>把字节数格式化为 KB / MB / GB。</summary>
        /// <param name="bytes">字节数。</param>
        /// <returns>格式化文本。</returns>
        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            if (bytes < 1024L * 1024)
            {
                return $"{bytes / 1024f:0.0} KB";
            }

            if (bytes < 1024L * 1024 * 1024)
            {
                return $"{bytes / (1024f * 1024f):0.0} MB";
            }

            return $"{bytes / (1024f * 1024f * 1024f):0.00} GB";
        }
    }
}
