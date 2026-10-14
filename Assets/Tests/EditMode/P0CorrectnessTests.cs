using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using BtTree = Framework.BehaviourTree.BehaviourTree;
using Cysharp.Threading.Tasks;
using Framework.BehaviourTree;
using Framework.Bootstrap;
using Framework.Battle;
using Framework.Core;
using Framework.ECS;
using Framework.Events;
using Framework.FixedMath;
using Framework.GAS;
using Framework.GAS.Combat;
using Framework.GAS.Tags;
using Framework.MemoryPool;
using MemPool = Framework.MemoryPool.MemoryPool;
using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Framework.Tests
{
    /// <summary>阶段 1 的 P0：伤害为 0、空间查询、冷却、启动失败、单例退出标志、内存池锁。</summary>
    public sealed class P0CorrectnessTests
    {
        [Test]
        public void ZeroPipelineDamageStaysZero()
        {
            var raw = new DamageContext(new ActorId(1), new ActorId(2), (FP)8, "hit");
            Assert.AreEqual((FP)8, raw.FinalDamage);

            var blocked = raw.WithPipelineResult(FP.Zero, false, FP.Zero);
            Assert.AreEqual(FP.Zero, blocked.FinalDamage);
            Assert.AreEqual(FP.Zero, raw.WithFinalDamage(FP.Zero).FinalDamage);
        }

        [Test]
        public void ImmuneArmorShieldAndDeadDoNotDealHealthDamage()
        {
            var bus = new ZeroGcEventBus();
            var immune = CombatTarget();
            immune.Tags.AddTag(new GameplayTag(BattleConstants.TagImmuneDamage));
            Assert.IsFalse(immune.ApplyDamage(Hit((FP)8), bus));
            Assert.AreEqual((FP)100, Health(immune));

            var armored = CombatTarget();
            armored.Attributes.GetOrCreate(BattleConstants.Defense).SetCurrentValue((FP)10);
            Assert.IsFalse(armored.ApplyDamage(Hit((FP)5), bus));
            Assert.AreEqual((FP)100, Health(armored));

            var shielded = CombatTarget();
            shielded.Attributes.GetOrCreate(BattleConstants.Shield).SetCurrentValue((FP)20);
            Assert.IsTrue(shielded.ApplyDamage(Hit((FP)8), bus));
            Assert.AreEqual((FP)100, Health(shielded));
            Assert.AreEqual((FP)12, shielded.Attributes.GetCurrentValue(BattleConstants.Shield));

            var dead = CombatTarget();
            dead.Tags.AddTag(new GameplayTag(BattleConstants.TagDead));
            Assert.IsFalse(dead.ApplyDamage(Hit((FP)8), bus));
            Assert.AreEqual((FP)100, Health(dead));
        }

        [Test]
        public void CeilingRoundsAwayFromZeroForPositiveFractions()
        {
            Assert.AreEqual((FP)2, TSMath.Ceiling((FP)3 / 2));
            Assert.AreEqual((FP)2, TSMath.Ceiling((FP)2));
            Assert.AreEqual((FP)(-1), TSMath.Ceiling((FP)(-3) / 2));
            Assert.AreEqual((FP)(-2), TSMath.Ceiling((FP)(-2)));
            Assert.AreEqual(FP.Zero, TSMath.Ceiling(FP.Zero));
        }

        [Test]
        public void QueryNearbyIncludesTargetsAcrossACellBoundary()
        {
            var grid = new SpatialHashGrid((FP)2);
            var origin = new TSVector((FP)19 / 10, FP.Zero, FP.Zero);
            grid.Insert(1, origin);
            grid.Insert(2, new TSVector((FP)21 / 10, FP.Zero, FP.Zero));
            grid.Insert(3, new TSVector((FP)49 / 10, FP.Zero, FP.Zero));
            grid.Insert(4, new TSVector((FP)(-1), FP.Zero, (FP)(-1)));

            var near = grid.QueryNearby(origin, (FP)1 / 2);
            Assert.IsTrue(Contains(near, 2), "隔着格子边界、距离 0.2 的目标应被半径 0.5 的查询覆盖。");
            Assert.IsFalse(Contains(near, 3));

            var wide = grid.QueryNearby(origin, (FP)3);
            Assert.IsTrue(Contains(wide, 3), "半径 3、原点靠近格子右侧时，+2 格内的目标不能丢。");

            var points = new[]
            {
                new TSVector(FP.Zero, FP.Zero, FP.Zero),
                new TSVector((FP)3, FP.Zero, (FP)(-1)),
                new TSVector((FP)(-4), FP.Zero, (FP)2),
                origin
            };
            var ids = new uint[] { 10, 11, 12, 13 };
            for (var i = 0; i < points.Length; i++)
            {
                grid.Insert(ids[i], points[i]);
            }

            var radius = (FP)3;
            var found = grid.QueryNearby(origin, radius);
            var radiusSq = radius * radius;
            for (var i = 0; i < points.Length; i++)
            {
                var delta = points[i] - origin;
                var distSq = delta.x * delta.x + delta.z * delta.z;
                if (distSq <= radiusSq)
                {
                    Assert.IsTrue(Contains(found, ids[i]));
                }
            }
        }

        [Test]
        public void CooldownSurvivesParentReset()
        {
            var hits = 0;
            var action = new BtAction(_ =>
            {
                hits++;
                return BtStatus.Success;
            });
            var selector = new BtSelector();
            selector.AddChild(new BtCooldown(action, FP.One));
            var tree = new BtTree(selector);
            var context = new BtContext(new BtBlackboard());
            context.DeltaTime = (FP)1 / 2;

            Assert.AreEqual(BtStatus.Success, tree.Tick(context));
            Assert.AreEqual(1, hits);
            Assert.AreEqual(BtStatus.Failure, tree.Tick(context));
            Assert.AreEqual(1, hits);
            Assert.AreEqual(BtStatus.Success, tree.Tick(context));
            Assert.AreEqual(2, hits);
        }

        [Test]
        public void SyncInitFailureRaisesFailedAndShutsDownPreviousModule()
        {
            var previous = new CountingModule("previous");
            var failing = new ThrowingModule("failing", ModuleInitMode.Synchronous, typeof(CountingModule));
            var go = new GameObject("bootstrap-sync-fail");
            try
            {
                var bootstrap = go.AddComponent<GameBootstrap>();
                Exception reported = null;
                var group = bootstrap.SetModules("p0-sync", new IGameModule[] { previous, failing });
                group.Failed += (_, ex) => reported = ex;
                LogAssert.Expect(LogType.Error, new Regex("failing"));
                var run = bootstrap.RunAsync("p0-sync");
                Assert.AreEqual(UniTaskStatus.Succeeded, run.Status, "同步模块失败应在调用当帧结束。");
                Assert.AreEqual(ModuleGroupState.Failed, group.State);
                Assert.IsNotNull(reported);
                Assert.AreEqual(1, previous.ShutdownCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [UnityTest]
        public IEnumerator AsyncInitFailureRaisesFailed() => UniTask.ToCoroutine(async () =>
        {
            var failing = new ThrowingModule("async-fail", ModuleInitMode.Asynchronous);
            var go = new GameObject("bootstrap-async-fail");
            try
            {
                var bootstrap = go.AddComponent<GameBootstrap>();
                Exception reported = null;
                var group = bootstrap.SetModules("p0-async", new IGameModule[] { failing });
                group.Failed += (_, ex) => reported = ex;
                LogAssert.Expect(LogType.Error, new Regex("async-fail"));
                await bootstrap.RunAsync("p0-async");
                Assert.AreEqual(ModuleGroupState.Failed, group.State);
                Assert.AreEqual("async-fail", reported.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        });

        [Test]
        public void QuittingFlagCanBeClearedWithoutDomainReload()
        {
            // EditMode 下 AddComponent 不会调用 Awake，DestroyImmediate 也不会调用 OnDestroy，
            // 所以这里走懒创建路径，并用 Unity 的 == null（已销毁即为 null）判断。
            QuitProbeSingleton.ResetStaticState();
            var probe = QuitProbeSingleton.Instance;
            Assert.IsTrue(QuitProbeSingleton.HasInstance);
            probe.MarkQuitting();
            UnityEngine.Object.DestroyImmediate(probe.gameObject);
            Assert.IsTrue(QuitProbeSingleton.Instance == null, "退出阶段不应重建单例。");

            QuitProbeSingleton.ResetStaticState();
            var recreated = QuitProbeSingleton.Instance;
            Assert.IsTrue(recreated != null, "清掉退出标志后应能重建单例。");
            UnityEngine.Object.DestroyImmediate(recreated.gameObject);
            QuitProbeSingleton.ResetStaticState();
        }

        [Test]
        public void MemoryPoolSurvivesConcurrentAcquireAndRelease()
        {
            var previous = MemPool.EnableStrictCheck;
            MemPool.EnableStrictCheck = false;
            MemPool.ClearAll();
            var errors = 0;
            var gate = new object();
            var threads = new Thread[4];
            for (var t = 0; t < threads.Length; t++)
            {
                threads[t] = new Thread(() =>
                {
                    try
                    {
                        for (var i = 0; i < 400; i++)
                        {
                            var item = MemPool.Acquire<PoolProbe>();
                            item.Value = i;
                            MemPool.Release(item);
                        }
                    }
                    catch (Exception)
                    {
                        lock (gate)
                        {
                            errors++;
                        }
                    }
                });
            }

            for (var t = 0; t < threads.Length; t++)
            {
                threads[t].Start();
            }

            for (var t = 0; t < threads.Length; t++)
            {
                threads[t].Join();
            }

            Assert.AreEqual(0, errors);
            var infos = MemPool.GetAllInfos();
            for (var i = 0; i < infos.Length; i++)
            {
                if (infos[i].Type == typeof(PoolProbe))
                {
                    Assert.AreEqual(0, infos[i].UsingCount);
                }
            }

            MemPool.ClearAll();
            MemPool.EnableStrictCheck = previous;
        }

        static AbilitySystemComponent CombatTarget()
        {
            var asc = new AbilitySystemComponent(new ActorId(2));
            asc.InitializeCombatAttributes((FP)100, (FP)50);
            return asc;
        }

        static DamageContext Hit(FP amount) =>
            new DamageContext(new ActorId(1), new ActorId(2), amount, "hit");

        static FP Health(AbilitySystemComponent asc) =>
            asc.Attributes.GetCurrentValue(BattleConstants.Health);

        static bool Contains(IReadOnlyList<uint> ids, uint id)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        sealed class CountingModule : IGameModule
        {
            public CountingModule(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public ModulePhase Phase => ModulePhase.Infrastructure;
            public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();
            public int ShutdownCount { get; private set; }
            public void Initialize()
            {
            }

            public void Shutdown() => ShutdownCount++;
        }

        sealed class ThrowingModule : IGameModule
        {
            readonly ModuleInitMode _mode;

            public ThrowingModule(string name, ModuleInitMode mode, Type dependency = null)
            {
                Name = name;
                _mode = mode;
                Dependencies = dependency == null ? Array.Empty<Type>() : new[] { dependency };
            }

            public string Name { get; }
            public ModulePhase Phase => ModulePhase.Infrastructure;
            public IReadOnlyList<Type> Dependencies { get; }
            public ModuleInitMode InitMode => _mode;

            public void Initialize() => throw new InvalidOperationException(Name);

            public async UniTask InitializeAsync(CancellationToken cancellationToken)
            {
                await UniTask.Yield();
                throw new InvalidOperationException(Name);
            }

            public void Shutdown()
            {
            }
        }

        sealed class PoolProbe : IMemory
        {
            public int Value;

            public void Clear()
            {
                Value = 0;
            }
        }
    }

    /// <summary>供单例退出标志测试挂到 GameObject 上。不能写成测试类的嵌套类型。</summary>
    public sealed class QuitProbeSingleton : PersistentSingleton<QuitProbeSingleton>
    {
        /// <summary>模拟应用退出，置位静态退出标志。</summary>
        public void MarkQuitting() => OnApplicationQuit();
    }
}
