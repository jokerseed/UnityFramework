using System;

namespace Framework.Save
{
    /// <summary>
    /// 设置存储：一个键对应一份设置对象（音量、画质、语言等），修改后立即保存并通知订阅者。
    /// </summary>
    /// <typeparam name="T">设置类型，须可被当前 <see cref="ISaveSerializer"/> 序列化。</typeparam>
    public sealed class SettingsStore<T> where T : class, new()
    {
        readonly SaveManager _saves;
        T _current;

        /// <summary>创建设置存储。</summary>
        /// <param name="saves">存档管理器。</param>
        /// <param name="key">存档键。</param>
        /// <exception cref="ArgumentNullException"><paramref name="saves"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> 为空。</exception>
        public SettingsStore(SaveManager saves, string key)
        {
            _saves = saves ?? throw new ArgumentNullException(nameof(saves));
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Settings key is required.", nameof(key));
            }

            Key = key;
        }

        /// <summary>存档键。</summary>
        public string Key { get; }

        /// <summary>当前设置；首次访问时从存档读取，不存在则新建。</summary>
        /// <exception cref="FormatException">存档内容损坏。</exception>
        public T Current => _current ?? (_current = _saves.LoadOrDefault(Key, () => new T()));

        /// <summary>设置被修改并保存后触发。</summary>
        public event Action<T> Changed;

        /// <summary>修改设置、保存并触发 <see cref="Changed"/>。</summary>
        /// <param name="mutate">修改逻辑。</param>
        /// <exception cref="ArgumentNullException"><paramref name="mutate"/> 为 null。</exception>
        public void Update(Action<T> mutate)
        {
            if (mutate == null)
            {
                throw new ArgumentNullException(nameof(mutate));
            }

            var settings = Current;
            mutate(settings);
            _saves.Save(Key, settings);
            Changed?.Invoke(settings);
        }

        /// <summary>恢复默认值、保存并触发 <see cref="Changed"/>。</summary>
        public void ResetToDefault()
        {
            _current = new T();
            _saves.Save(Key, _current);
            Changed?.Invoke(_current);
        }

        /// <summary>丢弃内存中的设置，下次访问 <see cref="Current"/> 时重新读取。</summary>
        public void Reload()
        {
            _current = null;
        }
    }
}
