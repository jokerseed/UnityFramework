using System;
using System.Collections.Generic;
using System.IO;

namespace Framework.Save
{
    /// <summary>
    /// 本地文件存档：每个键一个 <c>{key}.sav</c> 文件。
    /// 写入时先把数据写进临时文件并强制落盘（<c>Flush(true)</c>），再替换正式文件，上一份正式文件保留为 <c>{key}.sav.bak</c>。
    /// 写入中断或断电不会留下半截存档；正式文件损坏时 <see cref="SaveManager"/> 会改读备份。
    /// 键只允许字母、数字、<c>_</c>、<c>-</c>、<c>.</c>。
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage, ISaveBackupStorage
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
        public bool Exists(string key) => File.Exists(PathOf(key));

        /// <inheritdoc />
        public bool TryRead(string key, out byte[] data)
        {
            var path = PathOf(key);
            if (!File.Exists(path))
            {
                data = null;
                return false;
            }

            data = File.ReadAllBytes(path);
            return true;
        }

        /// <inheritdoc />
        public void Write(string key, byte[] data)
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
                File.Replace(temp, path, BackupPathOf(key));
            }
            else
            {
                File.Move(temp, path);
            }
        }

        /// <inheritdoc />
        public bool TryReadBackup(string key, out byte[] data)
        {
            var backup = BackupPathOf(key);
            if (!File.Exists(backup))
            {
                data = null;
                return false;
            }

            data = File.ReadAllBytes(backup);
            return true;
        }

        /// <inheritdoc />
        public bool Delete(string key)
        {
            var path = PathOf(key);
            var backup = BackupPathOf(key);
            var existed = File.Exists(path);
            if (existed)
            {
                File.Delete(path);
            }

            if (File.Exists(backup))
            {
                File.Delete(backup);
            }

            return existed;
        }

        /// <inheritdoc />
        public IReadOnlyList<string> ListKeys()
        {
            var keys = new List<string>();
            if (!System.IO.Directory.Exists(Directory))
            {
                return keys;
            }

            foreach (var file in System.IO.Directory.GetFiles(Directory, "*" + Extension))
            {
                // Windows 的三字符扩展名通配会顺带匹配更长的扩展名，这里只认精确的 .sav。
                if (file.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(Path.GetFileNameWithoutExtension(file));
                }
            }

            keys.Sort(StringComparer.Ordinal);
            return keys;
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
