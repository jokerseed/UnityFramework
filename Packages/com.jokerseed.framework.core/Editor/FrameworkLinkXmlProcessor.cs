using System;
using System.IO;
using System.Xml.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Framework.Editor
{
    /// <summary>
    /// 构建时把框架各包里的 <c>link.xml</c> 交给 UnityLinker。
    /// Unity 只读取 Assets 下的 link.xml，包里的会被忽略（官方 Issue 标为 By Design）。
    /// 这里枚举所有 <c>com.jokerseed.framework.*</c> 包，把它们的 link.xml 合并成一份写到 <c>Library/Framework/link.xml</c>，
    /// 再通过 <see cref="IUnityLinkerProcessor.GenerateAdditionalLinkXmlFile"/> 返回。包里的 link.xml 仍是唯一的维护位置。
    /// </summary>
    sealed class FrameworkLinkXmlProcessor : IUnityLinkerProcessor
    {
        const string PackagePrefix = "com.jokerseed.framework.";

        /// <inheritdoc />
        public int callbackOrder => 0;

        /// <inheritdoc />
        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            var merged = new XElement("linker");
            foreach (var package in PackageInfo.GetAllRegisteredPackages())
            {
                if (package == null || !package.name.StartsWith(PackagePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var path = Path.Combine(package.resolvedPath, "link.xml");
                if (!File.Exists(path))
                {
                    continue;
                }

                var root = XDocument.Load(path).Root;
                if (root == null)
                {
                    continue;
                }

                merged.Add(new XComment(" " + package.name + " "));
                foreach (var element in root.Elements())
                {
                    merged.Add(new XElement(element));
                }
            }

            var directory = Path.Combine(Path.GetFullPath("Library"), "Framework");
            Directory.CreateDirectory(directory);
            var output = Path.Combine(directory, "link.xml");
            new XDocument(merged).Save(output);
            return output;
        }
    }
}
