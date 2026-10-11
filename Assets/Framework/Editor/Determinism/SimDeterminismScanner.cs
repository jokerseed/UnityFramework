using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Framework.Logging;
using UnityEditor;
using UnityEngine;

namespace Framework.Editor.Determinism
{
    /// <summary>
    /// 扫描战斗逻辑程序集，标出容易破坏确定性的 API。
    /// 错误级：<c>UnityEngine.Random</c>、<c>System.Random</c>、<c>Time.*</c>、<c>DateTime.Now</c>、<c>Guid.NewGuid</c>，
    /// 以及 ECS 里新出现的 <c>float</c>（<c>ITickable.Tick(float)</c> 桥除外）。
    /// GAS / GamePlay 的 float 留在配置装配和表现边界，扫描器只记录、不阻断。
    /// 行尾写 <c>determinism-allow</c> 可跳过该行。
    /// </summary>
    public static class SimDeterminismScanner
    {
        static readonly string[] ScanRoots =
        {
            "Framework/ECS",
            "Framework/GAS",
            "Framework/FixedMath",
            "Framework/GamePlay",
        };

        static readonly Regex Forbidden = new Regex(
            @"UnityEngine\.Random|\bSystem\.Random\b|\bnew\s+Random\s*\(|\bTime\.(deltaTime|fixedDeltaTime|unscaledDeltaTime|unscaledTime|timeScale|frameCount|realtimeSinceStartup|time)\b|DateTime\.(Now|UtcNow)|\bGuid\.NewGuid\b",
            RegexOptions.Compiled);

        static readonly Regex FloatToken = new Regex(@"\bfloat\b", RegexOptions.Compiled);

        /// <summary>扫描 Assets 下的战斗逻辑目录。</summary>
        /// <param name="assetsPath"><c>Application.dataPath</c>；为空时用当前编辑器路径。</param>
        /// <returns>违规说明；空列表表示通过。</returns>
        public static List<string> Scan(string assetsPath = null)
        {
            var root = string.IsNullOrEmpty(assetsPath) ? Application.dataPath : assetsPath;
            var violations = new List<string>();
            for (var i = 0; i < ScanRoots.Length; i++)
            {
                var directory = Path.Combine(root, ScanRoots[i].Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                var files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
                for (var f = 0; f < files.Length; f++)
                {
                    var relative = files[f].Replace('\\', '/');
                    var source = File.ReadAllText(files[f]);
                    CollectViolations(relative, source, violations);
                }
            }

            return violations;
        }

        /// <summary>对一段源码收集违规，供编辑器菜单和测试复用。</summary>
        /// <param name="relativePath">显示用路径。</param>
        /// <param name="source">C# 源码。</param>
        /// <param name="violations">追加违规说明。</param>
        public static void CollectViolations(string relativePath, string source, List<string> violations)
        {
            if (string.IsNullOrEmpty(source) || violations == null)
            {
                return;
            }

            var checkFloat = relativePath != null &&
                             relativePath.Replace('\\', '/').Contains("Framework/ECS");
            var inBlock = false;
            var lines = source.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("determinism-allow"))
                {
                    StripComments(lines[i], ref inBlock);
                    continue;
                }

                var code = StripComments(lines[i], ref inBlock);
                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }

                if (Forbidden.IsMatch(code))
                {
                    violations.Add(relativePath + ":" + (i + 1) + " " + code.Trim());
                    continue;
                }

                if (checkFloat && FloatToken.IsMatch(code) && code.IndexOf("Tick(float", System.StringComparison.Ordinal) < 0)
                {
                    violations.Add(relativePath + ":" + (i + 1) + " float in ECS: " + code.Trim());
                }
            }
        }

        /// <summary>菜单：Tools/Determinism/Scan Battle Sim。</summary>
        [MenuItem("Tools/Determinism/Scan Battle Sim")]
        public static void ScanFromMenu()
        {
            var violations = Scan();
            if (violations.Count == 0)
            {
                GameLog.Info(LogCategories.GamePlay, "Determinism scan passed.");
                return;
            }

            for (var i = 0; i < violations.Count; i++)
            {
                GameLog.Error(LogCategories.GamePlay, violations[i]);
            }
        }

        static string StripComments(string line, ref bool inBlock)
        {
            if (line == null)
            {
                return string.Empty;
            }

            var result = new System.Text.StringBuilder(line.Length);
            var inString = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (inBlock)
                {
                    if (c == '*' && i + 1 < line.Length && line[i + 1] == '/')
                    {
                        inBlock = false;
                        i++;
                    }

                    continue;
                }

                if (c == '"' && (i == 0 || line[i - 1] != '\\'))
                {
                    inString = !inString;
                    result.Append(c);
                    continue;
                }

                if (!inString && c == '/' && i + 1 < line.Length)
                {
                    if (line[i + 1] == '/')
                    {
                        break;
                    }

                    if (line[i + 1] == '*')
                    {
                        inBlock = true;
                        i++;
                        continue;
                    }
                }

                result.Append(c);
            }

            return result.ToString();
        }
    }
}
