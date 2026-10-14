using System;
using System.Collections.Generic;
using System.IO;

namespace Framework.Save
{
    /// <summary>
    /// 本地文件存档：每个键一个 <c>{key}.sav</c> 文件。先写临时文件再替换，写入中断不会损坏旧存档。
    /// 键只允许字母、数字、<c>_</c>、<c>-</c>、<c>.</c>。
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        const string Extension = ".sav";
        const string TempExtension = ".sav.tmp";

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
            File.WriteAllBytes(temp, data);
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }

        /// <inheritdoc />
        public bool Delete(string key)
        {
            var path = PathOf(key);
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
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
                keys.Add(Path.GetFileNameWithoutExtension(file));
            }

            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        string PathOf(string key)
        {
            ValidateKey(key);
            return Path.Combine(Directory, key + Extension);
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
