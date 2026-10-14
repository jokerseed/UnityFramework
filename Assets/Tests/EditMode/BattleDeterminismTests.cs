using System.Collections.Generic;
using Framework.Battle;
using Framework.Editor.Determinism;
using Framework.FixedMath;
using Framework.GamePlay;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    /// <summary>固定种子战斗与录像对拍。在 Unity Editor 的 Test Runner（EditMode）里运行。</summary>
    public sealed class BattleDeterminismTests
    {
        const int Steps = 24;

        [Test]
        public void SameSeedProducesSameChecksumSequence()
        {
            var first = Simulate(42);
            var second = Simulate(42);
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void DifferentSeedChangesChecksum()
        {
            var left = Simulate(42);
            var right = Simulate(99);
            Assert.AreNotEqual(left[left.Length - 1], right[right.Length - 1]);
        }

        [Test]
        public void ReplayTapeMatchesRecordedChecksums()
        {
            const int seed = 7;
            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(seed);
                var hero = session.AllocateActorId();
                var enemy = session.AllocateActorId();
                session.Framework.CreateActor(hero, Vector3.zero, 120f, 0);
                session.Framework.CreateActor(enemy, new Vector3(4f, 0f, 1f), 80f, 1);

                var recorder = new BattleReplayRecorder();
                var frame = new BattleIntentFrame();
                var velocity = new TSVector((FP)1.25f, FP.Zero, (FP)0.5f);
                for (var i = 0; i < 10; i++)
                {
                    frame.Reset();
                    frame.FrameIndex = i;
                    frame.Commands.Add(BattleIntentCommand.Move(hero, velocity, velocity, BattleIntentSource.Player));
                    session.Step(1, _ => BattleIntentApplier.ApplyAll(session.Framework, frame.Commands));
                    recorder.Capture(frame, BattleFrameChecksum.Compute(session.Framework));
                }

                var tape = recorder.ToTape(seed, session.FixedDeltaTime);
                using (var shadowDirector = new BattleDirector())
                {
                    var shadow = shadowDirector.CreateSession(tape.Seed);
                    shadow.Framework.CreateActor(shadow.AllocateActorId(), Vector3.zero, 120f, 0);
                    shadow.Framework.CreateActor(shadow.AllocateActorId(), new Vector3(4f, 0f, 1f), 80f, 1);
                    var result = BattleReplayVerifier.Run(tape, shadow);
                    Assert.IsTrue(
                        result.Matched,
                        "mismatch frame " + result.MismatchFrameIndex +
                        " expected " + result.ExpectedChecksum +
                        " actual " + result.ActualChecksum);
                    Assert.AreEqual(10, result.ComparedFrames);
                }
            }
        }

        [Test]
        public void ScannerRejectsUnityRandom()
        {
            var violations = new List<string>();
            SimDeterminismScanner.CollectViolations(
                "Packages/com.jokerseed.framework.battle/Runtime/ECS/Fake.cs",
                "var n = UnityEngine.Random.Range(0, 1);",
                violations);
            Assert.Greater(violations.Count, 0);
        }

        [Test]
        public void ScannerRejectsSimEventSubscription()
        {
            var violations = new List<string>();
            SimDeterminismScanner.CollectViolations(
                "Packages/com.jokerseed.framework.battle/Runtime/GAS/Fake.cs",
                "GameEvent.Listen<DamageDealtEvent>(OnDamage);",
                violations);
            Assert.Greater(violations.Count, 0);

            violations.Clear();
            SimDeterminismScanner.CollectViolations(
                "Packages/com.jokerseed.framework.battle/Runtime/GamePlay/Fake.cs",
                "bus.Subscribe<DamageDealtEvent>(OnDamage);",
                violations);
            Assert.Greater(violations.Count, 0);

            violations.Clear();
            SimDeterminismScanner.CollectViolations(
                "Packages/com.jokerseed.framework.battle/Runtime/GAS/Fake.cs",
                "public IDisposable Subscribe<T>(Action<T> handler) where T : struct => EmptySubscription.Instance;",
                violations);
            Assert.AreEqual(0, violations.Count);
        }

        [Test]
        public void ScannerAcceptsCurrentSimSources()
        {
            var violations = SimDeterminismScanner.Scan();
            Assert.IsEmpty(violations, string.Join("\n", violations.ToArray()));
        }

        static long[] Simulate(int seed)
        {
            var checksums = new long[Steps];
            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(seed);
                var framework = session.Framework;
                var hero = session.AllocateActorId();
                var enemy = session.AllocateActorId();
                framework.CreateActor(hero, new Vector3(-1f, 0f, 0f), 100f, 0);
                framework.CreateActor(enemy, new Vector3(2f, 0f, 2f), 90f, 1);
                var velocity = new TSVector((FP)1.5f, FP.Zero, (FP)(-0.35f));
                for (var i = 0; i < Steps; i++)
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
