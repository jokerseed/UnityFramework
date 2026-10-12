using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.FixedMath;
using Framework.GamePlay;
using Framework.GamePlay.Data;
using Framework.GAS.Abilities;
using Framework.GAS.Abilities.Builtin;
using Framework.GAS.Effects;
using Framework.GAS.Tags;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    /// <summary>配置目录不依赖 Luban 表：假目录可以注册技能并施加效果。</summary>
    public sealed class ConfigCatalogTests
    {
        [Test]
        public void FakeCatalogAppliesEffectAndRegistersAbility()
        {
            using (var framework = new GamePlayFramework(1))
            {
                var actorId = new ActorId(1);
                var asc = framework.CreateActor(actorId, Vector3.zero, 100f, teamId: 0);

                framework.Commands.EnqueueApplyEffect(new ApplyEffectCommand
                {
                    Source = actorId,
                    Target = actorId,
                    EffectId = "Test.Buff"
                });
                Assert.Throws<InvalidOperationException>(() => framework.Tick((FP)1 / 30));
                framework.Commands.ClearAll();
                Assert.AreEqual(0, asc.ActiveEffectCount);

                var effect = new GameplayEffectDef(
                    "Test.Buff",
                    EffectDurationPolicy.Duration,
                    (FP)2,
                    grantedTags: new[] { new GameplayTag("Test.Buffed") });
                framework.EffectCatalog = new FakeEffectCatalog(effect);

                framework.Commands.EnqueueApplyEffect(new ApplyEffectCommand
                {
                    Source = actorId,
                    Target = actorId,
                    EffectId = "Test.Buff"
                });
                framework.Tick((FP)1 / 30);
                Assert.AreEqual(1, asc.ActiveEffectCount);
                Assert.AreEqual("Test.Buff", asc.GetActiveEffect(0).Spec.EffectId);
                Assert.IsTrue(asc.Tags.HasTag(new GameplayTag("Test.Buffed")));

                framework.Commands.EnqueueApplyEffect(new ApplyEffectCommand
                {
                    Source = actorId,
                    Target = actorId,
                    EffectId = "Missing.Effect"
                });
                Assert.Throws<InvalidOperationException>(() => framework.Tick((FP)1 / 30));
                framework.Commands.ClearAll();
                Assert.AreEqual(1, asc.ActiveEffectCount);

                var dash = new DashAbility("Test.Dash", 1f, 3f, 0.2f, null);
                var abilities = new FakeAbilityCatalog(dash);
                GamePlayConfigSetup.RegisterActorAbilities(
                    framework,
                    actorId,
                    teamId: 0,
                    abilityIds: new[] { "Test.Dash" },
                    abilities);
                Assert.IsTrue(asc.TryGetSpecHandle("Test.Dash", out _));

                Assert.Throws<KeyNotFoundException>(() =>
                    GamePlayConfigSetup.RegisterActorAbilities(
                        framework,
                        actorId,
                        0,
                        new[] { "Missing.Ability" },
                        abilities));
            }
        }

        [Test]
        public void ApplierUsesEffectCatalog()
        {
            using (var framework = new GamePlayFramework(1))
            {
                var actorId = new ActorId(1);
                var asc = framework.CreateActor(actorId, Vector3.zero, 100f, teamId: 0);
                var effect = new GameplayEffectDef(
                    "Test.Direct",
                    EffectDurationPolicy.Infinite,
                    FP.Zero);
                var catalog = new FakeEffectCatalog(effect);

                BattleConfigApplier.ApplyEffect(asc, "Test.Direct", actorId, catalog, framework.Context);
                Assert.AreEqual("Test.Direct", asc.GetActiveEffect(0).Spec.EffectId);

                Assert.Throws<KeyNotFoundException>(() =>
                    BattleConfigApplier.ApplyEffect(asc, "Missing.Effect", actorId, catalog, framework.Context));
                Assert.Throws<ArgumentNullException>(() =>
                    BattleConfigApplier.ApplyEffect(asc, "Test.Direct", actorId, null, framework.Context));
            }
        }

        sealed class FakeEffectCatalog : IBattleEffectCatalog
        {
            readonly Dictionary<string, GameplayEffectDef> _effects = new Dictionary<string, GameplayEffectDef>();

            public FakeEffectCatalog(GameplayEffectDef effect)
            {
                _effects[effect.EffectId] = effect;
            }

            public bool TryCreateEffect(string effectId, out GameplayEffectDef effect)
            {
                if (effectId != null && _effects.TryGetValue(effectId, out effect))
                {
                    return true;
                }

                effect = null;
                return false;
            }
        }

        sealed class FakeAbilityCatalog : IBattleAbilityCatalog
        {
            readonly GameplayAbilityDef _def;

            public FakeAbilityCatalog(GameplayAbility ability)
            {
                _def = new GameplayAbilityDef(ability);
            }

            public bool TryCreateAbility(string abilityId, int teamId, out GameplayAbilityDef ability)
            {
                if (abilityId == _def.AbilityId)
                {
                    ability = _def;
                    return true;
                }

                ability = null;
                return false;
            }
        }
    }
}
