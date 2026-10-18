using System;
using System.Collections.Generic;
using System.IO;
using Framework.Core;
using Framework.Logging;

namespace Framework.Save
{
    /// <summary>
    /// 存档入口：按键保存/读取对象，存储后端与序列化器可替换。
    /// 由 <see cref="SaveModule"/> 配置；未配置时调用读写会抛异常。
    /// </summary>
    public sealed class SaveManager : PersistentSingleton<SaveManager>
    {
        ISaveStorage _storage;
        ISaveSerializer _serializer;

        /// <summary>是否已配置存储与序列化器。</summary>
        public bool IsConfigured => _storage != null && _serializer != null;

        /// <summary>当前存储后端。</summary>
        public ISaveStorage Storage => _storage;

        /// <summary>某个键保存成功后触发。</summary>
        public event Action<string> Saved;

        /// <summary>某个键被删除后触发。</summary>
        public event Action<string> Deleted;

        /// <summary>配置存储后端与序列化器。</summary>
        /// <param name="storage">存储后端。</param>
        /// <param name="serializer">序列化器。</param>
        /// <exception cref="ArgumentNullException">参数为 null。</exception>
        public void Configure(ISaveStorage storage, ISaveSerializer serializer)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>保存对象。</summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="key">存档键。</param>
        /// <param name="value">数据。</param>
        /// <exception cref="InvalidOperationException">未配置。</exception>
        public void Save<T>(string key, T value)
        {
            EnsureConfigured();
            _storage.Write(key, _serializer.Serialize(value));
            Saved?.Invoke(key);
        }

        /// <summary>
        /// 读取对象。主存档读取失败或解析失败时，若存储支持 <see cref="ISaveBackupStorage"/>，改读上一份备份并记警告。
        /// </summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="key">存档键。</param>
        /// <param name="value">存在时输出数据。</param>
        /// <returns>存在返回 true；不存在返回 false。</returns>
        /// <exception cref="InvalidOperationException">未配置。</exception>
        /// <exception cref="FormatException">存档与备份都损坏或与类型不符。</exception>
        public bool TryLoad<T>(string key, out T value)
        {
            EnsureConfigured();
            Exception primaryError;
            try
            {
                if (!_storage.TryRead(key, out var data))
                {
                    if (TryLoadBackup(key, out value))
                    {
                        GameLog.Warning(LogCategories.Save, $"Save {LogStyle.Value(key)} missing, restored from backup.");
                        return true;
                    }

                    value = default;
                    return false;
                }

                value = _serializer.Deserialize<T>(data);
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is IOException)
            {
                primaryError = ex;
            }

            if (TryLoadBackup(key, out value))
            {
                GameLog.Warning(LogCategories.Save, $"Save {LogStyle.Value(key)} corrupted ({primaryError.Message}), restored from backup.");
                return true;
            }

            throw primaryError is FormatException format
                ? format
                : new FormatException($"Save '{key}' cannot be read.", primaryError);
        }

        bool TryLoadBackup<T>(string key, out T value)
        {
            value = default;
            if (!(_storage is ISaveBackupStorage backups))
            {
                return false;
            }

            try
            {
                if (!backups.TryReadBackup(key, out var data))
                {
                    return false;
                }

                value = _serializer.Deserialize<T>(data);
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is IOException)
            {
                GameLog.Warning(LogCategories.Save, $"Backup of {LogStyle.Value(key)} is unusable: {ex.Message}");
                value = default;
                return false;
            }
        }

        /// <summary>读取对象；不存在时用 <paramref name="createDefault"/> 创建（不自动保存）。</summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="key">存档键。</param>
        /// <param name="createDefault">创建默认值的工厂。</param>
        /// <returns>读取或新建的数据。</returns>
        /// <exception cref="InvalidOperationException">未配置。</exception>
        /// <exception cref="FormatException">存档内容损坏或与类型不符。</exception>
        public T LoadOrDefault<T>(string key, Func<T> createDefault)
        {
            if (createDefault == null)
            {
                throw new ArgumentNullException(nameof(createDefault));
            }

            return TryLoad(key, out T value) ? value : createDefault();
        }

        /// <summary>键是否存在。</summary>
        /// <param name="key">存档键。</param>
        /// <returns>存在返回 true。</returns>
        /// <exception cref="InvalidOperationException">未配置。</exception>
        public bool Exists(string key)
        {
            EnsureConfigured();
            return _storage.Exists(key);
        }

        /// <summary>删除存档。</summary>
        /// <param name="key">存档键。</param>
        /// <returns>存在并删除返回 true。</returns>
        /// <exception cref="InvalidOperationException">未配置。</exception>
        public bool Delete(string key)
        {
            EnsureConfigured();
            if (!_storage.Delete(key))
            {
                return false;
            }

            Deleted?.Invoke(key);
            return true;
        }

        /// <summary>列出全部存档键。</summary>
        /// <returns>存档键列表。</returns>
        /// <exception cref="InvalidOperationException">未配置。</exception>
        public IReadOnlyList<string> ListKeys()
        {
            EnsureConfigured();
            return _storage.ListKeys();
        }

        /// <summary>清除配置与事件订阅。</summary>
        public void Shutdown()
        {
            _storage = null;
            _serializer = null;
            Saved = null;
            Deleted = null;
        }

        void EnsureConfigured()
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("SaveManager is not configured. Add SaveModule or call Configure first.");
            }
        }
    }
}
