using System;
using System.Collections.Generic;
using Framework.Core;

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

        /// <summary>存储后端成功删除某个键的主档或备份后触发一次；不存在或删除失败时不触发。</summary>
        public event Action<string> Deleted;

        /// <summary>配置存储后端与序列化器；参数校验失败时保留原配置。</summary>
        /// <param name="storage">存储后端。</param>
        /// <param name="serializer">序列化器。</param>
        /// <exception cref="ArgumentNullException">参数为 null。</exception>
        public void Configure(ISaveStorage storage, ISaveSerializer serializer)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));

            _storage = storage;
            _serializer = serializer;
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
        /// 读取对象。主存档缺失、读取失败或解析失败时，若存储支持 <see cref="ISaveBackupStorage"/>，改读备份并记警告。
        /// 支持 <see cref="ISaveRecoveryStorage"/> 的后端在返回成功前提升已验证的备份；提升失败直接抛出异常。
        /// </summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="key">存档键。</param>
        /// <param name="value">存在时输出数据。</param>
        /// <returns>主档或备份可用时返回 true；两者都不存在时返回 false。</returns>
        /// <exception cref="InvalidOperationException">未配置。</exception>
        /// <exception cref="FormatException">存在存档或备份，但没有可读且可解析的副本。</exception>
        /// <exception cref="System.IO.IOException">备份有效，但修复主存档失败。</exception>
        /// <exception cref="UnauthorizedAccessException">无权读取存档或写入恢复数据。</exception>
        /// <exception cref="NotSupportedException">存储平台不支持所需的安全恢复操作。</exception>
        public bool TryLoad<T>(string key, out T value)
        {
            EnsureConfigured();
            return SaveReader.TryLoad(_storage, _serializer.Deserialize<T>, key, out value);
        }

        /// <summary>读取对象；不存在时用 <paramref name="createDefault"/> 创建（不自动保存）。</summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="key">存档键。</param>
        /// <param name="createDefault">创建默认值的工厂。</param>
        /// <returns>读取或新建的数据。</returns>
        /// <exception cref="InvalidOperationException">未配置。</exception>
        /// <exception cref="FormatException">存在存档或备份，但没有可读且可解析的副本。</exception>
        /// <exception cref="System.IO.IOException">备份有效，但修复主存档失败。</exception>
        /// <exception cref="UnauthorizedAccessException">无权读取存档或写入恢复数据。</exception>
        /// <exception cref="NotSupportedException">存储平台不支持所需的安全恢复操作。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="createDefault"/> 为 null。</exception>
        public T LoadOrDefault<T>(string key, Func<T> createDefault)
        {
            if (createDefault == null)
            {
                throw new ArgumentNullException(nameof(createDefault));
            }

            return TryLoad(key, out T value) ? value : createDefault();
        }

        /// <summary>主档或受支持的备份是否存在，不验证内容，也不执行恢复。</summary>
        /// <param name="key">存档键。</param>
        /// <returns>至少一份副本存在返回 true；内容是否有效须调用 TryLoad 检查。</returns>
        /// <exception cref="InvalidOperationException">未配置。</exception>
        /// <exception cref="System.IO.IOException">无法检查存档路径。</exception>
        /// <exception cref="UnauthorizedAccessException">无权检查存档路径。</exception>
        public bool Exists(string key)
        {
            EnsureConfigured();
            return _storage.Exists(key);
        }

        /// <summary>删除主档和受支持的备份；成功删除后通知 Deleted，删除失败不通知。</summary>
        /// <param name="key">存档键。</param>
        /// <returns>至少一份副本原先存在且删除完成返回 true；两者均不存在返回 false。</returns>
        /// <exception cref="System.IO.IOException">检查或删除失败；此前可能已删除部分副本。</exception>
        /// <exception cref="UnauthorizedAccessException">无权检查或删除存档。</exception>
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

        /// <summary>列出主档或受支持的备份对应的键，不验证或恢复内容。</summary>
        /// <returns>按键字符串去重的存档键列表，包含损坏副本对应的键。</returns>
        /// <exception cref="InvalidOperationException">未配置。</exception>
        /// <exception cref="System.IO.IOException">无法枚举存档。</exception>
        /// <exception cref="UnauthorizedAccessException">无权枚举存档。</exception>
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
