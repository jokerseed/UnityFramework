using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Framework.Editor
{
    /// <summary>Collector 打包粒度。</summary>
    public enum CollectorPackMode
    {
        /// <summary>目录下每个文件单独打包。</summary>
        PerFile,

        /// <summary>整个目录打成一个包。</summary>
        PerFolder,
    }

    /// <summary>一条 YooAsset Collector 规则。</summary>
    [Serializable]
    public sealed class CollectorRuleSetting
    {
        /// <summary>收集目录（Assets/ 开头）。</summary>
        public string CollectPath;

        /// <summary>打包粒度。</summary>
        public CollectorPackMode PackMode;

        /// <summary>所属分组名。</summary>
        public string GroupName;

        /// <summary>为 true 时，目录下每个子目录再各加一条按目录打包的规则。</summary>
        public bool SubfoldersPerFolder;
    }

    /// <summary>Luban 打表路径（均相对工程根目录）。</summary>
    [Serializable]
    public sealed class LubanSettings
    {
        /// <summary>dotnet 可执行文件；不在 PATH 上时填绝对路径。</summary>
        public string DotnetExecutable = "dotnet";

        /// <summary>Luban 源码工程；<see cref="LubanDll"/> 不存在时用它构建。</summary>
        public string LubanProject = "ThirdParty/luban-4.11.0/src/Luban/Luban.csproj";

        /// <summary>Luban CLI 程序集。</summary>
        public string LubanDll = "ThirdParty/luban-4.11.0/Tools/Luban/Luban.dll";

        /// <summary>luban.conf。</summary>
        public string ConfFile = "Config/Luban/luban.conf";

        /// <summary>生成代码命名规则（codeTypePrefix 等）。</summary>
        public string CodegenRules = "Config/Luban/codegen.json";

        /// <summary>C# 代码输出目录。</summary>
        public string OutputCodeDir = "Assets/Generated/Luban/Scripts";

        /// <summary>二进制数据输出目录。</summary>
        public string OutputDataDir = "Assets/Bundles/Configs";

        /// <summary>调试用 JSON 输出目录；为空则不导出。</summary>
        public string OutputJsonDir = "Config/Luban/Output/json";
    }

    /// <summary>YooAsset Collector 生成规则。</summary>
    [Serializable]
    public sealed class CollectorSettings
    {
        /// <summary>资源包名，须与运行时 <c>ResourceInitOptions.PackageName</c> 一致。</summary>
        public string PackageName = "DefaultPackage";

        /// <summary>收集规则；不存在的目录会跳过。</summary>
        public List<CollectorRuleSetting> Rules = new List<CollectorRuleSetting>
        {
            new CollectorRuleSetting { CollectPath = "Assets/Bundles/Configs", PackMode = CollectorPackMode.PerFile, GroupName = "Configs", SubfoldersPerFolder = true },
            new CollectorRuleSetting { CollectPath = "Assets/Bundles/Prefabs", PackMode = CollectorPackMode.PerFile, GroupName = "Prefabs" },
            new CollectorRuleSetting { CollectPath = "Assets/Bundles/Scenes", PackMode = CollectorPackMode.PerFile, GroupName = "Scenes" },
            new CollectorRuleSetting { CollectPath = "Assets/Bundles/BehaviourTrees", PackMode = CollectorPackMode.PerFile, GroupName = "BehaviourTrees" },
        };
    }

    /// <summary>
    /// 框架编辑器工具的工程级设置，保存在 <c>ProjectSettings/FrameworkSettings.asset</c>（随仓库提交）。
    /// 在 Project Settings → Framework 中编辑。
    /// </summary>
    [FilePath("ProjectSettings/FrameworkSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class FrameworkSettings : ScriptableSingleton<FrameworkSettings>
    {
        /// <summary>Luban 打表设置。</summary>
        public LubanSettings Luban = new LubanSettings();

        /// <summary>YooAsset Collector 设置。</summary>
        public CollectorSettings Collector = new CollectorSettings();

        /// <summary>工程根目录（Assets 的上一级）。</summary>
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        /// <summary>把相对工程根目录的路径转成绝对路径；已是绝对路径时原样返回。</summary>
        /// <param name="path">路径。</param>
        /// <returns>绝对路径。</returns>
        public static string ToAbsolute(string path)
        {
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(ProjectRoot, path));
        }

        /// <summary>写回 <c>ProjectSettings/FrameworkSettings.asset</c>。</summary>
        public void SaveSettings()
        {
            Save(true);
        }

        void OnEnable()
        {
            hideFlags &= ~HideFlags.NotEditable;
        }
    }
}
