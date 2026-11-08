using System;
using Framework.Events;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    [TestFixture(false)]
    [TestFixture(true)]
    [NonParallelizable]
    public sealed class EventBusTests
    {
        readonly bool _global;
        IEventBus _bus;

        public EventBusTests(bool global)
        {
            _global = global;
        }

        [SetUp]
        public void SetUp()
        {
            _bus = _global ? GameEvent.Bus : new ZeroGcEventBus();
            _bus.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Clear();
        }

        struct Ping
        {
            public int Value;
        }

        [Test]
        public void RemoveThenAddDuringDispatch_KeepsHandler()
        {
            var bus = _bus;
            var calls = 0;
            Action<Ping> target = _ => calls++;
            Action<Ping> toggler = null;
            toggler = _ =>
            {
                bus.Unsubscribe(target);
                bus.Subscribe(target);
                bus.Unsubscribe(toggler);
            };

            bus.Subscribe(toggler);
            bus.Subscribe(target);
            bus.Publish(new Ping());
            Assert.AreEqual(1, calls, "最后一次操作是 Add，本轮分发里 target 仍应被调用");

            bus.Publish(new Ping());
            Assert.AreEqual(2, calls, "分发结束后 target 应仍在列表中");
        }

        [Test]
        public void AddThenRemoveDuringDispatch_DropsHandler()
        {
            var bus = _bus;
            var calls = 0;
            Action<Ping> target = _ => calls++;
            Action<Ping> toggler = null;
            toggler = _ =>
            {
                bus.Subscribe(target);
                bus.Unsubscribe(target);
                bus.Unsubscribe(toggler);
            };

            bus.Subscribe(toggler);
            bus.Publish(new Ping());
            bus.Publish(new Ping());
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void RemoveDuringDispatch_SkipsLaterHandlerInSameRound()
        {
            var bus = _bus;
            var calls = 0;
            Action<Ping> target = _ => calls++;
            bus.Subscribe<Ping>(_ => bus.Unsubscribe(target));
            bus.Subscribe(target);

            bus.Publish(new Ping { Value = 1 });
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void SubscriptionDispose_Unsubscribes()
        {
            var bus = _bus;
            var last = 0;
            var sub = bus.Subscribe<Ping>(e => last = e.Value);
            bus.Publish(new Ping { Value = 3 });
            sub.Dispose();
            bus.Publish(new Ping { Value = 9 });
            Assert.AreEqual(3, last);
        }
        [Test]
        public void ClearThenResubscribe_StaleTokenCannotRemoveNewRegistration()
        {
            var calls = 0;
            Action<Ping> handler = _ => calls++;
            var old = _bus.Subscribe(handler);
            _bus.Clear();
            var current = _bus.Subscribe(handler);

            old.Dispose();
            old.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls);

            current.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void UnsubscribeThenResubscribe_StaleTokenCannotRemoveNewRegistration()
        {
            var calls = 0;
            Action<Ping> handler = _ => calls++;
            var old = _bus.Subscribe(handler);
            _bus.Unsubscribe(handler);
            var current = _bus.Subscribe(handler);

            old.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls);

            current.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void DisposeThenResubscribe_RepeatedDisposeCannotRemoveNewRegistration()
        {
            var calls = 0;
            Action<Ping> handler = _ => calls++;
            var old = _bus.Subscribe(handler);
            old.Dispose();
            var current = _bus.Subscribe(handler);

            old.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls);

            current.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DuplicateSubscriptions_ShareOneRegistrationWithoutReferenceCounting(bool disposeFirst)
        {
            var calls = 0;
            Action<Ping> handler = _ => calls++;
            var first = _bus.Subscribe(handler);
            var second = _bus.Subscribe(handler);
            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls, "重复订阅只调用一次");

            (disposeFirst ? first : second).Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls, "任一凭证可退订，无引用计数");

            var current = _bus.Subscribe(handler);
            first.Dispose();
            second.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(2, calls, "旧重复凭证不能取消新的注册");

            current.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(2, calls);
        }

        sealed class Receiver
        {
            public int Calls;

            public void Handle(Ping evt)
            {
                Calls++;
            }
        }

        [Test]
        public void EqualDelegates_DeduplicateAndKeepRegistrationIdentity()
        {
            var receiver = new Receiver();
            var firstHandler = new Action<Ping>(receiver.Handle);
            var secondHandler = new Action<Ping>(receiver.Handle);
            Assert.AreNotSame(firstHandler, secondHandler);
            var first = _bus.Subscribe(firstHandler);
            var second = _bus.Subscribe(secondHandler);
            _bus.Publish(new Ping());
            Assert.AreEqual(1, receiver.Calls);

            _bus.Unsubscribe(secondHandler);
            var current = _bus.Subscribe(firstHandler);
            first.Dispose();
            second.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(2, receiver.Calls);

            current.Dispose();
        }

        [Test]
        public void ReplacingDuringDispatch_StaleTokenDoesNotSuppressCurrentOrNextRound()
        {
            var calls = 0;
            Action<Ping> handler = _ => calls++;
            IDisposable old = null;
            IDisposable current = null;
            Action<Ping> toggler = null;
            toggler = _ =>
            {
                _bus.Unsubscribe(handler);
                current = _bus.Subscribe(handler);
                old.Dispose();
                _bus.Unsubscribe(toggler);
            };
            _bus.Subscribe(toggler);
            old = _bus.Subscribe(handler);

            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls, "保留先退订再订阅时本轮调用的约定");
            _bus.Publish(new Ping());
            Assert.AreEqual(2, calls);
            current.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void PendingRegistration_DisposeThenReplaceIgnoresStaleToken()
        {
            var calls = 0;
            Action<Ping> handler = _ => calls++;
            IDisposable current = null;
            Action<Ping> toggler = null;
            toggler = _ =>
            {
                var old = _bus.Subscribe(handler);
                old.Dispose();
                current = _bus.Subscribe(handler);
                old.Dispose();
                _bus.Unsubscribe(toggler);
            };
            _bus.Subscribe(toggler);

            _bus.Publish(new Ping());
            Assert.AreEqual(0, calls, "新处理器延迟到最外层分发结束后注册");
            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls);
            current.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void PendingDuplicateSubscriptions_DisposeEitherCancelsRegistration()
        {
            var calls = 0;
            Action<Ping> handler = _ => calls++;
            IDisposable duplicate = null;
            Action<Ping> toggler = null;
            toggler = _ =>
            {
                var first = _bus.Subscribe(handler);
                duplicate = _bus.Subscribe(handler);
                first.Dispose();
                _bus.Unsubscribe(toggler);
            };
            _bus.Subscribe(toggler);

            _bus.Publish(new Ping());
            _bus.Publish(new Ping());
            Assert.AreEqual(0, calls);
            var current = _bus.Subscribe(handler);
            duplicate.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls);
            current.Dispose();
        }

        [Test]
        public void ClearDuringDispatch_InvalidatesCommittedAndPendingTokens()
        {
            var calls = 0;
            Action<Ping> handler = _ => calls++;
            IDisposable old = null;
            IDisposable pending = null;
            IDisposable current = null;
            _bus.Subscribe<Ping>(_ =>
            {
                old.Dispose();
                pending = _bus.Subscribe(handler);
                _bus.Clear();
                current = _bus.Subscribe(handler);
                old.Dispose();
                pending.Dispose();
            });
            old = _bus.Subscribe(handler);

            _bus.Publish(new Ping());
            Assert.AreEqual(0, calls);
            old.Dispose();
            pending.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls);
            current.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void NestedPublish_ReplacementSurvivesStaleDisposeAndWaitsForOutermostExit()
        {
            var targetCalls = 0;
            var addedCalls = 0;
            Action<Ping> handler = _ => targetCalls++;
            Action<Ping> added = _ => addedCalls++;
            IDisposable old = null;
            IDisposable current = null;
            Action<Ping> toggler = null;
            toggler = e =>
            {
                if (e.Value != 0)
                {
                    return;
                }

                old.Dispose();
                current = _bus.Subscribe(handler);
                _bus.Subscribe(added);
                old.Dispose();
                _bus.Publish(new Ping { Value = 1 });
                _bus.Unsubscribe(toggler);
            };
            _bus.Subscribe(toggler);
            old = _bus.Subscribe(handler);

            _bus.Publish(new Ping());
            Assert.AreEqual(2, targetCalls, "原有处理器在嵌套及外层分发中均按最后操作可见");
            Assert.AreEqual(0, addedCalls, "新处理器在嵌套分发中仍不可见");
            _bus.Publish(new Ping());
            Assert.AreEqual(3, targetCalls);
            Assert.AreEqual(1, addedCalls);
            current.Dispose();
        }

        [Test]
        public void CurrentReplacementToken_DisposeSuppressesCurrentAndNextRound()
        {
            var calls = 0;
            Action<Ping> handler = _ => calls++;
            Action<Ping> toggler = null;
            toggler = _ =>
            {
                _bus.Unsubscribe(handler);
                var current = _bus.Subscribe(handler);
                current.Dispose();
                _bus.Unsubscribe(toggler);
            };
            _bus.Subscribe(toggler);
            _bus.Subscribe(handler);

            _bus.Publish(new Ping());
            _bus.Publish(new Ping());
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void ClearThenNestedPublish_PreservesChannelLifetimeSemantics()
        {
            var calls = 0;
            Action<Ping> handler = _ => calls++;
            IDisposable old = null;
            IDisposable current = null;
            _bus.Subscribe<Ping>(_ =>
            {
                _bus.Clear();
                current = _bus.Subscribe(handler);
                old.Dispose();
                _bus.Publish(new Ping { Value = 1 });
            });
            old = _bus.Subscribe(handler);

            _bus.Publish(new Ping());
            var nestedCalls = _global ? 0 : 1;
            Assert.AreEqual(nestedCalls, calls, "实例总线创建新通道，全局总线复用分发中的通道");
            _bus.Publish(new Ping());
            Assert.AreEqual(nestedCalls + 1, calls);
            current.Dispose();
            _bus.Publish(new Ping());
            Assert.AreEqual(nestedCalls + 1, calls);
        }

        [Test]
        public void PublishAfterWarmup_DoesNotAllocate()
        {
            var calls = 0;
            _bus.Subscribe<Ping>(_ => calls++);
            for (var i = 0; i < 1000; i++)
            {
                _bus.Publish(new Ping());
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10000; i++)
            {
                _bus.Publish(new Ping());
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated);
            Assert.AreEqual(11000, calls);
        }

    }
}
