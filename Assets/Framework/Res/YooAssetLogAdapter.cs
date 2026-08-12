using System;
using Framework.Logging;
using YooAsset;

namespace Framework.Res
{
    /// <summary>
    /// 将 YooAsset 内部日志转到 <see cref="GameLog"/>。
    /// 关闭时 abort 未完成异步任务属于正常清理，降为 Debug，避免停 Play 刷 Warning。
    /// </summary>
    sealed class YooAssetLogAdapter : ILogger
    {
        const string AbortedToken = "has been aborted";

        /// <inheritdoc />
        public void Log(string message)
        {
            GameLog.Debug(LogCategories.YooAsset, message);
        }

        /// <inheritdoc />
        public void LogWarning(string message)
        {
            if (!string.IsNullOrEmpty(message) &&
                message.IndexOf(AbortedToken, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                GameLog.Debug(LogCategories.YooAsset, message);
                return;
            }

            GameLog.Warning(LogCategories.YooAsset, message);
        }

        /// <inheritdoc />
        public void LogError(string message)
        {
            GameLog.Error(LogCategories.YooAsset, message);
        }

        /// <inheritdoc />
        public void LogException(Exception exception)
        {
            GameLog.Exception(LogCategories.YooAsset, exception);
        }
    }
}
