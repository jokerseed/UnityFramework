#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Framework.Logging;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

namespace Framework.Editor.YooAsset
{
    /// <summary>
    /// 按 Bundles 目录规则生成 YooAsset Collector。
    /// 菜单：Tools/YooAsset/Generate Collector
    /// </summary>
    public static class YooAssetCollectorGenerator
    {
        const string PackageName = "DefaultPackage";
        const string ConfigsRoot = "Assets/Bundles/Configs";
        const string PrefabsRoot = "Assets/Bundles/Prefabs";

        /// <summary>Collector 打包粒度。</summary>
        enum PackMode
        {
            /// <summary>目录下每个文件单独打包。</summary>
            PerFile,

            /// <summary>整个目录打成一个包。</summary>
            PerFolder,
        }

        readonly struct FolderRule
        {
            public readonly string CollectPath;
            public readonly PackMode PackMode;
            public readonly string GroupName;
            public readonly string FilterRule;
            public readonly ECollectorType CollectorType;

            public FolderRule(
                string collectPath,
                PackMode packMode,
                string groupName,
                string filterRule = nameof(CollectAll),
                ECollectorType collectorType = ECollectorType.MainAssetCollector)
            {
                CollectPath = collectPath;
                PackMode = packMode;
                GroupName = groupName;
                FilterRule = filterRule;
                CollectorType = collectorType;
            }
        }

        /// <summary>
        /// 根据 <c>Assets/Bundles/Configs</c> 目录结构生成 YooAsset BundleCollectorSetting，并保存到工程。
        /// 菜单：<c>Tools/YooAsset/Generate Collector</c>。
        /// </summary>
        [MenuItem("Tools/YooAsset/Generate Collector")]
        public static void Generate()
        {
            var rules = BuildRules();
            if (rules.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "YooAsset Collector",
                    $"未找到可收集目录：{ConfigsRoot}",
                    "确定");
                return;
            }

            BundleCollectorSettingData.ClearAll();
            BundleCollectorSettingData.ModifyUniqueBundleName(false);
            BundleCollectorSettingData.ModifyShowPackageView(true);

            var package = BundleCollectorSettingData.CreatePackage(PackageName);
            package.EnableAddressable = true;
            package.LocationToLower = false;
            package.SupportExtensionless = true;
            package.AutoCollectShaders = true;

            var groups = new Dictionary<string, BundleCollectorGroup>();

            foreach (var rule in rules)
            {
                if (!groups.TryGetValue(rule.GroupName, out var group))
                {
                    group = BundleCollectorSettingData.CreateGroup(package, rule.GroupName);
                    groups[rule.GroupName] = group;
                }

                BundleCollectorSettingData.CreateCollector(group, new BundleCollector
                {
                    CollectPath = rule.CollectPath,
                    CollectorGUID = AssetDatabase.AssetPathToGUID(rule.CollectPath),
                    CollectorType = rule.CollectorType,
                    AddressRuleName = nameof(AddressByPreImportPath),
                    PackRuleName = rule.PackMode == PackMode.PerFile
                        ? nameof(PackSeparately)
                        : nameof(PackDirectory),
                    FilterRuleName = rule.FilterRule,
                });
            }

            package.CheckConfigError();
            BundleCollectorSettingData.SaveFile();
            AssetDatabase.Refresh();

            GameLog.Info(LogCategories.YooAsset, $"Collector generated  Package={LogStyle.Name(PackageName)}  Rules={LogStyle.Value(rules.Count)}");
            EditorUtility.DisplayDialog(
                "YooAsset Collector",
                $"已写入 BundleCollectorSetting.asset\nCollector 数量: {rules.Count}",
                "确定");
        }

        static List<FolderRule> BuildRules()
        {
            var rules = new List<FolderRule>();
            if (!AssetDatabase.IsValidFolder(ConfigsRoot))
            {
                return rules;
            }

            // Luban 二进制：根目录下每个 .bytes 单独打包
            rules.Add(new FolderRule(ConfigsRoot, PackMode.PerFile, "Configs"));

            if (AssetDatabase.IsValidFolder(PrefabsRoot))
            {
                rules.Add(new FolderRule(PrefabsRoot, PackMode.PerFile, "Prefabs"));
            }

            // 子目录按文件夹打包（后续扩展 battle/item 等模块时使用）
            var absoluteRoot = Path.Combine(Application.dataPath, "Bundles/Configs");
            if (!Directory.Exists(absoluteRoot))
            {
                return rules;
            }

            foreach (var dir in Directory.GetDirectories(absoluteRoot))
            {
                var folderName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(folderName) || folderName.StartsWith("."))
                {
                    continue;
                }

                var collectPath = $"{ConfigsRoot}/{folderName}";
                if (!AssetDatabase.IsValidFolder(collectPath))
                {
                    continue;
                }

                rules.Add(new FolderRule(collectPath, PackMode.PerFolder, "Configs"));
            }

            return rules;
        }
    }
}
#endif
