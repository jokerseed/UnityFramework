using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Framework.Logging
{
    /// <summary>
    /// 滚动文件 Sink：写入 <c>{目录}/{前缀}.log</c>，超过大小上限时依次改名为 <c>.1.log</c>、<c>.2.log</c>…，
    /// 超出保留个数的旧文件删除。始终输出纯文本（富文本标签由 <see cref="LogStyle.FormatPlain"/> 去掉）。
    /// 写入走缓冲：Warning 及以上立即落盘，其余每秒落盘一次，或在 <see cref="Flush"/> / <see cref="Dispose"/> 时落盘。
    /// 磁盘满、文件被占用、滚动改名失败等 IO 错误只会丢掉当条日志，不会抛给调用方；滚动失败时继续追加当前文件。
    /// WebGL 没有可写文件系统，不要在 WebGL 上启用。
    /// </summary>
    public sealed class FileLogSink : ILogSink, IDisposable
    {
        const int FlushIntervalMilliseconds = 1000;
        static readonly int NewLineBytes = Encoding.UTF8.GetByteCount(Environment.NewLine);

        readonly object _lock = new object();
        readonly string _directory;
        readonly string _prefix;
        readonly long _maxBytes;
        readonly int _maxFiles;
        readonly Timer _flushTimer;
        StreamWriter _writer;
        long _length;
        bool _dirty;
        bool _disposed;

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
            _flushTimer = new Timer(_ => Flush(), null, FlushIntervalMilliseconds, FlushIntervalMilliseconds);
        }

        /// <summary>写入一条日志；可在任意线程调用，不抛异常。</summary>
        /// <param name="entry">要写入的日志条目。</param>
        public void Write(in LogEntry entry)
        {
            string text;
            try
            {
                text = LogStyle.FormatPlain(in entry);
                if (entry.Exception != null)
                {
                    text = $"{text}\n{entry.Exception}";
                }
            }
            catch
            {
                return;
            }

            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    if (_writer == null)
                    {
                        Open();
                    }

                    var bytes = Encoding.UTF8.GetByteCount(text) + NewLineBytes;
                    if (_length > 0 && _length + bytes > _maxBytes)
                    {
                        Roll();
                    }

                    _writer.WriteLine(text);
                    _length += bytes;
                    if (entry.Level >= LogLevel.Warning)
                    {
                        _writer.Flush();
                        _dirty = false;
                    }
                    else
                    {
                        _dirty = true;
                    }
                }
                catch
                {
                    // 磁盘满、文件被占用等：丢掉这一条。_writer 为 null 时下一条会重新打开文件。
                }
            }
        }

        /// <summary>把缓冲中的日志写入磁盘。</summary>
        public void Flush()
        {
            lock (_lock)
            {
                if (_writer == null || !_dirty)
                {
                    return;
                }

                try
                {
                    _writer.Flush();
                }
                catch
                {
                    // 落盘失败留到下一次再试。
                    return;
                }

                _dirty = false;
            }
        }

        /// <summary>落盘并关闭文件；之后的写入被忽略。</summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _flushTimer.Dispose();
                CloseWriter();
            }
        }

        void Open()
        {
            var stream = new FileStream(CurrentPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _length = stream.Length;
            _writer = new StreamWriter(stream, new UTF8Encoding(false));
        }

        void CloseWriter()
        {
            if (_writer == null)
            {
                return;
            }

            try
            {
                _writer.Dispose();
            }
            catch
            {
                // 关闭时的 IO 错误不再处理。
            }

            _writer = null;
            _dirty = false;
        }

        void Roll()
        {
            CloseWriter();
            var rolled = false;
            try
            {
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

                rolled = true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            Open();
            if (!rolled)
            {
                // 改名失败（例如文件被其它进程占用）：继续追加当前文件，再写满一个上限后重试滚动。
                _length = 0;
            }
        }

        string RolledPath(int index) => Path.Combine(_directory, $"{_prefix}.{index}.log");
    }
}
