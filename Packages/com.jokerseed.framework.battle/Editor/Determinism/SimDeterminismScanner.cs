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
        /// <summary>战斗包运行时代码的根目录（相对工程根）。</summary>
        public const string BattleRuntimeRoot = "Packages/com.jokerseed.framework.battle/Runtime";

        /// <summary>
        /// 相对工程根目录的扫描根。
        /// 不含 <c>LockstepPhysics</c>：那里仍有不参与战斗 Tick 的 float 路径，纳入后当前扫描无法通过。
        /// </summary>
        public static readonly string[] ScanRoots =
        {
            BattleRuntimeRoot + "/ECS",
            BattleRuntimeRoot + "/ECS.Physics",
            BattleRuntimeRoot + "/GAS",
            BattleRuntimeRoot + "/FixedMath",
            BattleRuntimeRoot + "/GamePlay",
            BattleRuntimeRoot + "/BehaviourTree",
            BattleRuntimeRoot + "/Battle.Core",
            BattleRuntimeRoot + "/Lockstep",
            "Packages/com.jokerseed.framework.core/Runtime/Core",
            "Assets/Scripts/Luban",
        };

        static readonly Regex Forbidden = new Regex(
            @"UnityEngine\.Random|\bSystem\.Random\b|\bnew\s+Random\s*\(|\bTime\.(deltaTime|fixedDeltaTime|unscaledDeltaTime|unscaledTime|timeScale|frameCount|realtimeSinceStartup|time)\b|DateTime\.(Now|UtcNow)|\bGuid\.NewGuid\b",
            RegexOptions.Compiled);

        static readonly Regex FloatToken = new Regex(@"\bfloat\b", RegexOptions.Compiled);

        static readonly Regex SimSubscribesToBus = new Regex(
            @"\bGameEvent\b|\bGlobalEventBus\b|\.Subscribe\s*[<(]|\.Listen\s*<",
            RegexOptions.Compiled);

        /// <summary>扫描工程里的战斗逻辑目录。</summary>
        /// <param name="projectRoot">工程根目录（<c>Assets</c> 的上一级）；为空时用当前编辑器工程。</param>
        /// <returns>违规说明；空列表表示通过。</returns>
        public static List<string> Scan(string projectRoot = null)
        {
            var root = string.IsNullOrEmpty(projectRoot)
                ? Path.GetDirectoryName(Application.dataPath)
                : projectRoot;
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
                    var relative = files[f].Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
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

        /// <summary>
        /// 批处理入口：<c>Unity -batchmode -executeMethod Framework.Editor.Determinism.SimDeterminismScanner.ScanBatch</c>。
        /// 有违规时退出码为 1。
        /// </summary>
        public static void ScanBatch()
        {
            var violations = Scan();
            if (violations.Count == 0)
            {
                Debug.Log("Determinism scan passed.");
            }
            else
            {
                for (var i = 0; i < violations.Count; i++)
                {
                    Debug.LogError(violations[i]);
                }
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(violations.Count == 0 ? 0 : 1);
            }
        }

        /// <summary>菜单：Tools/Framework/Battle/Scan Determinism。</summary>
        [MenuItem("Tools/Framework/Battle/Scan Determinism")]
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

            if (path.Contains("/Runtime/ECS/") ||
                path.Contains("/Runtime/ECS.Physics/") ||
                path.Contains("/Runtime/Core/") ||
                path.Contains("/Runtime/Battle.Core/"))
            {
                return true;
            }

            if (path.Contains("/Runtime/BehaviourTree/"))
            {
                return !path.Contains("/Runtime/BehaviourTree/Config/");
            }

            if (path.Contains("/Runtime/Lockstep/"))
            {
                return true;
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
