using System;
using System.IO;
using System.Text;

namespace Framework.Logging
{
    /// <summary>
    /// 滚动文件 Sink：写入 <c>{目录}/{前缀}.log</c>，超过大小上限时依次改名为 <c>.1.log</c>、<c>.2.log</c>…，
    /// 超出保留个数的旧文件删除。始终输出纯文本（不含富文本标签）。
    /// WebGL 没有可写文件系统，不要在 WebGL 上启用。
    /// </summary>
    public sealed class FileLogSink : ILogSink, IDisposable
    {
        readonly object _lock = new object();
        readonly string _directory;
        readonly string _prefix;
        readonly long _maxBytes;
        readonly int _maxFiles;
        StreamWriter _writer;
        long _length;

        /// <summary>当前写入文件的完整路径。</summary>
        public string CurrentPath { get; }

        /// <summary>构造并打开日志文件（追加写）。</summary>
        /// <param name="directory">日志目录，不存在时创建。</param>
        /// <param name="prefix">文件名前缀，默认 <c>game</c>。</param>
        /// <param name="maxBytes">单个文件大小上限（字节），须大于 0。</param>
        /// <param name="maxFiles">包括当前文件在内保留的文件个数，须不小于 1。</param>
        /// <exception cref="ArgumentException"><paramref name="directory"/> 为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> 或 <paramref name="maxFiles"/> 越界。</exception>
        public FileLogSink(string directory, string prefix = "game", long maxBytes = 4 * 1024 * 1024, int maxFiles = 5)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Log directory is empty.", nameof(directory));
            }

            if (maxBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes));
            }

            if (maxFiles < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFiles));
            }

            _directory = directory;
            _prefix = string.IsNullOrWhiteSpace(prefix) ? "game" : prefix;
            _maxBytes = maxBytes;
            _maxFiles = maxFiles;
            CurrentPath = Path.Combine(_directory, _prefix + ".log");
            Directory.CreateDirectory(_directory);
            Open();
        }

        /// <summary>写入一条日志；可在任意线程调用。</summary>
        /// <param name="entry">要写入的日志条目。</param>
        public void Write(in LogEntry entry)
        {
            var text = LogStyle.FormatPlain(in entry);
            if (entry.Exception != null)
            {
                text = $"{text}\n{entry.Exception}";
            }

            lock (_lock)
            {
                if (_writer == null)
                {
                    return;
                }

                var bytes = Encoding.UTF8.GetByteCount(text) + 1;
                if (_length > 0 && _length + bytes > _maxBytes)
                {
                    Roll();
                }

                _writer.WriteLine(text);
                _writer.Flush();
                _length += bytes;
            }
        }

        /// <summary>关闭文件；之后的写入被忽略。</summary>
        public void Dispose()
        {
            lock (_lock)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }

        void Open()
        {
            var stream = new FileStream(CurrentPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _length = stream.Length;
            _writer = new StreamWriter(stream, new UTF8Encoding(false));
        }

        void Roll()
        {
            _writer.Dispose();
            _writer = null;

            var oldest = RolledPath(_maxFiles - 1);
            if (_maxFiles > 1 && File.Exists(oldest))
            {
                File.Delete(oldest);
            }

            for (var i = _maxFiles - 2; i >= 1; i--)
            {
                var from = RolledPath(i);
                if (File.Exists(from))
                {
                    File.Move(from, RolledPath(i + 1));
                }
            }

            if (_maxFiles > 1)
            {
                File.Move(CurrentPath, RolledPath(1));
            }
            else
            {
                File.Delete(CurrentPath);
            }

            Open();
        }

        string RolledPath(int index) => Path.Combine(_directory, $"{_prefix}.{index}.log");
    }
}
