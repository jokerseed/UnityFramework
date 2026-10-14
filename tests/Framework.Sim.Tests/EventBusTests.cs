using System;
using Framework.Events;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    public sealed class EventBusTests
    {
        struct Ping
        {
            public int Value;
        }

        [Test]
        public void RemoveThenAddDuringDispatch_KeepsHandler()
        {
            var bus = new ZeroGcEventBus();
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
            var bus = new ZeroGcEventBus();
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
            var bus = new ZeroGcEventBus();
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
            var bus = new ZeroGcEventBus();
            var last = 0;
            var sub = bus.Subscribe<Ping>(e => last = e.Value);
            bus.Publish(new Ping { Value = 3 });
            sub.Dispose();
            bus.Publish(new Ping { Value = 9 });
            Assert.AreEqual(3, last);
        }
    }
}
