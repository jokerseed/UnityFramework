using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Snapshot;
using Framework.ECS.Components;
using Framework.FixedMath;

namespace Framework.ECS
{
    /// <summary>ECS 世界的快照。不包含系统回调，也不自动保存单例里的物理接触。</summary>
    public sealed partial class World : ISnapshotable
    {
        const int SnapshotMagic = 0x31534345;
        const int SnapshotVersion = 3;
        const int TypeTransform = 1;
        const int TypeVelocity = 2;
        const int TypeKnockback = 3;
        const int TypeCombat = 4;
        const int TypeTeam = 5;
        const int TypeActorLink = 6;
        const int TypeCollision = 7;
        const int TypeProjectile = 8;

        readonly List<IEcsSnapshotExtension> _snapshotExtensions = new List<IEcsSnapshotExtension>();
        readonly List<uint> _snapshotIds = new List<uint>(64);

        /// <summary>注册额外组件块。类型号须 &gt;= 100，且不可重复。</summary>
        /// <param name="extension">扩展；不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="extension"/> 为 null。</exception>
        /// <exception cref="ArgumentException">类型号非法或重复。</exception>
        public void AddSnapshotExtension(IEcsSnapshotExtension extension)
        {
            if (extension == null)
            {
                throw new ArgumentNullException(nameof(extension));
            }

            if (extension.TypeId < 100)
            {
                throw new ArgumentException("Snapshot extension TypeId must be >= 100.", nameof(extension));
            }

            for (var i = 0; i < _snapshotExtensions.Count; i++)
            {
                if (_snapshotExtensions[i].TypeId == extension.TypeId)
                {
                    throw new ArgumentException("Duplicate snapshot extension TypeId " + extension.TypeId, nameof(extension));
                }
            }

            _snapshotExtensions.Add(extension);
        }

        /// <summary>
        /// 保存存活实体与内置战斗组件。单例（物理世界、空间网格）不在这里。
        /// 物理运动学用 <see cref="BattlePhysicsWorld.SaveKinematics"/>，且不包含接触。
        /// </summary>
        /// <param name="writer">写入器。</param>
        public void Save(SnapshotWriter writer)
        {
            writer.WriteInt(SnapshotMagic);
            writer.WriteInt(SnapshotVersion);
            EnsureCustomComponentsAreRegistered();
            writer.WriteUInt(_nextEntityId);
            writer.WriteInt(_freeEntityIds.Count);
            for (var freeIndex = 0; freeIndex < _freeEntityIds.Count; freeIndex++)
            {
                writer.WriteUInt(_freeEntityIds[freeIndex]);
            }

            _snapshotIds.Clear();
            foreach (var pair in _entities)
            {
                _snapshotIds.Add(pair.Key);
            }

            _snapshotIds.Sort();
            writer.WriteInt(_snapshotIds.Count);
            for (var i = 0; i < _snapshotIds.Count; i++)
            {
                writer.WriteUInt(_snapshotIds[i]);
            }

            WriteTransforms(writer);
            WriteVelocities(writer);
            WriteKnockbacks(writer);
            WriteCombat(writer);
            WriteTeams(writer);
            WriteActorLinks(writer);
            WriteCollisions(writer);
            WriteProjectiles(writer);

            writer.WriteInt(_snapshotExtensions.Count);
            for (var i = 0; i < _snapshotExtensions.Count; i++)
            {
                writer.WriteInt(_snapshotExtensions[i].TypeId);
                _snapshotExtensions[i].Save(this, writer);
            }
        }

        /// <summary>
        /// 清空实体和组件后按快照重建。不销毁系统，不碰单例。
        /// 旧的 <see cref="Entity"/> 会被标成死亡；持有者要按 ID 重新绑定。
        /// 若物理刚体还在，需要另调 <see cref="BattlePhysicsWorld.RestoreKinematics"/>。
        /// </summary>
        /// <param name="reader">读取器。</param>
        /// <exception cref="InvalidOperationException">魔数、版本或扩展类型不匹配。</exception>
        public void Restore(SnapshotReader reader)
        {
            var magic = reader.ReadInt();
            if (magic != SnapshotMagic)
            {
                throw new InvalidOperationException("ECS snapshot magic mismatch.");
            }

            var version = reader.ReadInt();
            if (version != SnapshotVersion)
            {
                throw new InvalidOperationException("Unsupported ECS snapshot version " + version);
            }

            var nextEntityId = reader.ReadUInt();
            var freeCount = reader.ReadInt();
            _freeEntityIds.Clear();
            for (var freeIndex = 0; freeIndex < freeCount; freeIndex++)
            {
                _freeEntityIds.Add(reader.ReadUInt());
            }

            var entityCount = reader.ReadInt();
            _snapshotIds.Clear();
            for (var i = 0; i < entityCount; i++)
            {
                _snapshotIds.Add(reader.ReadUInt());
            }

            ClearEntitiesAndComponents();
            for (var i = 0; i < _snapshotIds.Count; i++)
            {
                RecreateEntity(_snapshotIds[i]);
            }

            _nextEntityId = nextEntityId;

            ReadTransforms(reader);
            ReadVelocities(reader);
            ReadKnockbacks(reader);
            ReadCombat(reader);
            ReadTeams(reader);
            ReadActorLinks(reader);
            ReadCollisions(reader);
            ReadProjectiles(reader);

            var extensionCount = reader.ReadInt();
            if (extensionCount != _snapshotExtensions.Count)
            {
                throw new InvalidOperationException("ECS snapshot extension count mismatch.");
            }

            for (var i = 0; i < extensionCount; i++)
            {
                var typeId = reader.ReadInt();
                if (_snapshotExtensions[i].TypeId != typeId)
                {
                    throw new InvalidOperationException("ECS snapshot extension type mismatch.");
                }

                _snapshotExtensions[i].Restore(this, reader);
            }

            GetSingleton<SpatialHashGrid>()?.MarkStructuralDirty();
        }

        void EnsureCustomComponentsAreRegistered()
        {
            foreach (var pair in _storages)
            {
                if (pair.Value.Count == 0 || IsBuiltinComponent(pair.Key))
                {
                    continue;
                }

                var covered = false;
                for (var i = 0; i < _snapshotExtensions.Count; i++)
                {
                    if (_snapshotExtensions[i].ComponentType == pair.Key)
                    {
                        covered = true;
                        break;
                    }
                }

                if (!covered)
                {
                    throw new InvalidOperationException(
                        "Component " + pair.Key.FullName + " has no IEcsSnapshotExtension.");
                }
            }
        }

        static bool IsBuiltinComponent(Type type)
        {
            return type == typeof(TransformComponent)
                || type == typeof(VelocityComponent)
                || type == typeof(KnockbackComponent)
                || type == typeof(CombatStateComponent)
                || type == typeof(TeamComponent)
                || type == typeof(ActorLinkComponent)
                || type == typeof(CollisionComponent)
                || type == typeof(ProjectileComponent);
        }

        void ClearEntitiesAndComponents()
        {
            foreach (var pair in _entities)
            {
                pair.Value.IsAlive = false;
            }

            foreach (var storage in _storages.Values)
            {
                storage.Clear();
            }

            _entities.Clear();
            _nextEntityId = 1;
        }

        void RecreateEntity(uint id)
        {
            var entity = new Entity(id);
            _entities[id] = entity;
        }

        void WriteTransforms(SnapshotWriter writer)
        {
            var storage = GetStorage<TransformComponent>();
            writer.WriteInt(TypeTransform);
            writer.WriteInt(storage.Count);
            foreach (var pair in storage)
            {
                writer.WriteUInt(pair.Key);
                writer.WriteFp(pair.Value.Position.x);
                writer.WriteFp(pair.Value.Position.y);
                writer.WriteFp(pair.Value.Position.z);
                writer.WriteFp(pair.Value.Forward.x);
                writer.WriteFp(pair.Value.Forward.y);
                writer.WriteFp(pair.Value.Forward.z);
            }
        }

        void ReadTransforms(SnapshotReader reader)
        {
            var count = ExpectBlock(reader, TypeTransform);
            var storage = GetStorage<TransformComponent>();
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadUInt();
                var component = new TransformComponent
                {
                    Position = new TSVector(reader.ReadFp(), reader.ReadFp(), reader.ReadFp()),
                    Forward = new TSVector(reader.ReadFp(), reader.ReadFp(), reader.ReadFp())
                };
                storage.Add(id, component);
            }
        }

        void WriteVelocities(SnapshotWriter writer)
        {
            var storage = GetStorage<VelocityComponent>();
            writer.WriteInt(TypeVelocity);
            writer.WriteInt(storage.Count);
            foreach (var pair in storage)
            {
                writer.WriteUInt(pair.Key);
                writer.WriteFp(pair.Value.Value.x);
                writer.WriteFp(pair.Value.Value.y);
                writer.WriteFp(pair.Value.Value.z);
            }
        }

        void ReadVelocities(SnapshotReader reader)
        {
            var count = ExpectBlock(reader, TypeVelocity);
            var storage = GetStorage<VelocityComponent>();
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadUInt();
                storage.Add(id, new VelocityComponent
                {
                    Value = new TSVector(reader.ReadFp(), reader.ReadFp(), reader.ReadFp())
                });
            }
        }

        void WriteKnockbacks(SnapshotWriter writer)
        {
            var storage = GetStorage<KnockbackComponent>();
            writer.WriteInt(TypeKnockback);
            writer.WriteInt(storage.Count);
            foreach (var pair in storage)
            {
                writer.WriteUInt(pair.Key);
                writer.WriteFp(pair.Value.Velocity.x);
                writer.WriteFp(pair.Value.Velocity.y);
                writer.WriteFp(pair.Value.Velocity.z);
                writer.WriteFp(pair.Value.Remaining);
            }
        }

        void ReadKnockbacks(SnapshotReader reader)
        {
            var count = ExpectBlock(reader, TypeKnockback);
            var storage = GetStorage<KnockbackComponent>();
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadUInt();
                storage.Add(id, new KnockbackComponent
                {
                    Velocity = new TSVector(reader.ReadFp(), reader.ReadFp(), reader.ReadFp()),
                    Remaining = reader.ReadFp()
                });
            }
        }

        void WriteCombat(SnapshotWriter writer)
        {
            var storage = GetStorage<CombatStateComponent>();
            writer.WriteInt(TypeCombat);
            writer.WriteInt(storage.Count);
            foreach (var pair in storage)
            {
                writer.WriteUInt(pair.Key);
                writer.WriteBool(pair.Value.IsAlive);
            }
        }

        void ReadCombat(SnapshotReader reader)
        {
            var count = ExpectBlock(reader, TypeCombat);
            var storage = GetStorage<CombatStateComponent>();
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadUInt();
                storage.Add(id, new CombatStateComponent { IsAlive = reader.ReadBool() });
            }
        }

        void WriteTeams(SnapshotWriter writer)
        {
            var storage = GetStorage<TeamComponent>();
            writer.WriteInt(TypeTeam);
            writer.WriteInt(storage.Count);
            foreach (var pair in storage)
            {
                writer.WriteUInt(pair.Key);
                writer.WriteInt(pair.Value.TeamId);
            }
        }

        void ReadTeams(SnapshotReader reader)
        {
            var count = ExpectBlock(reader, TypeTeam);
            var storage = GetStorage<TeamComponent>();
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadUInt();
                storage.Add(id, new TeamComponent { TeamId = reader.ReadInt() });
            }
        }

        void WriteActorLinks(SnapshotWriter writer)
        {
            var storage = GetStorage<ActorLinkComponent>();
            writer.WriteInt(TypeActorLink);
            writer.WriteInt(storage.Count);
            foreach (var pair in storage)
            {
                writer.WriteUInt(pair.Key);
                writer.WriteUInt(pair.Value.ActorId.Value);
            }
        }

        void ReadActorLinks(SnapshotReader reader)
        {
            var count = ExpectBlock(reader, TypeActorLink);
            var storage = GetStorage<ActorLinkComponent>();
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadUInt();
                storage.Add(id, new ActorLinkComponent { ActorId = new ActorId(reader.ReadUInt()) });
            }
        }

        void WriteCollisions(SnapshotWriter writer)
        {
            var storage = GetStorage<CollisionComponent>();
            writer.WriteInt(TypeCollision);
            writer.WriteInt(storage.Count);
            foreach (var pair in storage)
            {
                writer.WriteUInt(pair.Key);
                writer.WriteFp(pair.Value.Radius);
            }
        }

        void ReadCollisions(SnapshotReader reader)
        {
            var count = ExpectBlock(reader, TypeCollision);
            var storage = GetStorage<CollisionComponent>();
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadUInt();
                storage.Add(id, new CollisionComponent { Radius = reader.ReadFp() });
            }
        }

        void WriteProjectiles(SnapshotWriter writer)
        {
            var storage = GetStorage<ProjectileComponent>();
            writer.WriteInt(TypeProjectile);
            writer.WriteInt(storage.Count);
            foreach (var pair in storage)
            {
                var projectile = pair.Value;
                writer.WriteUInt(pair.Key);
                writer.WriteUInt(projectile.Owner.Value);
                writer.WriteString(projectile.AbilityId);
                writer.WriteFp(projectile.Damage);
                writer.WriteFp(projectile.Radius);
                writer.WriteFp(projectile.RemainingLifetime);
                writer.WriteInt(projectile.TeamId);
                writer.WriteInt(projectile.PierceRemaining);
                writer.WriteString(projectile.HitEffectId);
                writer.WriteFp(projectile.ExplodeRadius);
                writer.WriteInt((int)projectile.DamageType);
                var hits = projectile.HitEntityIds;
                writer.WriteInt(hits != null ? hits.Count : 0);
                if (hits != null)
                {
                    for (var h = 0; h < hits.Count; h++)
                    {
                        writer.WriteUInt(hits[h]);
                    }
                }
            }
        }

        void ReadProjectiles(SnapshotReader reader)
        {
            var count = ExpectBlock(reader, TypeProjectile);
            var storage = GetStorage<ProjectileComponent>();
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadUInt();
                storage.Add(id, new ProjectileComponent
                {
                    Owner = new ActorId(reader.ReadUInt()),
                    AbilityId = reader.ReadString(),
                    Damage = reader.ReadFp(),
                    Radius = reader.ReadFp(),
                    RemainingLifetime = reader.ReadFp(),
                    TeamId = reader.ReadInt(),
                    PierceRemaining = reader.ReadInt(),
                    HitEffectId = reader.ReadString(),
                    ExplodeRadius = reader.ReadFp(),
                    DamageType = (BattleDamageType)reader.ReadInt(),
                    HitEntityIds = ReadHitEntities(reader)
                });
            }
        }

        static List<uint> ReadHitEntities(SnapshotReader reader)
        {
            var count = reader.ReadInt();
            if (count <= 0)
            {
                return null;
            }

            var hits = new List<uint>(count);
            for (var i = 0; i < count; i++)
            {
                hits.Add(reader.ReadUInt());
            }

            return hits;
        }

        static int ExpectBlock(SnapshotReader reader, int typeId)
        {
            var actual = reader.ReadInt();
            if (actual != typeId)
            {
                throw new InvalidOperationException("ECS snapshot block mismatch. Expected " + typeId + " but was " + actual);
            }

            return reader.ReadInt();
        }
    }
}
