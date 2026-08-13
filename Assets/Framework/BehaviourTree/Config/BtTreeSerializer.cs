using System;
using UnityEngine;

namespace Framework.BehaviourTree
{
    /// <summary>行为树配置 JSON 导入导出（Unity <see cref="JsonUtility"/>）。</summary>
    public static class BtTreeSerializer
    {
        /// <summary>
        /// 将定义序列化为 JSON 字符串。
        /// </summary>
        /// <param name="definition">树定义；不可为 null。</param>
        /// <param name="prettyPrint">是否格式化缩进。</param>
        /// <returns>JSON 文本。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> 为 null。</exception>
        public static string ToJson(BtTreeDefinition definition, bool prettyPrint = true)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.Version = BtTreeDefinition.CurrentVersion;
            return JsonUtility.ToJson(definition, prettyPrint);
        }

        /// <summary>
        /// 从 JSON 反序列化树定义。
        /// </summary>
        /// <param name="json">JSON 文本；不可为 null 或空。</param>
        /// <returns>树定义。</returns>
        /// <exception cref="ArgumentException"><paramref name="json"/> 无效。</exception>
        public static BtTreeDefinition FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("JSON is empty.", nameof(json));
            }

            var definition = JsonUtility.FromJson<BtTreeDefinition>(json);
            if (definition == null)
            {
                throw new ArgumentException("Failed to parse behaviour tree JSON.", nameof(json));
            }

            if (definition.Nodes == null)
            {
                definition.Nodes = new System.Collections.Generic.List<BtConfigNode>();
            }

            return definition;
        }

        /// <summary>
        /// 从资产导出 JSON。
        /// </summary>
        /// <param name="asset">行为树资产；不可为 null。</param>
        /// <param name="prettyPrint">是否格式化。</param>
        /// <returns>JSON 文本。</returns>
        public static string ExportAsset(BtTreeAsset asset, bool prettyPrint = true)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            return ToJson(asset.Definition, prettyPrint);
        }

        /// <summary>
        /// 将 JSON 导入到资产定义（不自动保存）。
        /// </summary>
        /// <param name="asset">目标资产；不可为 null。</param>
        /// <param name="json">JSON 文本。</param>
        public static void ImportToAsset(BtTreeAsset asset, string json)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            asset.Definition = FromJson(json);
        }
    }
}
