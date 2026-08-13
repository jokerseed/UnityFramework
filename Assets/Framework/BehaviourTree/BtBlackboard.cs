using System;
using System.Collections.Generic;
using Framework.FixedMath;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 行为树黑板。按 key 读写共享数据；权威逻辑禁止依赖字典遍历顺序。
    /// </summary>
    public sealed class BtBlackboard
    {
        readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        /// <summary>当前条目数量。</summary>
        public int Count => _values.Count;

        /// <summary>是否包含指定键。</summary>
        /// <param name="key">键；不可为 null 或空。</param>
        /// <returns>存在则为 true。</returns>
        /// <exception cref="ArgumentException"><paramref name="key"/> 无效。</exception>
        public bool Contains(string key)
        {
            ValidateKey(key);
            return _values.ContainsKey(key);
        }

        /// <summary>写入任意对象值。</summary>
        /// <param name="key">键；不可为 null 或空。</param>
        /// <param name="value">值；可为 null。</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> 无效。</exception>
        public void Set(string key, object value)
        {
            ValidateKey(key);
            _values[key] = value;
        }

        /// <summary>写入定点数。</summary>
        /// <param name="key">键；不可为 null 或空。</param>
        /// <param name="value">定点数。</param>
        public void Set(string key, FP value) => Set(key, (object)value);

        /// <summary>写入整数。</summary>
        /// <param name="key">键；不可为 null 或空。</param>
        /// <param name="value">整数。</param>
        public void Set(string key, int value) => Set(key, (object)value);

        /// <summary>写入布尔值。</summary>
        /// <param name="key">键；不可为 null 或空。</param>
        /// <param name="value">布尔值。</param>
        public void Set(string key, bool value) => Set(key, (object)value);

        /// <summary>尝试读取指定类型的值。</summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="key">键；不可为 null 或空。</param>
        /// <param name="value">输出值；失败时为 default。</param>
        /// <returns>键存在且类型匹配则为 true。</returns>
        public bool TryGet<T>(string key, out T value)
        {
            ValidateKey(key);
            if (_values.TryGetValue(key, out var boxed) && boxed is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>读取值；键不存在或类型不匹配时返回默认值。</summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="key">键；不可为 null 或空。</param>
        /// <param name="defaultValue">缺省值。</param>
        /// <returns>读取到的值或 <paramref name="defaultValue"/>。</returns>
        public T Get<T>(string key, T defaultValue = default)
        {
            return TryGet(key, out T value) ? value : defaultValue;
        }

        /// <summary>移除指定键。</summary>
        /// <param name="key">键；不可为 null 或空。</param>
        /// <returns>确实移除了条目则为 true。</returns>
        public bool Remove(string key)
        {
            ValidateKey(key);
            return _values.Remove(key);
        }

        /// <summary>清空黑板。</summary>
        public void Clear() => _values.Clear();

        static void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Blackboard key must be non-empty.", nameof(key));
            }
        }
    }
}
