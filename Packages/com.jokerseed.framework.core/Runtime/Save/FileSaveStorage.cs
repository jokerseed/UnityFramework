using System;
using System.Collections.Generic;
using System.IO;

namespace Framework.Save
{
    /// <summary>
    /// 本地文件存档：每个键一个 <c>{key}.sav</c> 文件。
    /// 写入时先把数据写进临时文件并强制落盘（<c>Flush(true)</c>），再替换正式文件，上一份正式文件保留为 <c>{key}.sav.bak</c>。
    /// 替换前的写入失败不会改动正式文件；正式文件损坏时 <see cref="SaveManager"/> 会读取并提升已验证的备份。
    /// 键只允许字母、数字、<c>_</c>、<c>-</c>、<c>.</c>。
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage, ISaveRecoveryStorage
    {
        const string Extension = ".sav";
        const string TempExtension = ".sav.tmp";
        const string BackupExtension = ".sav.bak";

        /// <summary>使用指定目录；目录不存在时在首次写入时创建。</summary>
        /// <param name="directory">存档目录，不可为空。</param>
        /// <exception cref="ArgumentException"><paramref name="directory"/> 为空。</exception>
        public FileSaveStorage(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Save directory is required.", nameof(directory));
            }

            Directory = directory;
        }

        /// <summary>存档目录。</summary>
        public string Directory { get; }

        /// <inheritdoc />
        public bool Exists(string key) => SaveFileExists(PathOf(key)) || SaveFileExists(BackupPathOf(key));

        /// <inheritdoc />
        public bool TryRead(string key, out byte[] data) => TryReadFile(PathOf(key), out data);

        /// <inheritdoc />
        public void Write(string key, byte[] data) => WriteCore(key, data, preserveBackup: false);

        /// <inheritdoc />
        public void RestoreBackup(string key, byte[] data) => WriteCore(key, data, preserveBackup: true);

        void WriteCore(string key, byte[] data, bool preserveBackup)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var path = PathOf(key);
            System.IO.Directory.CreateDirectory(Directory);
            var temp = Path.Combine(Directory, key + TempExtension);
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(data, 0, data.Length);
                // 只靠改名的原子性不够：数据块还在系统缓存里时断电，改名可能先落盘，留下 0 字节文件。
                stream.Flush(true);
            }

            if (File.Exists(path))
            {
                // null 表示不生成新备份：恢复不能让坏主档覆盖已验证的 .bak。
                // 不支持原子替换的平台直接抛错，不降级为先删除主档再复制。
                File.Replace(temp, path, preserveBackup ? null : BackupPathOf(key));
            }
            else
            {
                File.Move(temp, path);
            }
        }

        /// <inheritdoc />
        public bool TryReadBackup(string key, out byte[] data) => TryReadFile(BackupPathOf(key), out data);

        static bool TryReadFile(string path, out byte[] data)
        {
            try
            {
                data = File.ReadAllBytes(path);
                return true;
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                data = null;
                return false;
            }
            // 不用 File.Exists 探测读取：它会把权限等错误也当成“不存在”。
        }

        /// <inheritdoc />
        public bool Delete(string key)
        {
            var path = PathOf(key);
            var backup = BackupPathOf(key);
            // 先检查两个副本；权限问题或同名目录不是“没有存档”。
            var primaryExisted = SaveFileExists(path);
            var backupExisted = SaveFileExists(backup);
            if (primaryExisted)
            {
                File.Delete(path);
            }

            if (backupExisted)
            {
                File.Delete(backup);
            }

            return primaryExisted || backupExisted;
        }

        /// <inheritdoc />
        public IReadOnlyList<string> ListKeys()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                AddKeys(keys, Extension);
                AddKeys(keys, BackupExtension);
            }
            catch (DirectoryNotFoundException)
            {
                // 未创建过存档目录，不是读取失败。
            }

            var result = new List<string>(keys);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        void AddKeys(HashSet<string> keys, string extension)
        {
            foreach (var file in System.IO.Directory.GetFiles(Directory, "*" + extension))
            {
                // 保留平台的通配大小写语义，并过滤 Windows 三字符扩展名的宽松匹配。
                if (file.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    var name = Path.GetFileName(file);
                    keys.Add(name.Substring(0, name.Length - extension.Length));
                }
            }
        }

        static bool SaveFileExists(string path)
        {
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
                {
                    throw new IOException($"Save path '{path}' is a directory, not a file.");
                }

                return true;
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return false;
            }
        }

        string PathOf(string key)
        {
            ValidateKey(key);
            return Path.Combine(Directory, key + Extension);
        }

        string BackupPathOf(string key)
        {
            ValidateKey(key);
            return Path.Combine(Directory, key + BackupExtension);
        }

        static void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Save key is required.", nameof(key));
            }

            for (var i = 0; i < key.Length; i++)
            {
                var c = key[i];
                var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
                if (!ok)
                {
                    throw new ArgumentException($"Save key '{key}' contains invalid character '{c}'.", nameof(key));
                }
            }
        }
    }
}
