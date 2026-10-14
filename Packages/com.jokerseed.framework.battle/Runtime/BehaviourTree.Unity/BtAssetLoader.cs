using System;
using UnityEngine;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 从 Unity 资产加载行为树：<see cref="BtTreeAsset"/>（ScriptableObject）与 <see cref="TextAsset"/>（通常是热更的 <c>.bt.json</c>）。
    /// 纯 JSON 文本的加载在引擎无关的 <see cref="BtTreeLoader"/> 里。
    /// </summary>
    public static class BtAssetLoader
    {
        /// <summary>从 ScriptableObject 资产编译为运行时实例。</summary>
        /// <param name="asset">行为树资产；不可为 null。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>运行时行为树。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="asset"/> 为 null。</exception>
        public static BehaviourTree Load(
            BtTreeAsset asset,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            return new BehaviourTree(CompileToTemplate(asset, customRegistry, subtrees));
        }

        /// <summary>从 ScriptableObject 资产编译为共享模板。</summary>
        /// <param name="asset">行为树资产；不可为 null。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>共享拓扑模板。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="asset"/> 为 null。</exception>
        public static BtTreeTemplate CompileToTemplate(
            BtTreeAsset asset,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            return BtTreeCompiler.CompileToTemplate(asset.Definition, customRegistry, subtrees);
        }

        /// <summary>从 <see cref="TextAsset"/> 编译为运行时实例。</summary>
        /// <param name="textAsset">文本资产；不可为 null。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>运行时行为树。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="textAsset"/> 为 null。</exception>
        public static BehaviourTree LoadFromTextAsset(
            TextAsset textAsset,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            return new BehaviourTree(CompileToTemplateFromTextAsset(textAsset, customRegistry, subtrees));
        }

        /// <summary>从 <see cref="TextAsset"/> 编译为共享模板。</summary>
        /// <param name="textAsset">文本资产；不可为 null。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>共享拓扑模板。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="textAsset"/> 为 null。</exception>
        public static BtTreeTemplate CompileToTemplateFromTextAsset(
            TextAsset textAsset,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            if (textAsset == null)
            {
                throw new ArgumentNullException(nameof(textAsset));
            }

            return BtTreeLoader.CompileToTemplateFromJson(textAsset.text, customRegistry, subtrees);
        }

        /// <summary>从 <see cref="TextAsset"/> 编译（或取缓存）模板。</summary>
        /// <param name="cache">模板缓存；不可为 null。</param>
        /// <param name="key">缓存键。</param>
        /// <param name="textAsset">文本资产；不可为 null。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <returns>共享模板。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="cache"/> 为 null。</exception>
        public static BtTreeTemplate GetOrCompileFromTextAsset(
            this BtTreeTemplateCache cache,
            string key,
            TextAsset textAsset,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            return cache.GetOrCompile(key, () => CompileToTemplateFromTextAsset(textAsset, customRegistry, subtrees));
        }

        /// <summary>从资产导出 JSON。</summary>
        /// <param name="asset">行为树资产；不可为 null。</param>
        /// <param name="prettyPrint">是否格式化。</param>
        /// <returns>JSON 文本。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="asset"/> 为 null。</exception>
        public static string ExportAsset(BtTreeAsset asset, bool prettyPrint = true)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            return BtTreeSerializer.ToJson(asset.Definition, prettyPrint);
        }

        /// <summary>将 JSON 导入到资产定义（不自动保存）。</summary>
        /// <param name="asset">目标资产；不可为 null。</param>
        /// <param name="json">JSON 文本。</param>
        /// <exception cref="ArgumentNullException"><paramref name="asset"/> 为 null。</exception>
        public static void ImportToAsset(BtTreeAsset asset, string json)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            asset.Definition = BtTreeSerializer.FromJson(json);
        }

        /// <summary>编辑器坐标转成 Unity <see cref="Vector2"/>。</summary>
        /// <param name="position">编辑器坐标。</param>
        /// <returns>对应的 <see cref="Vector2"/>。</returns>
        public static Vector2 ToVector2(this BtEditorPosition position) => new Vector2(position.x, position.y);

        /// <summary>Unity <see cref="Vector2"/> 转成编辑器坐标。</summary>
        /// <param name="position">Unity 坐标。</param>
        /// <returns>对应的 <see cref="BtEditorPosition"/>。</returns>
        public static BtEditorPosition ToBtPosition(this Vector2 position) => new BtEditorPosition(position.x, position.y);
    }
}
