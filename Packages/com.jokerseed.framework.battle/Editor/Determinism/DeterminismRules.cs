using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Framework.Editor.Determinism
{
    /// <summary>
    /// 确定性扫描规则本体，不依赖 UnityEngine / UnityEditor，Unity 菜单（<see cref="SimDeterminismScanner"/>）和纯 .NET CI 测试共用这一份。
    /// 错误级（所有扫描目录）：<c>UnityEngine.Random</c>、<c>System.Random</c>、<c>Time.*</c>、<c>DateTime.Now/UtcNow/Today</c>、
    /// <c>Guid.NewGuid</c>、<c>Environment.TickCount</c>、<c>Stopwatch</c>。
    /// ECS、Battle.Core、BehaviourTree 运行时、Lockstep 模拟和 Farseer 求解代码里：<c>float</c>、<c>Single</c>、<c>MathF</c> 和 float 字面量（如 <c>0.5f</c>）都算违规；
    /// 直接转成 FP 的常量（<c>(FP)0.5f</c>）除外，那是精确的编译期常量换算
    /// （BehaviourTree 配置 DTO 不查；<see cref="FarseerToolPaths"/> 里不参与战斗帧的 Farseer 工具整个不扫描）。
    /// 上述目录以及 GAS、GamePlay、FixedMath 还拦截 <c>double</c>、<c>decimal</c>、<c>Mathf</c>、浮点 <c>Math.*</c> 和小数字面量（如 <c>1.5</c>）。
    /// GAS / GamePlay 允许 float 局部变量和字段（表现边界），但方法形参不能是 float：配置值应在装配时经 <c>FixedConfig.FromConfig</c> 转成 FP 再传进来。
    /// 整数 <c>Math.Max</c> / <c>Min</c> / <c>Abs</c> 不拦截。
    /// 模拟目录里出现 <c>GameEvent</c>、<c>GlobalEventBus</c>、<c>.Subscribe</c> 或 <c>.Listen&lt;</c> 也算错误：
    /// 模拟只发布命令和表现事件，不订阅总线。实现 <c>IEventBus.Subscribe</c> 的方法声明不匹配（前面没有点号）。
    /// 行尾写 <c>determinism-allow</c> 可跳过该行（用于配置边界、渲染帧入口、查找表生成等）。Editor 目录不扫描。
    /// 这是基于正则的逐行检查，挡不住类型推断出来的 float（如 <c>var x = someFloat;</c>）；更彻底的做法是 Roslyn 分析器，列在 README 的后续工作里。
    /// </summary>
    public static class DeterminismRules
    {
        /// <summary>战斗包运行时代码的根目录（相对工程根）。</summary>
        public const string BattleRuntimeRoot = "Packages/com.jokerseed.framework.battle/Runtime";

        /// <summary>相对工程根目录的扫描根。</summary>
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
            BattleRuntimeRoot + "/LockstepPhysics/Farseer",
            "Assets/Scripts/Luban",
        };

        /// <summary>
        /// Farseer 里不参与战斗帧的部分（多边形分解、纹理工具、路径、关节、调试视图、性能计时等），整个文件不扫描。
        /// 战斗只用圆形刚体、宽相、接触和求解器，这些文件不会在逻辑帧里执行。路径前缀相对 Farseer 目录。
        /// </summary>
        public static readonly string[] FarseerToolPaths =
        {
            "Common/ConvexHull/",
            "Common/Decomposition/",
            "Common/PhysicsLogic/",
            "Common/PolygonManipulation/",
            "Common/TextureTools/",
            "Common/Curve.cs",
            "Common/CurveKey.cs",
            "Common/CurveKeyCollection.cs",
            "Common/Path.cs",
            "Common/PathManager.cs",
            "Common/Serialization.cs",
            "Common/Stopwatch.cs",
            "Controllers/",
            "Dynamics/Joints/",
            "Dynamics/BreakableBody.cs",
            "Factories/",
            "ConvertUnits.cs",
            "DebugViewBase.cs",
        };

        const string FarseerRoot = "/Runtime/LockstepPhysics/Farseer/";

        static readonly Regex Forbidden = new Regex(
            @"UnityEngine\.Random|\bSystem\.Random\b|\bnew\s+Random\s*\(|\bTime\.(deltaTime|fixedDeltaTime|unscaledDeltaTime|unscaledTime|timeScale|frameCount|realtimeSinceStartup|time)\b|DateTime\.(Now|UtcNow|Today)|\bGuid\.NewGuid\b|\bEnvironment\.TickCount(64)?\b|\bStopwatch\b",
            RegexOptions.Compiled);

        /// <summary>float 类型与单精度数学。</summary>
        static readonly Regex FloatToken = new Regex(@"\bfloat\b|\bSingle\b|\bMathF\.", RegexOptions.Compiled);

        /// <summary>float 字面量：0.5f、2f、.5F、1e-7f。十六进制（0x1F）前面是字母，不会匹配。</summary>
        static readonly Regex FloatLiteral = new Regex(@"(?<![\w.])(?:\d+\.\d*|\.\d+|\d+)(?:[eE][-+]?\d+)?[fF](?!\w)", RegexOptions.Compiled);

        /// <summary>
        /// 直接转成 FP 的常量 float 表达式，如 <c>(FP)0.5f</c>、<c>(FP)(2.0f / 180.0f)</c>。
        /// 字面量的位模式是固定的，乘 2^32 再截断也是精确运算，各平台结果一致，所以不算违规；先从行里去掉再查 float 字面量。
        /// </summary>
        static readonly Regex FpCastOfConstant = new Regex(
            @"\(FP\)\s*(?:\(\s*[-+]?\s*(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?[fF]?(?:\s*[-+*/]\s*(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?[fF]?)*\s*\)|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?[fF])",
            RegexOptions.Compiled);

        /// <summary>浮点标量、浮点数学与小数字面量（默认是 double）。整数 Math.Max / Min / Abs 不算。</summary>
        static readonly Regex ScalarLeak = new Regex(
            @"\bdouble\b|\bdecimal\b|\bMathf\.|\bMathF\.|\bMath\.(Sin|Cos|Tan|Asin|Acos|Atan|Atan2|Sinh|Cosh|Tanh|Pow|Sqrt|Log|Log10|Exp|Round|Floor|Ceiling|IEEERemainder)\b|(?<![\w.])\d+\.\d+(?:[dDmM])?(?![\w.])",
            RegexOptions.Compiled);

        /// <summary>float 形参：<c>(float x</c>、<c>, float x</c>，以及多行形参列表里单独一行的 <c>float x,</c>。</summary>
        static readonly Regex FloatParameter = new Regex(
            @"[(,]\s*float\s+\w+|^\s*float\s+\w+\s*(?:=[^,;)]*)?[,)]",
            RegexOptions.Compiled);

        static readonly Regex SimSubscribesToBus = new Regex(
            @"\bGameEvent\b|\bGlobalEventBus\b|\.Subscribe\s*[<(]|\.Listen\s*<",
            RegexOptions.Compiled);

        // 字符串字面量的内容不参与匹配，避免 "1.5"、"Stopwatch" 之类的文案误报。
        static readonly Regex StringLiteral = new Regex(@"@?""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)'", RegexOptions.Compiled);

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
                System.Array.Sort(files, System.StringComparer.Ordinal);
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
            if (string.IsNullOrEmpty(source) || violations == null || IsFarseerTool(Normalize(relativePath)))
            {
                return;
            }

            var checkFloat = BlocksFloat(relativePath);
            var checkScalar = checkFloat || BlocksScalar(relativePath);
            var checkFloatParameter = BlocksFloatParameter(relativePath);
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

                var bare = StringLiteral.Replace(code, "\"\"");
                if (Forbidden.IsMatch(bare))
                {
                    violations.Add(relativePath + ":" + (i + 1) + " " + code.Trim());
                    continue;
                }

                if (SimSubscribesToBus.IsMatch(code))
                {
                    violations.Add(relativePath + ":" + (i + 1) + " sim must not subscribe: " + code.Trim());
                    continue;
                }

                if (checkFloat && (FloatToken.IsMatch(bare) || FloatLiteral.IsMatch(FpCastOfConstant.Replace(bare, "FP"))))
                {
                    violations.Add(relativePath + ":" + (i + 1) + " float in sim: " + code.Trim());
                    continue;
                }

                if (checkScalar && ScalarLeak.IsMatch(bare))
                {
                    violations.Add(relativePath + ":" + (i + 1) + " scalar in sim: " + code.Trim());
                    continue;
                }

                if (checkFloatParameter && FloatParameter.IsMatch(bare))
                {
                    violations.Add(relativePath + ":" + (i + 1) + " float parameter in sim (convert with FixedConfig.FromConfig at the boundary): " + code.Trim());
                }
            }
        }

        static string Normalize(string relativePath)
        {
            return string.IsNullOrEmpty(relativePath) ? string.Empty : relativePath.Replace('\\', '/');
        }

        static bool IsFarseerTool(string path)
        {
            var at = path.IndexOf(FarseerRoot, System.StringComparison.Ordinal);
            if (at < 0)
            {
                return false;
            }

            var inner = path.Substring(at + FarseerRoot.Length);
            for (var i = 0; i < FarseerToolPaths.Length; i++)
            {
                if (inner.StartsWith(FarseerToolPaths[i], System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        static bool BlocksFloat(string relativePath)
        {
            var path = Normalize(relativePath);
            if (path.Length == 0 || path.Contains("/Editor/"))
            {
                return false;
            }

            if (path.Contains("/Runtime/ECS/") ||
                path.Contains("/Runtime/ECS.Physics/") ||
                path.Contains("/Runtime/Battle.Core/") ||
                path.Contains("/Runtime/Lockstep/"))
            {
                return true;
            }

            if (path.Contains("/Runtime/BehaviourTree/"))
            {
                return !path.Contains("/Runtime/BehaviourTree/Config/");
            }

            if (path.Contains(FarseerRoot))
            {
                return !IsFarseerTool(path);
            }

            return false;
        }

        /// <summary>GAS、GamePlay 和 FixedMath 允许 float 出现在边界上，但不允许 double / decimal / 浮点 Math。</summary>
        static bool BlocksScalar(string relativePath)
        {
            var path = Normalize(relativePath);
            if (path.Length == 0 || path.Contains("/Editor/"))
            {
                return false;
            }

            return path.Contains("/Runtime/GAS/") ||
                   path.Contains("/Runtime/GamePlay/") ||
                   path.Contains("/Runtime/FixedMath/");
        }

        /// <summary>GAS 与 GamePlay 的方法形参不能是 float。</summary>
        static bool BlocksFloatParameter(string relativePath)
        {
            var path = Normalize(relativePath);
            if (path.Length == 0 || path.Contains("/Editor/"))
            {
                return false;
            }

            return path.Contains("/Runtime/GAS/") || path.Contains("/Runtime/GamePlay/");
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
