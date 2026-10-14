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
        public void Rules_IgnoreCommentsAndAllowMarker()
        {
            var violations = new List<string>();
            DeterminismRules.CollectViolations(
                DeterminismRules.BattleRuntimeRoot + "/Battle.Core/X.cs",
                "// float a;\n/* System.Random */\nvar t = DateTime.Now; // determinism-allow",
                violations);
            Assert.IsEmpty(violations);
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
