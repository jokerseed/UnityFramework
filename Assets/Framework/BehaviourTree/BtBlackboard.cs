using System;
using System.Collections.Generic;
using Framework.FixedMath;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 行为树黑板。int / bool / FP / uint 分槽存储，权威路径无装箱。
    /// object 袋仅供非权威引用，<see cref="Clone"/> 不复制。
    /// 禁止依赖字典遍历顺序做判定。
    /// </summary>
    public sealed class BtBlackboard
    {
        readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
        readonly Dictionary<string, bool> _bools = new Dictionary<string, bool>();
        readonly Dictionary<string, long> _fps = new Dictionary<string, long>();
        readonly Dictionary<string, uint> _ids = new Dictionary<string, uint>();
        readonly Dictionary<string, object> _objects = new Dictionary<string, object>();

        /// <summary>权威条目数量（不含 object 袋）。</summary>
        public int Count => _ints.Count + _bools.Count + _fps.Count + _ids.Count;

        /// <summary>是否包含指定键。</summary>
        /// <param name="key">键；不可为 null 或空。</param>
        /// <returns>存在则为 true。</returns>
        public bool Contains(string key)
        {
            ValidateKey(key);
            return _ints.ContainsKey(key) ||
                   _bools.ContainsKey(key) ||
                   _fps.ContainsKey(key) ||
                   _ids.ContainsKey(key) ||
                   _objects.ContainsKey(key);
        }

        /// <summary>写入整数。</summary>
        /// <param name="key">键。</param>
        /// <param name="value">整数。</param>
        public void Set(string key, int value)
        {
            ValidateKey(key);
            RemoveFromOtherStores(key, keepInts: true);
            _ints[key] = value;
        }

        /// <summary>写入布尔值。</summary>
        /// <param name="key">键。</param>
        /// <param name="value">布尔值。</param>
        public void Set(string key, bool value)
        {
            ValidateKey(key);
            RemoveFromOtherStores(key, keepBools: true);
            _bools[key] = value;
        }

        /// <summary>写入定点数。</summary>
        /// <param name="key">键。</param>
        /// <param name="value">定点数。</param>
        public void Set(string key, FP value)
        {
            ValidateKey(key);
            RemoveFromOtherStores(key, keepFps: true);
            _fps[key] = value.RawValue;
        }

        /// <summary>写入无符号 id。</summary>
        /// <param name="key">键。</param>
        /// <param name="value">id。</param>
        public void SetId(string key, uint value)
        {
            ValidateKey(key);
            RemoveFromOtherStores(key, keepIds: true);
            _ids[key] = value;
        }

        /// <summary>写入非权威引用；不进入快照。</summary>
        /// <param name="key">键。</param>
        /// <param name="value">引用；可为 null。</param>
        public void SetObject(string key, object value)
        {
            ValidateKey(key);
            _objects[key] = value;
        }

        /// <summary>按运行时类型分流；其余进非权威袋。</summary>
        /// <param name="key">键。</param>
        /// <param name="value">值；可为 null。</param>
        public void Set(string key, object value)
        {
            if (value is int i)
            {
                Set(key, i);
                return;
            }

            if (value is bool b)
            {
                Set(key, b);
                return;
            }

            if (value is FP fp)
            {
                Set(key, fp);
                return;
            }

            if (value is uint id)
            {
                SetId(key, id);
                return;
            }

            SetObject(key, value);
        }

        /// <summary>尝试读取整数。</summary>
        /// <param name="key">键。</param>
        /// <param name="value">输出。</param>
        /// <returns>命中则为 true。</returns>
        public bool TryGetInt(string key, out int value)
        {
            ValidateKey(key);
            return _ints.TryGetValue(key, out value);
        }

        /// <summary>尝试读取布尔。</summary>
        /// <param name="key">键。</param>
        /// <param name="value">输出。</param>
        /// <returns>命中则为 true。</returns>
        public bool TryGetBool(string key, out bool value)
        {
            ValidateKey(key);
            return _bools.TryGetValue(key, out value);
        }

        /// <summary>尝试读取无符号 id。</summary>
        /// <param name="key">键。</param>
        /// <param name="value">输出。</param>
        /// <returns>命中则为 true。</returns>
        public bool TryGetId(string key, out uint value)
        {
            ValidateKey(key);
            return _ids.TryGetValue(key, out value);
        }

        /// <summary>尝试读取定点。</summary>
        /// <param name="key">键。</param>
        /// <param name="value">输出。</param>
        /// <returns>命中则为 true。</returns>
        public bool TryGetFp(string key, out FP value)
        {
            ValidateKey(key);
            if (_fps.TryGetValue(key, out var raw))
            {
                value = FP.FromRaw(raw);
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>尝试读取指定类型的值。</summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="key">键。</param>
        /// <param name="value">输出值。</param>
        /// <returns>键存在且类型匹配则为 true。</returns>
        public bool TryGet<T>(string key, out T value)
        {
            ValidateKey(key);
            if (typeof(T) == typeof(int) && _ints.TryGetValue(key, out var i))
            {
                value = (T)(object)i;
                return true;
            }

            if (typeof(T) == typeof(bool) && _bools.TryGetValue(key, out var b))
            {
                value = (T)(object)b;
                return true;
            }

            if (typeof(T) == typeof(FP) && _fps.TryGetValue(key, out var raw))
            {
                value = (T)(object)FP.FromRaw(raw);
                return true;
            }

            if (typeof(T) == typeof(uint) && _ids.TryGetValue(key, out var id))
            {
                value = (T)(object)id;
                return true;
            }

            if (_objects.TryGetValue(key, out var boxed) && boxed is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>读取值；失败时返回默认值。</summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="key">键。</param>
        /// <param name="defaultValue">缺省值。</param>
        /// <returns>读取到的值或默认值。</returns>
        public T Get<T>(string key, T defaultValue = default)
        {
            return TryGet(key, out T value) ? value : defaultValue;
        }

        /// <summary>移除指定键（所有槽）。</summary>
        /// <param name="key">键。</param>
        /// <returns>确实移除了条目则为 true。</returns>
        public bool Remove(string key)
        {
            ValidateKey(key);
            var removed = _ints.Remove(key);
            removed |= _bools.Remove(key);
            removed |= _fps.Remove(key);
            removed |= _ids.Remove(key);
            removed |= _objects.Remove(key);
            return removed;
        }

        /// <summary>清空全部槽位。</summary>
        public void Clear()
        {
            _ints.Clear();
            _bools.Clear();
            _fps.Clear();
            _ids.Clear();
            _objects.Clear();
        }

        /// <summary>深拷贝权威槽（不含 object 袋）。</summary>
        /// <returns>独立副本。</returns>
        public BtBlackboard Clone()
        {
            var copy = new BtBlackboard();
            copy.CopyAuthoritativeFrom(this);
            return copy;
        }

        /// <summary>用权威槽覆盖当前黑板（不改 object 袋）。</summary>
        /// <param name="source">来源；不可为 null。</param>
        public void CopyAuthoritativeFrom(BtBlackboard source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _ints.Clear();
            _bools.Clear();
            _fps.Clear();
            _ids.Clear();
            foreach (var pair in source._ints)
            {
                _ints[pair.Key] = pair.Value;
            }

            foreach (var pair in source._bools)
            {
                _bools[pair.Key] = pair.Value;
            }

            foreach (var pair in source._fps)
            {
                _fps[pair.Key] = pair.Value;
            }

            foreach (var pair in source._ids)
            {
                _ids[pair.Key] = pair.Value;
            }
        }

        /// <summary>供调试列出权威键；顺序不稳定，禁止用于逻辑。</summary>
        /// <param name="results">输出；不可为 null。</param>
        public void CopyDebugKeys(List<string> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            foreach (var pair in _ints)
            {
                results.Add(pair.Key + "=" + pair.Value);
            }

            foreach (var pair in _bools)
            {
                results.Add(pair.Key + "=" + pair.Value);
            }

            foreach (var pair in _fps)
            {
                results.Add(pair.Key + "=" + FP.FromRaw(pair.Value));
            }

            foreach (var pair in _ids)
            {
                results.Add(pair.Key + "=" + pair.Value);
            }
        }

        void RemoveFromOtherStores(string key, bool keepInts = false, bool keepBools = false, bool keepFps = false, bool keepIds = false)
        {
            if (!keepInts)
            {
                _ints.Remove(key);
            }

            if (!keepBools)
            {
                _bools.Remove(key);
            }

            if (!keepFps)
            {
                _fps.Remove(key);
            }

            if (!keepIds)
            {
                _ids.Remove(key);
            }
        }

        static void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Blackboard key must be non-empty.", nameof(key));
            }
        }
    }
}
