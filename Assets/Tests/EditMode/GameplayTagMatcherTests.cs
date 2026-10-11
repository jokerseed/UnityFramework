using Framework.Core;
using Framework.GAS;
using Framework.GAS.Tags;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>标签匹配不拼接字符串，控制状态缓存与 HasTag 一致。</summary>
    public sealed class GameplayTagMatcherTests
    {
        [Test]
        public void PrefixMatchDoesNotRequireConcatenation()
        {
            Assert.IsTrue(GameplayTagMatcher.MatchesName("State.CrowdControl.Stunned", "State.CrowdControl"));
            Assert.IsTrue(GameplayTagMatcher.MatchesName("State.Dead", "State.Dead"));
            Assert.IsFalse(GameplayTagMatcher.MatchesName("State.Deadly", "State.Dead"));
            Assert.IsFalse(GameplayTagMatcher.MatchesName("State.Crowd", "State.CrowdControl"));
        }

        [Test]
        public void ControlFlagsTrackTagMutations()
        {
            var asc = new AbilitySystemComponent(new ActorId(1));
            Assert.IsFalse(asc.IsDead);
            Assert.IsFalse(asc.IsStunned);

            asc.Tags.AddTag(new GameplayTag(BattleConstants.TagStunned));
            Assert.IsTrue(asc.IsStunned);
            Assert.IsTrue(asc.Tags.HasTag(new GameplayTag("State.CrowdControl")));

            asc.Tags.AddTag(new GameplayTag(BattleConstants.TagDead));
            Assert.IsTrue(asc.IsDead);

            asc.Tags.RemoveTag(new GameplayTag(BattleConstants.TagStunned));
            Assert.IsFalse(asc.IsStunned);
            Assert.IsTrue(asc.IsDead);

            asc.Tags.Clear();
            Assert.IsFalse(asc.IsDead);
        }
    }
}
