using System.Collections.Generic;
using System.IO;
using Framework.Editor;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>已提交的 Luban 生成代码必须符合 codegen.json 的前缀规则（CI 不跑打表，只校验结果）。</summary>
    public sealed class LubanCodegenPrefixTests
    {
        [Test]
        public void CommittedGeneratedCodeHasPrefix()
        {
            var settings = FrameworkSettings.instance.Luban;
            Assert.DoesNotThrow(() => LubanCodegenValidator.Validate(
                FrameworkSettings.ToAbsolute(settings.CodegenRules),
                FrameworkSettings.ToAbsolute(settings.OutputCodeDir)));
        }

        [Test]
        public void OffendersExcludeExemptAndPrefixed()
        {
            var dir = Path.Combine(Path.GetTempPath(), "luban_prefix_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "CfgAbility.cs"), string.Empty);
                File.WriteAllText(Path.Combine(dir, "Legacy.cs"), string.Empty);
                File.WriteAllText(Path.Combine(dir, "Ability.cs"), string.Empty);
                var offenders = LubanCodegenValidator.FindOffenders(dir, "Cfg", new HashSet<string> { "Legacy" });
                CollectionAssert.AreEqual(new[] { "Ability.cs" }, offenders);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
