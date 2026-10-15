using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    /// <summary>仓库布局和文档链接。不依赖 Unity。</summary>
    public sealed class RepositoryGuardTests
    {
        static readonly Regex MarkdownLink = new Regex(@"\[[^\]]*\]\(([^)\s]+)\)", RegexOptions.Compiled);

        [Test]
        public void KcpSourcesLiveInsideNetworkPackage()
        {
            var root = FindRepoRoot();
            var asmdef = Path.Combine(
                root,
                "Packages",
                "com.jokerseed.framework.network",
                "Runtime",
                "ThirdParty",
                "KCP",
                "KCP.asmdef");
            Assert.IsTrue(File.Exists(asmdef), asmdef);
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "Assets", "ThirdParty", "KCP")));
        }

        [Test]
        public void ReadmeDocumentsYooAssetInstall()
        {
            var readme = File.ReadAllText(Path.Combine(FindRepoRoot(), "README.md"));
            StringAssert.Contains("package.openupm.com", readme);
            StringAssert.Contains("com.tuyoogame", readme);
            StringAssert.Contains("YooAsset.git?path=Assets/YooAsset#3.0.5", readme);
        }

        [Test]
        public void RelativeMarkdownLinksResolve()
        {
            var root = FindRepoRoot();
            var files = new List<string>();
            AddIfExists(files, Path.Combine(root, "README.md"));
            AddIfExists(files, Path.Combine(root, "README.en.md"));
            AddIfExists(files, Path.Combine(root, "CHANGELOG.md"));
            AddIfExists(files, Path.Combine(root, "CONTRIBUTING.md"));
            AddIfExists(files, Path.Combine(root, "THIRD_PARTY_NOTICES.md"));
            AddMarkdownFiles(files, Path.Combine(root, "docs"));
            AddMarkdownFiles(files, Path.Combine(root, "Assets", "Scripts"));
            foreach (var package in Directory.GetDirectories(Path.Combine(root, "Packages"), "com.jokerseed.framework.*"))
            {
                AddMarkdownFiles(files, package);
            }

            var missing = new List<string>();
            for (var i = 0; i < files.Count; i++)
            {
                CollectMissingLinks(root, files[i], missing);
            }

            Assert.IsEmpty(missing, string.Join(Environment.NewLine, missing));
        }

        static void CollectMissingLinks(string repoRoot, string markdownPath, List<string> missing)
        {
            var lines = File.ReadAllLines(markdownPath);
            var inFence = false;
            var directory = Path.GetDirectoryName(markdownPath);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    inFence = !inFence;
                    continue;
                }

                if (inFence)
                {
                    continue;
                }

                foreach (Match match in MarkdownLink.Matches(line))
                {
                    var target = match.Groups[1].Value;
                    if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                        || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                        || target.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var hash = target.IndexOf('#');
                    if (hash >= 0)
                    {
                        target = target.Substring(0, hash);
                    }

                    if (target.Length == 0)
                    {
                        continue;
                    }

                    target = Uri.UnescapeDataString(target).Replace('/', Path.DirectorySeparatorChar);
                    var full = Path.GetFullPath(Path.Combine(directory, target));
                    if (!File.Exists(full) && !Directory.Exists(full))
                    {
                        var relative = markdownPath.Substring(repoRoot.Length).TrimStart('\\', '/');
                        missing.Add(relative + ":" + (i + 1) + " -> " + match.Groups[1].Value);
                    }
                }
            }
        }

        static void AddMarkdownFiles(List<string> files, string directory)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            files.AddRange(Directory.GetFiles(directory, "*.md", SearchOption.AllDirectories));
        }

        static void AddIfExists(List<string> files, string path)
        {
            if (File.Exists(path))
            {
                files.Add(path);
            }
        }

        static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Packages", "com.jokerseed.framework.battle")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "repo root not found");
            return dir.FullName;
        }
    }
}
