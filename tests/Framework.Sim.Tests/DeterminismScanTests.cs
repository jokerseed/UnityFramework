using System;
using System.Collections.Generic;
using System.IO;
using Framework.Editor.Determinism;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    /// <summary>在 CI 里跑与 Unity 菜单 Tools/Framework/Battle/Scan Determinism 相同的规则。</summary>
    public sealed class DeterminismScanTests
    {
        [Test]
        public void SimulationSources_HaveNoNonDeterministicApis()
        {
            var root = FindRepoRoot();
            var violations = DeterminismRules.Scan(root);
            Assert.IsEmpty(violations, string.Join(Environment.NewLine, violations));
        }

        [Test]
        public void Rules_FlagFloatInBattleCore()
        {
            var violations = new List<string>();
            DeterminismRules.CollectViolations(
                DeterminismRules.BattleRuntimeRoot + "/Battle.Core/X.cs",
                "class X { float speed; }",
                violations);
            Assert.AreEqual(1, violations.Count);
        }

        [Test]
        public void Rules_FlagDoubleAndFloatingMathInBattleCore()
        {
            var violations = new List<string>();
            DeterminismRules.CollectViolations(
                DeterminismRules.BattleRuntimeRoot + "/Battle.Core/X.cs",
                "double x = Math.Sin(1);",
                violations);
            Assert.GreaterOrEqual(violations.Count, 1);
        }

        [Test]
        public void Rules_FlagDecimalInGasButAllowPresentationFloat()
        {
            var scalar = new List<string>();
            DeterminismRules.CollectViolations(
                DeterminismRules.BattleRuntimeRoot + "/GAS/X.cs",
                "decimal x = 1m;",
                scalar);
            Assert.AreEqual(1, scalar.Count);

            var floats = new List<string>();
            DeterminismRules.CollectViolations(
                DeterminismRules.BattleRuntimeRoot + "/GAS/X.cs",
                "float speed = 1f;",
                floats);
            Assert.IsEmpty(floats);
        }

        [Test]
        public void Rules_IgnoreCommentsAndAllowMarker()
        {
            var violations = new List<string>();
            DeterminismRules.CollectViolations(
                DeterminismRules.BattleRuntimeRoot + "/Battle.Core/X.cs",
                "// float a;\n/* System.Random */\nvar t = DateTime.Now; // determinism-allow",
                violations);
            Assert.IsEmpty(violations);
        }

        [Test]
        public void Rules_FlagFloatLiteralButAllowConstantFpCast()
        {
            var literal = new List<string>();
            DeterminismRules.CollectViolations(DeterminismRules.BattleRuntimeRoot + "/ECS/X.cs", "var half = 0.5f * size;", literal);
            Assert.AreEqual(1, literal.Count);

            var cast = new List<string>();
            DeterminismRules.CollectViolations(
                DeterminismRules.BattleRuntimeRoot + "/ECS/X.cs",
                "var half = (FP)0.5f * size; var slop = (FP)(2.0f / 180.0f); var mask = 0x1F;",
                cast);
            Assert.IsEmpty(cast);
        }

        [Test]
        public void Rules_FlagTimersAndSinglePrecisionMathEverywhere()
        {
            var violations = new List<string>();
            DeterminismRules.CollectViolations(
                DeterminismRules.BattleRuntimeRoot + "/GAS/X.cs",
                "var a = Environment.TickCount;\nvar b = Stopwatch.GetTimestamp();\nvar c = MathF.Sqrt(x);\nvar s = \"Stopwatch 1.5\";",
                violations);
            Assert.AreEqual(3, violations.Count, string.Join("\n", violations));
        }

        [Test]
        public void Rules_FlagFloatParameterInGasButAllowFloatLocals()
        {
            var parameter = new List<string>();
            DeterminismRules.CollectViolations(
                DeterminismRules.BattleRuntimeRoot + "/GamePlay/X.cs",
                "public Foo(string id, float cooldown)\nvoid Bar(\n    float speed,\n    int team)",
                parameter);
            Assert.AreEqual(2, parameter.Count, string.Join("\n", parameter));

            var local = new List<string>();
            DeterminismRules.CollectViolations(
                DeterminismRules.BattleRuntimeRoot + "/GamePlay/X.cs",
                "float alpha = 1f;\npublic float InterpolationAlpha => 0f;\npublic void Tick(float dt) // determinism-allow: 渲染帧入口",
                local);
            Assert.IsEmpty(local, string.Join("\n", local));
        }

        [Test]
        public void Rules_FlagDoubleLiteralInFixedMath()
        {
            var violations = new List<string>();
            DeterminismRules.CollectViolations(DeterminismRules.BattleRuntimeRoot + "/FixedMath/X.cs", "var x = (FP)(0.5) * y;", violations);
            Assert.AreEqual(1, violations.Count);
        }

        [Test]
        public void Rules_ScanFarseerSolverButSkipTools()
        {
            var solver = new List<string>();
            DeterminismRules.CollectViolations(
                DeterminismRules.BattleRuntimeRoot + "/LockstepPhysics/Farseer/Dynamics/World.cs",
                "float dt = step.dt;",
                solver);
            Assert.AreEqual(1, solver.Count);

            var tool = new List<string>();
            DeterminismRules.CollectViolations(
                DeterminismRules.BattleRuntimeRoot + "/LockstepPhysics/Farseer/Common/Decomposition/X.cs",
                "float dt = step.dt; var t = Environment.TickCount;",
                tool);
            Assert.IsEmpty(tool);
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
