using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Bootstrap;
using Framework.Core;
using Framework.Events;
using Framework.Network;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>阶段 5：事件分发、模块依赖和嵌套协程的 EditMode 覆盖。</summary>
    public sealed class InfraP1Tests
    {
        struct ProbeEvent
        {
            public int Value;
        }

        [TearDown]
        public void TearDown()
        {
            GameEvent.Clear();
        }

        [Test]
        public void ThrowingListenerDoesNotBlockTheRestOrStickTheChannel()
        {
            var second = 0;
            GameEvent.Listen<ProbeEvent>(_ => throw new InvalidOperationException("boom"));
            GameEvent.Listen<ProbeEvent>(_ => second++);

            GameEvent.Send(new ProbeEvent());
            Assert.AreEqual(1, second);

            var third = 0;
            GameEvent.Listen<ProbeEvent>(_ => third++);
            GameEvent.Send(new ProbeEvent());
            Assert.AreEqual(1, third);
            Assert.AreEqual(2, second);
        }

        [Test]
        public void ReentrantPublishAppliesAddsAfterTheOuterInvoke()
        {
            var depth = 0;
            var lateCalls = 0;
            Action<ProbeEvent> late = _ => lateCalls++;
            Action<ProbeEvent> first = null;
            first = evt =>
            {
                if (depth == 0)
                {
                    depth = 1;
                    GameEvent.Listen(late);
                    GameEvent.Send(evt);
                }
            };

            GameEvent.Listen(first);
            GameEvent.Send(new ProbeEvent());
            Assert.AreEqual(0, lateCalls);

            GameEvent.Send(new ProbeEvent());
            Assert.AreEqual(1, lateCalls);
        }

        [Test]
        public void RemoveDuringInvokeSkipsThatListener()
        {
            var calls = 0;
            Action<ProbeEvent> second = null;
            Action<ProbeEvent> first = _ => GameEvent.RemoveListener(second);
            second = _ => calls++;
            GameEvent.Listen(first);
            GameEvent.Listen(second);

            GameEvent.Send(new ProbeEvent());
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void ClearDropsListeners()
        {
            var calls = 0;
            GameEvent.Listen<ProbeEvent>(_ => calls++);
            GameEvent.Clear();
            GameEvent.Send(new ProbeEvent());
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void NullDependenciesSortAsEmpty()
        {
            var module = new ProbeModule("solo", null);
            var ordered = ModuleSorter.Sort(new IGameModule[] { module });
            Assert.AreEqual(1, ordered.Count);
            Assert.AreSame(module, ordered[0]);
        }

        [Test]
        public void DuplicateDependencyDoesNotLookLikeACycle()
        {
            var first = new ProbeA();
            var second = new ProbeB();
            var ordered = ModuleSorter.Sort(new IGameModule[] { second, first });
            Assert.AreEqual(2, ordered.Count);
            Assert.AreSame(first, ordered[0]);
            Assert.AreSame(second, ordered[1]);
        }

        [Test]
        public void NestedDriveReportsChildException()
        {
            Exception caught = null;
            var driven = NestedCoroutine.Drive(Outer(), ex => caught = ex);
            while (driven.MoveNext())
            {
            }

            Assert.IsNotNull(caught);
            Assert.AreEqual("child", caught.Message);
        }

        [Test]
        public void UnregisterHandlerRemovesTheCallback()
        {
            var channel = new TcpNetworkChannel("probe", new DefaultNetworkChannelHelper());
            try
            {
                Assert.IsFalse(channel.UnregisterHandler(7));
                channel.RegisterHandler(7, (_, __) => { });
                Assert.IsTrue(channel.UnregisterHandler(7));
                Assert.IsFalse(channel.UnregisterHandler(7));
            }
            finally
            {
                channel.Shutdown();
            }
        }

        static IEnumerator Outer()
        {
            yield return Child();
        }

        static IEnumerator Child()
        {
            yield return null;
            throw new InvalidOperationException("child");
        }

        sealed class ProbeModule : IGameModule
        {
            public ProbeModule(string name, IReadOnlyList<Type> dependencies)
            {
                Name = name;
                Dependencies = dependencies;
            }

            public string Name { get; }

            public ModulePhase Phase => ModulePhase.Infrastructure;

            public IReadOnlyList<Type> Dependencies { get; }

            public void Initialize()
            {
            }

            public IEnumerator InitializeAsync()
            {
                yield break;
            }

            public void Shutdown()
            {
            }
        }

        sealed class ProbeA : IGameModule
        {
            public string Name => "a";

            public ModulePhase Phase => ModulePhase.Infrastructure;

            public IReadOnlyList<Type> Dependencies => null;

            public void Initialize()
            {
            }

            public IEnumerator InitializeAsync()
            {
                yield break;
            }

            public void Shutdown()
            {
            }
        }

        sealed class ProbeB : IGameModule
        {
            static readonly Type[] Deps = { typeof(ProbeA), typeof(ProbeA) };

            public string Name => "b";

            public ModulePhase Phase => ModulePhase.Gameplay;

            public IReadOnlyList<Type> Dependencies => Deps;

            public void Initialize()
            {
            }

            public IEnumerator InitializeAsync()
            {
                yield break;
            }

            public void Shutdown()
            {
            }
        }
    }
}
