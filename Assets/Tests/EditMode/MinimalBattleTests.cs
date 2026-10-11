using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>最小战斗跑两遍，逐步校验和必须相同，并且双方都掉了血。</summary>
    public sealed class MinimalBattleTests
    {
        [Test]
        public void TwoRunsMatchPerStepChecksum()
        {
            var first = Framework.GamePlay.MinimalBattle.Run();
            var second = Framework.GamePlay.MinimalBattle.Run();

            Assert.AreEqual(Framework.GamePlay.MinimalBattle.DefaultSteps, first.Checksums.Length);
            Assert.AreEqual(first.Checksums.Length, second.Checksums.Length);
            for (var i = 0; i < first.Checksums.Length; i++)
            {
                Assert.AreEqual(first.Checksums[i], second.Checksums[i], "step " + i);
            }

            Assert.AreNotEqual(first.Checksums[0], first.Checksums[first.Checksums.Length - 1]);
            Assert.Less(first.HeroHealthRaw, first.HeroHealthAtStartRaw);
            Assert.Less(first.EnemyHealthRaw, first.EnemyHealthAtStartRaw);
            Assert.AreEqual(first.HeroHealthRaw, second.HeroHealthRaw);
            Assert.AreEqual(first.EnemyHealthRaw, second.EnemyHealthRaw);
        }
    }
}
