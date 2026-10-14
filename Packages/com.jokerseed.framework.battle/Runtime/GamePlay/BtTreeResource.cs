using System;
using Framework.BehaviourTree;
using Framework.Res;
using UnityEngine;
using BtTree = Framework.BehaviourTree.BehaviourTree;

namespace Framework.GamePlay
{
    /// <summary>
    /// 经 YooAsset 加载行为树热更资源（<c>.bt.json</c> TextAsset），并走 <see cref="BtTreeTemplateCache"/>。
    /// </summary>
    public static class BtTreeResource
    {
        /// <summary>加载共享模板（缓存按寻址字符串）。</summary>
        /// <param name="treeId">树 id（与资产名一致，不含扩展名）。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <param name="cache">模板缓存；默认 <see cref="BtTreeTemplateCache.Shared"/>。</param>
        /// <returns>共享拓扑模板。</returns>
        /// <exception cref="ArgumentException"><paramref name="treeId"/> 为空。</exception>
        /// <exception cref="InvalidOperationException">资源缺失或编译失败。</exception>
        public static BtTreeTemplate LoadTemplate(
            string treeId,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null,
            BtTreeTemplateCache cache = null)
        {
            if (string.IsNullOrEmpty(treeId))
            {
                throw new ArgumentException("Tree id must be non-empty.", nameof(treeId));
            }

            cache ??= BtTreeTemplateCache.Shared;
            var location = ResourceAddresses.BehaviourTree(treeId);
            return cache.GetOrCompile(location, () =>
            {
                using (var handle = ResourceManager.Instance.LoadAssetSync<TextAsset>(location))
                {
                    if (!handle.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"Failed to load behaviour tree '{treeId}' at '{location}': {handle.Error}");
                    }

                    var textAsset = handle.GetAsset<TextAsset>();
                    if (textAsset == null)
                    {
                        throw new InvalidOperationException(
                            $"Behaviour tree asset at '{location}' is not a TextAsset.");
                    }

                    return BtTreeLoader.CompileToTemplateFromTextAsset(textAsset, customRegistry, subtrees);
                }
            });
        }

        /// <summary>加载新的行为树运行时实例（模板可缓存共享）。</summary>
        /// <param name="treeId">树 id。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <param name="cache">模板缓存；默认 Shared。</param>
        /// <returns>独立 Runtime 的行为树实例。</returns>
        public static BtTree LoadTree(
            string treeId,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null,
            BtTreeTemplateCache cache = null)
        {
            return new BtTree(LoadTemplate(treeId, customRegistry, subtrees, cache));
        }

        /// <summary>使指定树的模板缓存失效（热更单棵树后调用）。</summary>
        /// <param name="treeId">树 id。</param>
        /// <param name="cache">缓存；默认 Shared。</param>
        /// <returns>原先存在则为 true。</returns>
        public static bool Invalidate(string treeId, BtTreeTemplateCache cache = null)
        {
            if (string.IsNullOrEmpty(treeId))
            {
                return false;
            }

            cache ??= BtTreeTemplateCache.Shared;
            return cache.Invalidate(ResourceAddresses.BehaviourTree(treeId));
        }

        /// <summary>清空行为树模板缓存（热更换整包后调用）。</summary>
        /// <param name="cache">缓存；默认 Shared。</param>
        public static void ClearCache(BtTreeTemplateCache cache = null)
        {
            (cache ?? BtTreeTemplateCache.Shared).Clear();
        }
    }
}
