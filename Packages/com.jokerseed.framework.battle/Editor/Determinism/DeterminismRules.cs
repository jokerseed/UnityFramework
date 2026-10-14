using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Framework.Editor.Determinism
{
    /// <summary>
    /// 确定性扫描规则本体，不依赖 UnityEngine / UnityEditor，Unity 菜单（<see cref="SimDeterminismScanner"/>）和纯 .NET CI 测试共用这一份。
    /// 错误级：<c>UnityEngine.Random</c>、<c>System.Random</c>、<c>Time.*</c>、<c>DateTime.Now</c>、<c>Guid.NewGuid</c>，
    /// 以及 ECS、Battle.Core、BehaviourTree 运行时、Lockstep 模拟代码里的 <c>float</c>（BehaviourTree 配置 DTO 不阻断）。
    /// GAS / GamePlay 的 float 留在配置装配和表现边界，不算违规。
    /// 模拟目录里出现 <c>GameEvent</c>、<c>GlobalEventBus</c>、<c>.Subscribe</c> 或 <c>.Listen&lt;</c> 也算错误：
    /// 模拟只发布命令和表现事件，不订阅总线。实现 <c>IEventBus.Subscribe</c> 的方法声明不匹配（前面没有点号）。
    /// 行尾写 <c>determinism-allow</c> 可跳过该行。Editor 目录不扫描。
    /// </summary>
    public static class DeterminismRules
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
        /// <param name="projectRoot">工程根目录（<c>Assets</c> 的上一级）。</param>
        /// <returns>违规说明；空列表表示通过。</returns>
        public static List<string> Scan(string projectRoot)
        {
            var violations = new List<string>();
            for (var i = 0; i < ScanRoots.Length; i++)
            {
                var directory = Path.Combine(projectRoot, ScanRoots[i].Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                var files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
                for (var f = 0; f < files.Length; f++)
                {
                    var relative = files[f].Substring(projectRoot.Length).TrimStart('\\', '/').Replace('\\', '/');
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

                if (checkFloat && FloatToken.IsMatch(code))
                {
                    violations.Add(relativePath + ":" + (i + 1) + " float in sim: " + code.Trim());
                }
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

        static string StripComments(string line, ref bool inBlock)
        {
            if (line == null)
            {
                return string.Empty;
            }

            var result = new StringBuilder(line.Length);
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
