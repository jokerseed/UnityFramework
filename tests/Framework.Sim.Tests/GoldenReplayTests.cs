using System.Collections.Generic;
using Framework.Battle;
using Framework.FixedMath;
using Framework.GamePlay;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    /// <summary>
    /// Golden 录像：固定种子与输入下逐帧结果必须与仓库里记录的值一致。
    /// 这个测试失败说明模拟逻辑的逐帧结果变了：若是有意改动，请把 <see cref="BattleSimulationVersion.Current"/> 加一并更新下面的 golden 值；
    /// 若不是有意改动，说明引入了非确定性或回归。
    /// 因为在 CI 的 Linux 与本地 Windows 上都会跑，同一个常量也顺带检查了跨平台一致性。
    /// </summary>
    public sealed class GoldenReplayTests
    {
        const int Seed = 20260528;
        const int Frames = 120;

        /// <summary>第 <see cref="Frames"/> 帧结束时的校验和；改动模拟逻辑后需要同步更新。</summary>
        const long GoldenFinalChecksum = unchecked((long)0xA676A203AE287D9AUL);

        /// <summary>golden 值对应的模拟版本。</summary>
        const int GoldenSimulationVersion = 1;

        [Test]
        public void SameInputTwice_ProducesSameChecksums()
        {
            var a = Record(Seed);
            var b = Record(Seed);
            Assert.AreEqual(a.Frames.Count, b.Frames.Count);
            for (var i = 0; i < a.Frames.Count; i++)
            {
                Assert.AreEqual(a.Frames[i].Checksum, b.Frames[i].Checksum, $"frame {i}");
            }
        }

        [Test]
        public void Verifier_ReplaysTapeOnFreshSession()
        {
            var tape = Record(Seed);
            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(Seed);
                CreateActors(session, out _, out _);
                var result = BattleReplayVerifier.Run(tape, session);
                Assert.IsTrue(result.Matched, $"mismatch at frame {result.MismatchFrameIndex}");
                Assert.AreEqual(Frames, result.ComparedFrames);
            }
        }

        [Test]
        public void Verifier_RejectsTapeFromOtherSimulationVersion()
        {
            var tape = Record(Seed);
            var stale = new BattleReplayTape(tape.Seed, tape.FixedDeltaTime, tape.Frames, BattleSimulationVersion.Current + 1);
            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(Seed);
                Assert.Throws<System.InvalidOperationException>(() => BattleReplayVerifier.Run(stale, session));
            }
        }

        [Test]
        public void FinalChecksum_MatchesGolden()
        {
            Assert.AreEqual(GoldenSimulationVersion, BattleSimulationVersion.Current,
                "模拟版本变了：请重新生成 golden 值并更新 GoldenSimulationVersion。");
            var tape = Record(Seed);
            var actual = tape.Frames[tape.Frames.Count - 1].Checksum;
            Assert.AreEqual(GoldenFinalChecksum, actual,
                $"逐帧结果变了，实际 0x{actual:X16}。有意改动请把 BattleSimulationVersion.Current 加一并更新 golden 值。");
        }

        static BattleReplayTape Record(int seed)
        {
            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(seed);
                CreateActors(session, out var hero, out var enemy);
                var frames = new List<BattleReplayFrame>(Frames);
                for (var f = 0; f < Frames; f++)
                {
                    var commands = BuildInput(f, hero, enemy);
                    session.Step(1, _ => BattleIntentApplier.ApplyAll(session.Framework, commands));
                    frames.Add(new BattleReplayFrame(f, commands, BattleFrameChecksum.Compute(session.Framework)));
                }

                return new BattleReplayTape(seed, session.FixedDeltaTime, frames);
            }
        }

        static void CreateActors(BattleSession session, out ActorId hero, out ActorId enemy)
        {
            hero = session.AllocateActorId();
            enemy = session.AllocateActorId();
            session.Framework.CreateActor(hero, TSVector.zero, (FP)100, teamId: 0);
            session.Framework.CreateActor(enemy, new TSVector(6, 0, 0), (FP)80, teamId: 1);
        }

        static BattleIntentCommand[] BuildInput(int frame, ActorId hero, ActorId enemy)
        {
            var heroVelocity = frame < 60 ? new TSVector(2, 0, 0) : new TSVector(0, 0, 1);
            var commands = new List<BattleIntentCommand>(2)
            {
                BattleIntentCommand.Move(hero, heroVelocity, TSVector.zero, BattleIntentSource.Player),
            };

            if (frame % 3 == 0)
            {
                var enemyVelocity = new TSVector(-1, 0, frame % 2 == 0 ? 1 : -1);
                commands.Add(BattleIntentCommand.Move(enemy, enemyVelocity, TSVector.zero, BattleIntentSource.Ai));
            }

            return commands.ToArray();
        }
    }
}
