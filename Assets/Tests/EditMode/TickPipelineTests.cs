using System.Collections.Generic;
using Framework.Core;
using Framework.FixedMath;
using Framework.GamePlay;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    /// <summary>默认逻辑步管线的顺序，以及插入空步骤不改变校验和。</summary>
    public sealed class TickPipelineTests
    {
        [Test]
        public void DefaultOrderMatchesBuiltinPhases()
        {
            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(1);
                var steps = new List<BattleTickStepInfo>();
                session.Framework.CopyTickSteps(steps);
                Assert.AreEqual(9, steps.Count);
                AssertStep(steps, 0, "RebuildSpatialIndex", (int)BattleTickPhase.RebuildSpatialIndex);
                AssertStep(steps, 1, "SyncCue", (int)BattleTickPhase.SyncCue);
                AssertStep(steps, 2, "ApplyRootAndStun", (int)BattleTickPhase.ApplyRootAndStun);
                AssertStep(steps, 3, "TickAbilities", (int)BattleTickPhase.TickAbilities);
                AssertStep(steps, 4, "FlushSpawn", (int)BattleTickPhase.FlushSpawn);
                AssertStep(steps, 5, "TickEcs", (int)BattleTickPhase.TickEcs);
                AssertStep(steps, 6, "FlushOutcome", (int)BattleTickPhase.FlushOutcome);
                AssertStep(steps, 7, "SyncDeath", (int)BattleTickPhase.SyncDeath);
                AssertStep(steps, 8, "SyncPositions", (int)BattleTickPhase.SyncPositions);
            }
        }

        [Test]
        public void InsertedStepKeepsRelativeOrder()
        {
            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(1);
                var log = new List<string>();
                session.Framework.AddTickStep(-10, "Before", _ => log.Add("before"));
                session.Framework.AddTickStep((int)BattleTickPhase.ApplyRootAndStun + 1, "Between", _ => log.Add("between"));
                session.Framework.AddTickStep(10000, "After", _ => log.Add("after"));
                session.Step(1);
                Assert.AreEqual(3, log.Count);
                Assert.AreEqual("before", log[0]);
                Assert.AreEqual("between", log[1]);
                Assert.AreEqual("after", log[2]);

                var steps = new List<BattleTickStepInfo>();
                session.Framework.CopyTickSteps(steps);
                var between = IndexOf(steps, "Between");
                Assert.Greater(between, IndexOf(steps, "ApplyRootAndStun"));
                Assert.Less(between, IndexOf(steps, "TickAbilities"));
            }
        }

        [Test]
        public void NoOpInsertedStepDoesNotChangeChecksum()
        {
            var plain = Simulate(42, false);
            var withNoOp = Simulate(42, true);
            CollectionAssert.AreEqual(plain, withNoOp);
        }

        static void AssertStep(List<BattleTickStepInfo> steps, int index, string name, int order)
        {
            Assert.AreEqual(name, steps[index].Name);
            Assert.AreEqual(order, steps[index].Order);
        }

        static int IndexOf(List<BattleTickStepInfo> steps, string name)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].Name == name)
                {
                    return i;
                }
            }

            return -1;
        }

        static long[] Simulate(int seed, bool insertNoOp)
        {
            const int steps = 24;
            var checksums = new long[steps];
            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(seed);
                var framework = session.Framework;
                if (insertNoOp)
                {
                    framework.AddTickStep((int)BattleTickPhase.TickAbilities + 1, "NoOp", _ => { });
                }

                var hero = session.AllocateActorId();
                var enemy = session.AllocateActorId();
                framework.CreateActor(hero, new Vector3(-1f, 0f, 0f), 100f, 0);
                framework.CreateActor(enemy, new Vector3(2f, 0f, 2f), 90f, 1);
                var velocity = new TSVector((FP)1.5f, FP.Zero, (FP)(-0.35f));
                for (var i = 0; i < steps; i++)
                {
                    session.Step(1, _ =>
                    {
                        framework.SetActorVelocity(hero, velocity);
                        framework.Random.Next();
                    });
                    checksums[i] = BattleFrameChecksum.Compute(framework);
                }
            }

            return checksums;
        }
    }
}
