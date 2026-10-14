#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Framework.Logging;
using UnityEditor;
using YooAsset.Editor;

namespace Framework.Editor.YooAsset
{
    /// <summary>
    /// 按 Project Settings → Framework → Collector 的规则生成 YooAsset Collector。
    /// 菜单：Tools/Framework/Res/Generate YooAsset Collector
    /// </summary>
    public static class YooAssetCollectorGenerator
    {
        readonly struct FolderRule
        {
            public readonly string CollectPath;
            public readonly CollectorPackMode PackMode;
            public readonly string GroupName;

            public FolderRule(string collectPath, CollectorPackMode packMode, string groupName)
            {
                CollectPath = collectPath;
                PackMode = packMode;
                GroupName = groupName;
            }
        }

        /// <summary>
        /// 清空并按设置重新生成 BundleCollectorSetting，保存到工程。
        /// 菜单：<c>Tools/Framework/Res/Generate YooAsset Collector</c>。
        /// </summary>
        [MenuItem("Tools/Framework/Res/Generate YooAsset Collector", priority = 200)]
        public static void Generate()
        {
            var settings = FrameworkSettings.instance.Collector;
            var rules = BuildRules(settings.Rules);
            if (rules.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "YooAsset Collector",
                    "设置中的收集目录都不存在，请检查 Project Settings → Framework → Collector。",
                    "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "YooAsset Collector",
                "将清空现有 Collector 配置，并按设置重新生成。手工调整的规则会丢失。",
                "生成",
                "取消"))
            {
                return;
            }

            BundleCollectorSettingData.ClearAll();
            BundleCollectorSettingData.ModifyUniqueBundleName(false);
            BundleCollectorSettingData.ModifyShowPackageView(true);

            var package = BundleCollectorSettingData.CreatePackage(settings.PackageName);
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
                    CollectorType = ECollectorType.MainAssetCollector,
                    AddressRuleName = nameof(AddressByPreImportPath),
                    PackRuleName = rule.PackMode == CollectorPackMode.PerFile
                        ? nameof(PackSeparately)
                        : nameof(PackDirectory),
                    FilterRuleName = nameof(CollectAll),
                });
            }

            package.CheckConfigError();
            BundleCollectorSettingData.SaveFile();
            AssetDatabase.Refresh();

            GameLog.Info(LogCategories.YooAsset, $"Collector generated  Package={LogStyle.Name(settings.PackageName)}  Rules={LogStyle.Value(rules.Count)}");
            EditorUtility.DisplayDialog(
                "YooAsset Collector",
                $"已写入 BundleCollectorSetting.asset\nCollector 数量: {rules.Count}",
                "确定");
        }

        static List<FolderRule> BuildRules(List<CollectorRuleSetting> settings)
        {
            var rules = new List<FolderRule>();
            foreach (var setting in settings)
            {
                if (string.IsNullOrEmpty(setting.CollectPath) || !AssetDatabase.IsValidFolder(setting.CollectPath))
                {
                    GameLog.Warning(LogCategories.YooAsset, $"Collector path not found, skipped: {LogStyle.Value(setting.CollectPath)}");
                    continue;
                }

                rules.Add(new FolderRule(setting.CollectPath, setting.PackMode, setting.GroupName));
                if (setting.SubfoldersPerFolder)
                {
                    AddSubfolders(rules, setting);
                }
            }

            return rules;
        }

        static void AddSubfolders(List<FolderRule> rules, CollectorRuleSetting setting)
        {
            foreach (var dir in Directory.GetDirectories(FrameworkSettings.ToAbsolute(setting.CollectPath)))
            {
                var folderName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(folderName) || folderName.StartsWith("."))
                {
                    continue;
                }

                var collectPath = $"{setting.CollectPath}/{folderName}";
                if (AssetDatabase.IsValidFolder(collectPath))
                {
                    rules.Add(new FolderRule(collectPath, CollectorPackMode.PerFolder, setting.GroupName));
                }
            }
        }
    }
}
#endif
