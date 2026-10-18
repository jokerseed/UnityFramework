using System;
using System.Collections.Generic;
using Framework.Battle;
using Framework.Battle.Snapshot;
using Framework.ECS.Components;
using Framework.FixedMath;
using Framework.Lockstep;
using Framework.Lockstep.Physics2D;
using PhysicsBody = Framework.Lockstep.Physics2D.Body;
using PhysicsWorld = Framework.Lockstep.Physics2D.World;

namespace Framework.ECS
{
    /// <summary>
    /// 战斗用 Farseer 2D 物理世界：XZ 平面圆体、无重力。
    /// Actor 为动态刚体（互相挤开）；投射物为运动学传感器（只检测、不推开）。
    /// </summary>
    public sealed class BattlePhysicsWorld : IEcsBodyRemover
    {
        const Category ActorCategory = Category.Cat1;
        const Category ProjectileCategory = Category.Cat2;
        static readonly FP BodyDensity = FP.One;

        readonly PhysicsWorld _world;
        readonly Dictionary<uint, PhysicsBody> _bodies = new Dictionary<uint, PhysicsBody>(64);
        readonly List<uint> _bodyScratch = new List<uint>(64);
        readonly List<RetiredBody> _retired = new List<RetiredBody>();
        readonly HashSet<PhysicsBody> _retiredScratch = new HashSet<PhysicsBody>();
        int _stepCount;

        /// <summary>底层 Farseer 世界。</summary>
        public PhysicsWorld Native => _world;

        /// <summary>
        /// 为 true 时，删除实体不会马上把刚体移出 Farseer 世界：刚体只被停用并记进退役表。
        /// 回滚到删除之前的检查点时，世界克隆会把它恢复成启用状态，实体到刚体的映射随之重建，
        /// 已销毁又被快照恢复的投射物能照常碰撞。退役刚体要等 <see cref="PurgeRetiredBefore"/> 才真正移除。
        /// 锁步回滚（<c>BattleLockstepSimulation</c>）会打开它；默认关闭，删除即释放。
        /// </summary>
        public bool RetainRemovedBodies { get; set; }

        /// <summary>已经推进的物理步数。<see cref="RestoreFull"/> 会恢复成快照里的值。</summary>
        public int StepCount => _stepCount;

        /// <summary>当前退役表里的刚体数。</summary>
        public int RetiredBodyCount => _retired.Count;

        /// <summary>创建无重力的 2D 物理世界。</summary>
        public BattlePhysicsWorld()
        {
            _world = new PhysicsWorld(TSVector2.zero);
            _world.ContactManager.ContactFilter = FilterContact;
        }

        /// <summary>为 Actor 创建圆形动态刚体。</summary>
        /// <param name="entityId">ECS 实体 ID。</param>
        /// <param name="actorId">Actor ID。</param>
        /// <param name="teamId">队伍 ID。</param>
        /// <param name="position">世界坐标（取 XZ）。</param>
        /// <param name="radius">碰撞半径；≤0 时用默认半径。</param>
        public void AddActorBody(
            uint entityId,
            ActorId actorId,
            int teamId,
            TSVector position,
            FP radius)
        {
            RemoveBody(entityId);
            var data = new BattlePhysicsUserData
            {
                EntityId = entityId,
                ActorId = actorId,
                TeamId = teamId
            };
            var body = CreateCircle(position, radius, data);
            body.BodyType = BodyType.Dynamic;
            ConfigureShared(body);
            body.CollisionCategories = ActorCategory;
            body.CollidesWith = ActorCategory | ProjectileCategory;
            _bodies[entityId] = body;
        }

        /// <summary>为投射物创建圆形运动学传感器。</summary>
        /// <param name="entityId">ECS 实体 ID。</param>
        /// <param name="owner">发射者。</param>
        /// <param name="teamId">队伍 ID。</param>
        /// <param name="position">世界坐标（取 XZ）。</param>
        /// <param name="radius">碰撞半径；≤0 时用默认半径。</param>
        public void AddProjectileBody(
            uint entityId,
            ActorId owner,
            int teamId,
            TSVector position,
            FP radius)
        {
            RemoveBody(entityId);
            var data = new BattlePhysicsUserData
            {
                EntityId = entityId,
                IsProjectile = true,
                Owner = owner,
                TeamId = teamId
            };
            var body = CreateCircle(position, radius, data);
            body.BodyType = BodyType.Kinematic;
            ConfigureShared(body);
            body.IsSensor = true;
            body.CollisionCategories = ProjectileCategory;
            body.CollidesWith = ActorCategory;
            _bodies[entityId] = body;
        }

        /// <summary>写入刚体线速度（XZ）。</summary>
        /// <param name="entityId">ECS 实体 ID。</param>
        /// <param name="velocity">世界速度。</param>
        public void SetLinearVelocity(uint entityId, TSVector velocity)
        {
            if (!_bodies.TryGetValue(entityId, out var body) || body.TSDisabled)
            {
                return;
            }

            body.LinearVelocity = ToPlane(velocity);
        }

        /// <summary>传送刚体到指定坐标，不改朝向。</summary>
        /// <param name="entityId">ECS 实体 ID。</param>
        /// <param name="position">世界坐标。</param>
        public void SetPosition(uint entityId, TSVector position)
        {
            if (!_bodies.TryGetValue(entityId, out var body))
            {
                return;
            }

            body.SetTransform(ToPlane(position), body.Rotation);
        }

        /// <summary>启用或禁用刚体碰撞与模拟。</summary>
        /// <param name="entityId">ECS 实体 ID。</param>
        /// <param name="enabled">为 false 时不参与碰撞。</param>
        public void SetEnabled(uint entityId, bool enabled)
        {
            if (!_bodies.TryGetValue(entityId, out var body))
            {
                return;
            }

            body.Enabled = enabled;
            body.TSDisabled = !enabled;
            if (!enabled)
            {
                body.LinearVelocity = TSVector2.zero;
            }
        }

        /// <summary>把当前刚体对应的实体 ID 拷到列表并按 ID 升序排列。</summary>
        /// <param name="results">输出列表；调用前会被清空。不可为 null。</param>
        public void CopyBodyEntityIds(List<uint> results)
        {
            results.Clear();
            foreach (var pair in _bodies)
            {
                results.Add(pair.Key);
            }

            results.Sort();
        }

        /// <summary>尝试读取刚体平面坐标并写回 Transform 的 XZ。</summary>
        /// <param name="entityId">ECS 实体 ID。</param>
        /// <param name="transform">现有 Transform；Y 轴保留。</param>
        /// <returns>有刚体时返回 true。</returns>
        public bool TryWriteTransform(uint entityId, ref TransformComponent transform)
        {
            if (!_bodies.TryGetValue(entityId, out var body))
            {
                return false;
            }

            transform.Position = FromPlane(body.Position, transform.Position.y);
            return true;
        }

        /// <summary>枚举刚体当前正在接触的对方实体 ID（已按 ID 排序）。</summary>
        /// <param name="entityId">查询刚体对应的实体。</param>
        /// <param name="results">输出列表；调用前会被清空。</param>
        public void CollectTouchingEntityIds(uint entityId, List<uint> results)
        {
            results.Clear();
            if (!_bodies.TryGetValue(entityId, out var body))
            {
                return;
            }

            var edge = body.ContactList;
            while (edge != null)
            {
                if (edge.Contact != null &&
                    edge.Contact.IsTouching &&
                    edge.Other?.UserData is BattlePhysicsUserData other &&
                    other.EntityId != entityId)
                {
                    var exists = false;
                    for (var i = 0; i < results.Count; i++)
                    {
                        if (results[i] == other.EntityId)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        results.Add(other.EntityId);
                    }
                }

                edge = edge.Next;
            }

            results.Sort();
        }

        /// <summary>推进一个锁定时间步。</summary>
        /// <param name="deltaTime">步长（秒，定点）。</param>
        public void Step(FP deltaTime)
        {
            if (deltaTime > FP.Zero)
            {
                _world.Step(deltaTime);
                _stepCount++;
            }
        }

        /// <summary>
        /// 移除实体刚体；不存在时忽略。
        /// <see cref="RetainRemovedBodies"/> 为 true 时只停用并记进退役表，不移出世界。
        /// </summary>
        /// <param name="entityId">ECS 实体 ID。</param>
        public void RemoveBody(uint entityId)
        {
            if (!_bodies.TryGetValue(entityId, out var body))
            {
                return;
            }

            _bodies.Remove(entityId);
            if (body == null || body.IsDisposed)
            {
                return;
            }

            if (RetainRemovedBodies)
            {
                // 停用会销毁代理和接触，之后保存的快照里不再引用它的代理，真正移除时这些快照仍然可以恢复。
                body.LinearVelocity = TSVector2.zero;
                body.AngularVelocity = FP.Zero;
                body.Enabled = false;
                body.TSDisabled = true;
                _retired.Add(new RetiredBody(body, _stepCount));
                return;
            }

            _world.RemoveBody(body);
        }

        /// <summary>
        /// 真正移除退役步数早于 <paramref name="stepCount"/> 的刚体。
        /// 调用方要保证剩下的快照都是在 <paramref name="stepCount"/> 步或之后保存的：那时这些刚体早已停用，快照不再引用它们的代理和接触。
        /// </summary>
        /// <param name="stepCount">仍保留的最早快照的 <see cref="BattlePhysicsSnapshot.StepCount"/>。</param>
        public void PurgeRetiredBefore(int stepCount)
        {
            var kept = 0;
            for (var i = 0; i < _retired.Count; i++)
            {
                var retired = _retired[i];
                if (retired.Step < stepCount)
                {
                    if (!retired.Body.IsDisposed)
                    {
                        _world.RemoveBody(retired.Body);
                    }

                    continue;
                }

                _retired[kept++] = retired;
            }

            if (kept < _retired.Count)
            {
                _retired.RemoveRange(kept, _retired.Count - kept);
                _world.ProcessChanges();
            }
        }

        /// <summary>
        /// 保存已有刚体的运动学状态：平面位置、线速度、角速度、朝向、类型和启用标记。
        /// 不包含接触、关节和岛划分。互相推挤的刚体要用 <see cref="SaveFull"/>。
        /// Restore 要求这些实体的刚体已经存在。
        /// </summary>
        /// <param name="writer">写入器。</param>
        public void SaveKinematics(SnapshotWriter writer)
        {
            CopyBodyEntityIds(_bodyScratch);
            writer.WriteInt(1);
            writer.WriteInt(_bodyScratch.Count);
            for (var i = 0; i < _bodyScratch.Count; i++)
            {
                var entityId = _bodyScratch[i];
                var body = _bodies[entityId];
                writer.WriteUInt(entityId);
                writer.WriteFp(body.Position.x);
                writer.WriteFp(body.Position.y);
                writer.WriteFp(body.LinearVelocity.x);
                writer.WriteFp(body.LinearVelocity.y);
                writer.WriteFp(body.AngularVelocity);
                writer.WriteFp(body.Rotation);
                writer.WriteInt((int)body.BodyType);
                writer.WriteBool(body.Enabled && !body.TSDisabled);
            }
        }

        /// <summary>
        /// 把运动学状态写回已存在的刚体，不重建接触。
        /// 缺少对应刚体时抛出异常，避免静默留下旧速度。
        /// </summary>
        /// <param name="reader">读取器。</param>
        /// <exception cref="InvalidOperationException">版本不匹配或刚体不存在。</exception>
        public void RestoreKinematics(SnapshotReader reader)
        {
            var version = reader.ReadInt();
            if (version != 1)
            {
                throw new InvalidOperationException("Unsupported physics kinematic snapshot version " + version);
            }

            var count = reader.ReadInt();
            for (var i = 0; i < count; i++)
            {
                var entityId = reader.ReadUInt();
                var position = new TSVector2(reader.ReadFp(), reader.ReadFp());
                var velocity = new TSVector2(reader.ReadFp(), reader.ReadFp());
                var angular = reader.ReadFp();
                var rotation = reader.ReadFp();
                var bodyType = (BodyType)reader.ReadInt();
                var enabled = reader.ReadBool();
                if (!_bodies.TryGetValue(entityId, out var body))
                {
                    throw new InvalidOperationException("Missing physics body for entity " + entityId);
                }

                body.BodyType = bodyType;
                body.SetTransformIgnoreContacts(ref position, rotation);
                body.LinearVelocity = velocity;
                body.AngularVelocity = angular;
                body.Enabled = enabled;
                body.TSDisabled = !enabled;
            }
        }

        /// <summary>
        /// 克隆整个 Farseer 世界：刚体求解状态、接触流形与冲量、宽相、岛和 TOI。
        /// 克隆持有当时的 Fixture 引用，恢复时这些刚体实例必须还在，不能销毁后重建。
        /// 快照之后新加的刚体会在恢复时从世界里移除。快照时有、恢复前已被删掉的刚体不会被重新创建。
        /// 关节不在克隆里。会回写 Farseer 的刚体/夹具静态 ID 计数，应按整场物理世界使用。
        /// </summary>
        /// <param name="snapshot">调用方持有的快照；不可为 null。重复保存会覆盖上一次内容。</param>
        /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> 为 null。</exception>
        public void SaveFull(BattlePhysicsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            // 刚体先进入添加列表，要等 ProcessChanges 才进 BodyList。不先处理的话，第 0 帧克隆是空世界。
            _world.ProcessChanges();
            snapshot.Clone.Clone(_world, false);
            snapshot.StepCount = _stepCount;
            snapshot.Retired.Clear();
            snapshot.Retired.AddRange(_retired);
        }

        /// <summary>
        /// 用 <see cref="SaveFull"/> 的克隆覆盖当前 Farseer 世界，恢复物理步数和退役表，再按世界里的刚体重建实体到刚体的映射。
        /// 先把待添加列表收进世界，恢复时才会把快照之后才生成的刚体删掉。否则它们会先被字典忘掉，随后又进世界，变成没有实体管理的刚体。
        /// 快照之后才退役的刚体被克隆恢复成启用状态，重新回到映射里。
        /// </summary>
        /// <param name="snapshot">同一次保存得到的快照；不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> 为 null。</exception>
        public void RestoreFull(BattlePhysicsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            _world.ProcessChanges();
            snapshot.Clone.Restore(_world);
            _stepCount = snapshot.StepCount;
            _retired.Clear();
            _retiredScratch.Clear();
            for (var i = 0; i < snapshot.Retired.Count; i++)
            {
                var retired = snapshot.Retired[i];
                if (ContainsBody(retired.Body))
                {
                    _retired.Add(retired);
                    _retiredScratch.Add(retired.Body);
                }
            }

            RebuildBodyMap();
        }

        /// <summary>字典里登记、跟着实体走的刚体数。</summary>
        public int ManagedBodyCount => _bodies.Count;

        /// <summary>已经在 Farseer 世界里、会参与步进的刚体数。</summary>
        public int WorldBodyCount => _world.BodyList.Count;

        /// <summary>清空全部刚体。</summary>
        public void Clear()
        {
            _bodies.Clear();
            _retired.Clear();
            _stepCount = 0;
            _world.Clear();
        }

        /// <summary>退役刚体与退役时的物理步数。</summary>
        internal readonly struct RetiredBody
        {
            public readonly PhysicsBody Body;
            public readonly int Step;

            public RetiredBody(PhysicsBody body, int step)
            {
                Body = body;
                Step = step;
            }
        }

        PhysicsBody CreateCircle(TSVector position, FP radius, BattlePhysicsUserData data)
        {
            if (radius <= FP.Zero)
            {
                radius = BattleConstants.DefaultActorCollisionRadius;
            }

            var body = BodyFactory.CreateCircle(_world, radius, BodyDensity, ToPlane(position), data);
            body.UserData = data;
            return body;
        }

        static void ConfigureShared(PhysicsBody body)
        {
            body.IgnoreGravity = true;
            body.GravityScale = FP.Zero;
            body.FixedRotation = true;
            body.Friction = FP.Zero;
            body.Restitution = FP.Zero;
            body.LinearDamping = FP.Zero;
            body.AngularDamping = FP.Zero;
        }

        static bool FilterContact(Fixture fixtureA, Fixture fixtureB)
        {
            var a = fixtureA.Body.UserData as BattlePhysicsUserData;
            var b = fixtureB.Body.UserData as BattlePhysicsUserData;
            if (a == null || b == null)
            {
                return false;
            }

            if (a.IsProjectile == b.IsProjectile)
            {
                return !a.IsProjectile;
            }

            var projectile = a.IsProjectile ? a : b;
            var actor = a.IsProjectile ? b : a;
            return projectile.Owner != actor.ActorId && projectile.TeamId != actor.TeamId;
        }

        /// <summary>按世界里现有的刚体重建映射：带战斗标识、且不在退役表（<see cref="_retiredScratch"/>）里的刚体都算在用。</summary>
        void RebuildBodyMap()
        {
            _bodies.Clear();
            var list = _world.BodyList;
            for (var i = 0; i < list.Count; i++)
            {
                var body = list[i];
                if (body.UserData is BattlePhysicsUserData data && !_retiredScratch.Contains(body))
                {
                    _bodies[data.EntityId] = body;
                }
            }

            _retiredScratch.Clear();
        }

        bool ContainsBody(PhysicsBody body)
        {
            if (body == null)
            {
                return false;
            }

            var list = _world.BodyList;
            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], body))
                {
                    return true;
                }
            }

            return false;
        }

        static TSVector2 ToPlane(TSVector position) => new TSVector2(position.x, position.z);

        static TSVector FromPlane(TSVector2 plane, FP y) => new TSVector(plane.x, y, plane.y);
    }

    /// <summary>挂在 Farseer Body.UserData 上的战斗标识。</summary>
    public sealed class BattlePhysicsUserData
    {
        /// <summary>对应 ECS 实体 ID。</summary>
        public uint EntityId;

        /// <summary>是否为投射物刚体。</summary>
        public bool IsProjectile;

        /// <summary>Actor ID；投射物为无效值。</summary>
        public ActorId ActorId;

        /// <summary>投射物发射者；Actor 刚体为无效值。</summary>
        public ActorId Owner;

        /// <summary>队伍 ID。</summary>
        public int TeamId;
    }
}
