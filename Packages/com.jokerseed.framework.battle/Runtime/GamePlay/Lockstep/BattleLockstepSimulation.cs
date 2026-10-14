using System;
using System.Collections.Generic;
using Framework.BehaviourTree;
using Framework.Core;
using Framework.Core.Snapshot;
using Framework.ECS;
using Framework.FixedMath;
using Framework.GAS;
using Framework.GAS.Snapshot;
using Framework.Lockstep;
using UnityEngine;

namespace Framework.GamePlay
{
    /// <summary>
    /// 用一场 <see cref="BattleSession"/> 实现 <see cref="ILockstepSimulation"/>。
    /// 检查点包含随机数、ECS、GAS、Farseer 接触克隆，以及已绑定 Agent 的行为树快照。玩家下标对应创建顺序里的 Actor。
    /// 输入是 8 字节：X、Z 速度的毫单位，小端 int。
    /// </summary>
    public sealed class BattleLockstepSimulation : ILockstepSimulation, IDisposable
    {
        readonly BattleDirector _director;
        readonly BattleSession _session;
        readonly ActorId[] _actors;
        readonly List<AbilitySystemComponent> _components;
        readonly DeterministicRandomSnapshot _random;
        readonly Dictionary<int, Checkpoint> _checkpoints = new Dictionary<int, Checkpoint>();
        readonly List<ActorId> _actorScratch = new List<ActorId>(8);

        /// <summary>创建一场两名以上单位、固定种子的模拟。</summary>
        /// <param name="seed">随机种子。对端必须相同。</param>
        /// <param name="actorCount">单位数量，也是玩家数量；至少为 1。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="actorCount"/> 小于 1。</exception>
        public BattleLockstepSimulation(int seed, int actorCount = 2)
        {
            if (actorCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(actorCount));
            }

            _director = new BattleDirector();
            _session = _director.CreateSession(seed);
            _actors = new ActorId[actorCount];
            _components = new List<AbilitySystemComponent>(actorCount);
            for (var i = 0; i < actorCount; i++)
            {
                var id = _session.AllocateActorId();
                _actors[i] = id;
                var asc = _session.Framework.CreateActor(id, new Vector3(i * 4f, 0f, 0f), 100f, teamId: i);
                _components.Add(asc);
            }

            _random = new DeterministicRandomSnapshot(_session.Framework.Random);
        }

        /// <inheritdoc />
        public long Checksum => BattleFrameChecksum.Compute(_session.Framework);

        /// <summary>把毫单位速度编成 8 字节载荷。</summary>
        /// <param name="xMilli">X 速度，1 表示 0.01 米/秒。</param>
        /// <param name="zMilli">Z 速度，1 表示 0.01 米/秒。</param>
        /// <returns>小端载荷。</returns>
        public static byte[] EncodeVelocity(int xMilli, int zMilli)
        {
            var bytes = new byte[8];
            WriteInt(bytes, 0, xMilli);
            WriteInt(bytes, 4, zMilli);
            return bytes;
        }

        /// <inheritdoc />
        public void SaveCheckpoint(int tick)
        {
            var gas = new GasSnapshot(_components, _session.Framework.Context);
            var buffer = new SnapshotBuffer();
            var writer = buffer.BeginWrite();
            _random.Save(writer);
            _session.Framework.EcsWorld.Save(writer);
            gas.Save(writer);
            var physics = new BattlePhysicsSnapshot();
            _session.Framework.EcsWorld.GetSingleton<BattlePhysicsWorld>().SaveFull(physics);
            var framework = _session.Framework;
            framework.Registry.CopySortedActorIds(_actorScratch);
            var agentActors = new List<uint>();
            var agentSnapshots = new List<BtSnapshot>();
            for (var i = 0; i < _actorScratch.Count; i++)
            {
                var actorId = _actorScratch[i];
                if (!framework.TryGetBattleAgent(actorId, out var agent))
                {
                    continue;
                }

                agentActors.Add(actorId.Value);
                agentSnapshots.Add(agent.CaptureTree());
            }

            _checkpoints[tick] = new Checkpoint(buffer, gas, physics, agentActors.ToArray(), agentSnapshots.ToArray());
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">该步没有检查点。</exception>
        public void RestoreCheckpoint(int tick)
        {
            if (!_checkpoints.TryGetValue(tick, out var checkpoint))
            {
                throw new InvalidOperationException("No battle checkpoint at tick " + tick);
            }

            var reader = checkpoint.Buffer.BeginRead();
            _random.Restore(reader);
            var world = _session.Framework.EcsWorld;
            world.Restore(reader);
            checkpoint.Gas.Restore(reader);
            world.GetSingleton<BattlePhysicsWorld>().RestoreFull(checkpoint.Physics);
            _session.Framework.Commands.ClearAll();
            _session.Framework.Registry.RebindEntitiesAfterSnapshot();
            _session.Framework.Registry.SyncPositionsFromEcs();
            world.GetSingleton<SpatialHashGrid>().MarkStructuralDirty();
            var framework = _session.Framework;
            var actors = checkpoint.AgentActors;
            for (var i = 0; i < actors.Length; i++)
            {
                if (framework.TryGetBattleAgent(new ActorId(actors[i]), out var agent))
                {
                    agent.RestoreTree(checkpoint.AgentSnapshots[i]);
                }
            }
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">没有不大于该步的检查点。</exception>
        public int LatestCheckpointAtOrBefore(int tick)
        {
            var found = false;
            var best = 0;
            foreach (var pair in _checkpoints)
            {
                if (pair.Key <= tick && (!found || pair.Key > best))
                {
                    best = pair.Key;
                    found = true;
                }
            }

            if (!found)
            {
                throw new InvalidOperationException("No battle checkpoint at or before tick " + tick);
            }

            return best;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 小于 0。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="playerIds"/> 为 null。</exception>
        /// <exception cref="ArgumentException">玩家数或载荷数与单位数不一致。</exception>
        public void Simulate(int tick, int[] playerIds, byte[][] payloads)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }

            if (playerIds == null)
            {
                throw new ArgumentNullException(nameof(playerIds));
            }

            if (payloads == null || payloads.Length != _actors.Length || playerIds.Length != _actors.Length)
            {
                throw new ArgumentException("Player inputs must match the actor count.");
            }

            var framework = _session.Framework;
            var count = _actors.Length;
            for (var i = 0; i < count; i++)
            {
                var velocity = DecodeVelocity(payloads[i]);
                framework.SetActorVelocity(_actors[i], velocity);
            }

            _session.Step(1);
        }

        /// <summary>释放会话。</summary>
        public void Dispose()
        {
            _director.Dispose();
        }

        static TSVector DecodeVelocity(byte[] payload)
        {
            if (payload == null || payload.Length < 8)
            {
                return TSVector.zero;
            }

            var x = ReadInt(payload, 0);
            var z = ReadInt(payload, 4);
            return new TSVector((FP)x / (FP)100, FP.Zero, (FP)z / (FP)100);
        }

        static void WriteInt(byte[] bytes, int offset, int value)
        {
            var raw = unchecked((uint)value);
            bytes[offset] = (byte)raw;
            bytes[offset + 1] = (byte)(raw >> 8);
            bytes[offset + 2] = (byte)(raw >> 16);
            bytes[offset + 3] = (byte)(raw >> 24);
        }

        static int ReadInt(byte[] bytes, int offset)
        {
            return bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24);
        }

        sealed class Checkpoint
        {
            public readonly SnapshotBuffer Buffer;
            public readonly GasSnapshot Gas;
            public readonly BattlePhysicsSnapshot Physics;
            public readonly uint[] AgentActors;
            public readonly BtSnapshot[] AgentSnapshots;

            public Checkpoint(
                SnapshotBuffer buffer,
                GasSnapshot gas,
                BattlePhysicsSnapshot physics,
                uint[] agentActors,
                BtSnapshot[] agentSnapshots)
            {
                Buffer = buffer;
                Gas = gas;
                Physics = physics;
                AgentActors = agentActors;
                AgentSnapshots = agentSnapshots;
            }
        }
    }
}
