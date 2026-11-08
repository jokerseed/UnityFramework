using System;
using System.IO;
using Framework.Logging;

namespace Framework.Save
{
    // 保持读取/恢复逻辑独立于 Unity 生命周期，便于直接验证真实文件恢复流程。
    internal static class SaveReader
    {
        internal static bool TryLoad<T>(ISaveStorage storage, Func<byte[], T> deserialize, string key, out T value)
        {
            value = default;
            Exception primaryError = null;
            try
            {
                if (storage.TryRead(key, out var data))
                {
                    value = deserialize(data);
                    return true;
                }
            }
            catch (Exception ex) when (ex is FormatException || ex is IOException)
            {
                primaryError = ex;
            }

            if (TryLoadBackup(storage, deserialize, key, out value, out var backupData, out var backupError))
            {
                // 恢复失败必须直接传播，不能被读取/解析的 catch 吞掉或重试。
                // 修复主档后才报告成功，避免下次普通写入把坏主档轮换到好备份。
                if (storage is ISaveRecoveryStorage recovery)
                {
                    recovery.RestoreBackup(key, backupData);
                }

                var reason = primaryError == null ? "missing" : $"unusable ({primaryError.Message})";
                GameLog.Warning(LogCategories.Save, $"Save {LogStyle.Value(key)} {reason}, loaded from backup.");
                return true;
            }

            if (primaryError != null && backupError != null)
            {
                throw new FormatException($"Save '{key}' and its backup cannot be read.",
                    new AggregateException(primaryError, backupError));
            }

            if (primaryError != null)
            {
                throw primaryError is FormatException format
                    ? format
                    : new FormatException($"Save '{key}' cannot be read.", primaryError);
            }

            if (backupError != null)
            {
                throw new FormatException($"Backup of save '{key}' cannot be read.", backupError);
            }

            // 只有主档和备份都不存在才允许调用方创建默认值。
            return false;
        }

        static bool TryLoadBackup<T>(ISaveStorage storage, Func<byte[], T> deserialize, string key,
            out T value, out byte[] data, out Exception error)
        {
            value = default;
            data = null;
            error = null;
            if (!(storage is ISaveBackupStorage backups))
            {
                return false;
            }

            try
            {
                if (!backups.TryReadBackup(key, out data))
                {
                    return false;
                }

                value = deserialize(data);
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is IOException)
            {
                error = ex;
                data = null;
                GameLog.Warning(LogCategories.Save, $"Backup of {LogStyle.Value(key)} is unusable: {ex.Message}");
                return false;
            }
        }
    }
}
