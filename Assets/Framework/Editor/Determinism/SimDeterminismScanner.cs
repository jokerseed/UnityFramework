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
    /// 以及 ECS、Core、BehaviourTree 运行时、Lockstep 模拟代码里的 <c>float</c>。
    /// <c>ITickable.Tick(float)</c> 桥、BehaviourTree 配置 DTO、Lockstep 的调试绘制/统计文件不阻断。
    /// GAS / GamePlay 的 float 留在配置装配和表现边界，扫描器不把它们放进返回列表。
    /// 模拟目录里出现 <c>GameEvent</c>、<c>GlobalEventBus</c>、<c>.Subscribe</c> 或 <c>.Listen&lt;</c> 也算错误：
    /// 模拟只发布命令和表现事件，不订阅总线。实现 <c>IEventBus.Subscribe</c> 的方法声明不匹配（前面没有点号）。
    /// 行尾写 <c>determinism-allow</c> 可跳过该行。Editor 目录不扫描。
    /// </summary>
    public static class SimDeterminismScanner
    {
        static readonly string[] ScanRoots =
        {
            "Framework/ECS",
            "Framework/GAS",
            "Framework/FixedMath",
            "Framework/GamePlay",
            "Framework/BehaviourTree",
            "Framework/Core",
            "Framework/Lockstep",
        };

        static readonly Regex Forbidden = new Regex(
            @"UnityEngine\.Random|\bSystem\.Random\b|\bnew\s+Random\s*\(|\bTime\.(deltaTime|fixedDeltaTime|unscaledDeltaTime|unscaledTime|timeScale|frameCount|realtimeSinceStartup|time)\b|DateTime\.(Now|UtcNow)|\bGuid\.NewGuid\b",
            RegexOptions.Compiled);

        static readonly Regex FloatToken = new Regex(@"\bfloat\b", RegexOptions.Compiled);

        static readonly Regex SimSubscribesToBus = new Regex(
            @"\bGameEvent\b|\bGlobalEventBus\b|\.Subscribe\s*[<(]|\.Listen\s*<",
            RegexOptions.Compiled);

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
                if (relative.Contains("/Editor/"))
                {
                    continue;
                }

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

            var checkFloat = BlocksFloat(relativePath);
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

                if (SimSubscribesToBus.IsMatch(code))
                {
                    violations.Add(relativePath + ":" + (i + 1) + " sim must not subscribe: " + code.Trim());
                    continue;
                }

                if (checkFloat && FloatToken.IsMatch(code) && !IsFloatBridge(code))
                {
                    violations.Add(relativePath + ":" + (i + 1) + " float in sim: " + code.Trim());
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

        static bool BlocksFloat(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return false;
            }

            var path = relativePath.Replace('\\', '/');
            if (path.Contains("/Editor/"))
            {
                return false;
            }

            if (path.Contains("Framework/ECS") || path.Contains("Framework/Core"))
            {
                return true;
            }

            if (path.Contains("Framework/BehaviourTree"))
            {
                return !path.Contains("Framework/BehaviourTree/Config/");
            }

            if (path.Contains("Framework/Lockstep"))
            {
                return !path.EndsWith("TrueSyncStats.cs", System.StringComparison.Ordinal)
                    && !path.EndsWith("Drawing.cs", System.StringComparison.Ordinal)
                    && !path.EndsWith("CountInfo.cs", System.StringComparison.Ordinal)
                    && !path.EndsWith("CompoundStats.cs", System.StringComparison.Ordinal);
            }

            return false;
        }

        static bool IsFloatBridge(string code)
        {
            return code.IndexOf("Tick(float", System.StringComparison.Ordinal) >= 0;
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
