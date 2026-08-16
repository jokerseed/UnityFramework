using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 按资源 key 缓存已编译的 <see cref="BtTreeTemplate"/>，供多 Agent <see cref="BehaviourTree.Instantiate"/>。
    /// 热更换包后调用 <see cref="Invalidate"/> / <see cref="Clear"/>。
    /// </summary>
    public sealed class BtTreeTemplateCache
    {
        readonly Dictionary<string, BtTreeTemplate> _templates =
            new Dictionary<string, BtTreeTemplate>(StringComparer.Ordinal);

        /// <summary>全局默认缓存（进程内单例）。</summary>
        public static BtTreeTemplateCache Shared { get; } = new BtTreeTemplateCache();

        /// <summary>当前缓存条目数。</summary>
        public int Count => _templates.Count;

        /// <summary>若已缓存则返回模板，否则用工厂编译并写入缓存。</summary>
        /// <param name="key">缓存键（建议用 YooAsset 寻址或 treeId）；不可为空。</param>
        /// <param name="compile">编译委托；不可为 null。</param>
        /// <returns>共享模板。</returns>
        /// <exception cref="ArgumentException"><paramref name="key"/> 为空。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="compile"/> 为 null。</exception>
        public BtTreeTemplate GetOrCompile(string key, Func<BtTreeTemplate> compile)
        {
            ValidateKey(key);
            if (compile == null)
            {
                throw new ArgumentNullException(nameof(compile));
            }

            if (_templates.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var template = compile();
            if (template == null)
            {
                throw new InvalidOperationException("Compile returned null template for key: " + key);
            }

            _templates[key] = template;
            return template;
        }

        /// <summary>从 JSON 编译（或取缓存）模板。</summary>
        /// <param name="key">缓存键。</param>
        /// <param name="json">JSON 文本。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>共享模板。</returns>
        public BtTreeTemplate GetOrCompileFromJson(
            string key,
            string json,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            return GetOrCompile(key, () => BtTreeLoader.CompileToTemplateFromJson(json, customRegistry, subtrees));
        }

        /// <summary>从 <see cref="TextAsset"/> 编译（或取缓存）模板。</summary>
        /// <param name="key">缓存键。</param>
        /// <param name="textAsset">文本资产；不可为 null。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>共享模板。</returns>
        public BtTreeTemplate GetOrCompileFromTextAsset(
            string key,
            TextAsset textAsset,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            return GetOrCompile(
                key,
                () => BtTreeLoader.CompileToTemplateFromTextAsset(textAsset, customRegistry, subtrees));
        }

        /// <summary>尝试获取已缓存模板。</summary>
        /// <param name="key">缓存键。</param>
        /// <param name="template">命中时的模板。</param>
        /// <returns>命中则为 true。</returns>
        public bool TryGet(string key, out BtTreeTemplate template)
        {
            if (string.IsNullOrEmpty(key))
            {
                template = null;
                return false;
            }

            return _templates.TryGetValue(key, out template);
        }

        /// <summary>使指定 key 失效（热更换单棵树后调用）。</summary>
        /// <param name="key">缓存键。</param>
        /// <returns>原先存在则为 true。</returns>
        public bool Invalidate(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return _templates.Remove(key);
        }

        /// <summary>清空全部缓存（热更换整包后调用）。</summary>
        public void Clear()
        {
            _templates.Clear();
        }

        static void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Cache key must be non-empty.", nameof(key));
            }
        }
    }
}
