#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Framework.BehaviourTree.Editor
{
    /// <summary>按资产名解析子树（Editor）。</summary>
    public sealed class BtEditorSubtreeResolver : IBtSubtreeResolver
    {
        /// <inheritdoc />
        public bool TryResolve(string subtreeId, out BtTreeDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(subtreeId))
            {
                return false;
            }

            var guids = AssetDatabase.FindAssets("t:BtTreeAsset");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<BtTreeAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                var name = string.IsNullOrEmpty(asset.Definition.TreeName)
                    ? Path.GetFileNameWithoutExtension(path)
                    : asset.Definition.TreeName;
                if (name == subtreeId || Path.GetFileNameWithoutExtension(path) == subtreeId)
                {
                    definition = asset.Definition;
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>行为树 Editor 工具：创建资产、导入导出 JSON。</summary>
    public static class BtEditorUtility
    {
        /// <summary>默认资产目录。</summary>
        public const string DefaultTreeFolder = "Assets/Bundles/BehaviourTrees";

        /// <summary>导出 JSON 扩展名（含点）。</summary>
        public const string JsonExtension = ".bt.json";

        /// <summary>
        /// 创建新的行为树资产。
        /// </summary>
        /// <param name="folder">目标文件夹；默认 <see cref="DefaultTreeFolder"/>。</param>
        /// <returns>创建的资产；取消则为 null。</returns>
        public static BtTreeAsset CreateTreeAsset(string folder = null)
        {
            folder = string.IsNullOrEmpty(folder) ? DefaultTreeFolder : folder;
            EnsureFolder(folder);

            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/BehaviourTree.asset");
            var asset = ScriptableObject.CreateInstance<BtTreeAsset>();
            var root = CreateDefaultRootNode();
            asset.Definition.TreeName = Path.GetFileNameWithoutExtension(path);
            asset.Definition.RootNodeId = root.Id;
            asset.Definition.Nodes.Add(root);

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            return asset;
        }

        /// <summary>
        /// 导出 JSON 到与资产同目录（<c>{assetName}.bt.json</c>）。
        /// </summary>
        /// <param name="asset">行为树资产。</param>
        /// <returns>写入的绝对路径；失败返回 null。</returns>
        public static string ExportJsonNextToAsset(BtTreeAsset asset)
        {
            if (asset == null)
            {
                return null;
            }

            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("Export JSON", "Save the asset to the project first.", "OK");
                return null;
            }

            var jsonPath = Path.ChangeExtension(assetPath, null) + JsonExtension;
            var json = BtTreeSerializer.ExportAsset(asset, true);
            File.WriteAllText(jsonPath, json);
            AssetDatabase.Refresh();
            return jsonPath;
        }

        /// <summary>
        /// 扫描项目中全部 <see cref="BtTreeAsset"/>，导出旁路 <c>.bt.json</c> 运行时资源。
        /// </summary>
        /// <returns>成功导出的资产数量。</returns>
        public static int ExportAllRuntimeJson()
        {
            var guids = AssetDatabase.FindAssets("t:BtTreeAsset");
            var count = 0;
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<BtTreeAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                if (ExportJsonNextToAsset(asset) != null)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 从 JSON 文件导入到资产。
        /// </summary>
        /// <param name="asset">目标资产。</param>
        /// <param name="jsonPath">JSON 路径（项目内相对或绝对）。</param>
        public static void ImportJsonToAsset(BtTreeAsset asset, string jsonPath)
        {
            if (asset == null || string.IsNullOrEmpty(jsonPath))
            {
                return;
            }

            var text = File.ReadAllText(jsonPath);
            BtTreeSerializer.ImportToAsset(asset, text);
            EditorUtility.SetDirty(asset);
        }

        /// <summary>
        /// 尝试编译并显示结果。
        /// </summary>
        /// <param name="asset">行为树资产。</param>
        /// <returns>编译成功则为 true。</returns>
        public static bool TryCompilePreview(BtTreeAsset asset)
        {
            if (asset == null)
            {
                return false;
            }

            try
            {
                BtTreeCompiler.Compile(asset, null, new BtEditorSubtreeResolver());
                return true;
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Compile Failed", ex.Message, "OK");
                return false;
            }
        }

        internal static BtConfigNode CreateDefaultRootNode()
        {
            return new BtConfigNode
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = BtNodeKind.Selector,
                DisplayName = "Root",
                EditorPosition = new Vector2(200f, 80f),
            };
        }

        internal static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
#endif
