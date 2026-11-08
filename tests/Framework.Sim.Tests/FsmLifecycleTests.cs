using System;
using System.Collections.Generic;
using Framework.Fsm;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    public sealed class FsmLifecycleTests
    {
        sealed class Owner
        {
            public readonly List<string> Trace = new List<string>();
        }

        abstract class TracedState : FsmState<Owner>
        {
            public Action<Fsm<Owner>> Enter;
            public Action<Fsm<Owner>> Exit;

            protected override void OnEnter(Fsm<Owner> fsm)
            {
                fsm.Owner.Trace.Add("enter " + GetType().Name);
                Enter?.Invoke(fsm);
            }

            protected override void OnUpdate(Fsm<Owner> fsm, float deltaTime) =>
                fsm.Owner.Trace.Add("update " + GetType().Name);

            protected override void OnExit(Fsm<Owner> fsm)
            {
                fsm.Owner.Trace.Add("exit " + GetType().Name);
                Exit?.Invoke(fsm);
            }

            protected override void OnDestroy(Fsm<Owner> fsm) =>
                fsm.Owner.Trace.Add("destroy " + GetType().Name);
        }

        sealed class A : TracedState { }
        sealed class B : TracedState { }

        sealed class Scenario
        {
            public readonly Owner Owner = new Owner();
            public readonly A A = new A();
            public readonly B B = new B();
            public readonly Fsm<Owner> Fsm;

            public Scenario() => Fsm = new Fsm<Owner>("lifecycle", Owner, A, B);
        }

        [Test]
        public void ObserverTransitionRunsAfterEntryAndStopsObsoleteNotification()
        {
            var s = new Scenario();
            var observed = new List<string>();
            s.Fsm.StateChanged += (_, next) =>
            {
                s.Owner.Trace.Add("event " + next.GetType().Name);
                if (next is A) s.Fsm.ChangeState<B>();
            };
            s.Fsm.StateChanged += (_, next) => observed.Add(next.GetType().Name);

            s.Fsm.Start<A>();

            Assert.AreSame(s.B, s.Fsm.CurrentState);
            CollectionAssert.AreEqual(new[] { "enter A", "event A", "exit A", "enter B", "event B" }, s.Owner.Trace);
            CollectionAssert.AreEqual(new[] { "B" }, observed);
        }

        [Test]
        public void ObserverDestroyNeverEntersDestroyedStateOrCallsLaterObserver()
        {
            var s = new Scenario();
            var laterCalls = 0;
            s.Fsm.StateChanged += (_, _) => s.Fsm.Destroy();
            s.Fsm.StateChanged += (_, _) => laterCalls++;

            s.Fsm.Start<A>();

            Assert.IsFalse(s.Fsm.IsRunning);
            Assert.IsNull(s.Fsm.CurrentState);
            Assert.AreEqual(0, laterCalls);
            CollectionAssert.AreEqual(new[] { "enter A", "exit A", "destroy A", "destroy B" }, s.Owner.Trace);
            Assert.Throws<InvalidOperationException>(() => s.Fsm.Start<A>());
            s.Fsm.Destroy();
            Assert.AreEqual(4, s.Owner.Trace.Count);
        }

        [Test]
        public void ObserverExceptionPropagatesAfterEntryAndLeavesTransitionUsable()
        {
            var s = new Scenario();
            var failure = new InvalidOperationException("observer");
            var laterCalls = 0;
            Action<FsmState<Owner>, FsmState<Owner>> throwing = (_, _) => throw failure;
            s.Fsm.StateChanged += throwing;
            s.Fsm.StateChanged += (_, _) => laterCalls++;

            Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(() => s.Fsm.Start<A>()));
            Assert.AreSame(s.A, s.Fsm.CurrentState);
            CollectionAssert.AreEqual(new[] { "enter A" }, s.Owner.Trace);
            Assert.AreEqual(0, laterCalls);

            s.Fsm.StateChanged -= throwing;
            s.Fsm.ChangeState<B>();
            CollectionAssert.AreEqual(new[] { "enter A", "exit A", "enter B" }, s.Owner.Trace);
            Assert.AreEqual(1, laterCalls);
        }

        [Test]
        public void EntryCanImmediatelyTransitionWithoutObsoleteNotification()
        {
            var s = new Scenario();
            s.A.Enter = fsm => fsm.ChangeState<B>();
            var observed = new List<FsmState<Owner>>();
            s.Fsm.StateChanged += (previous, next) =>
            {
                Assert.AreSame(s.A, previous);
                Assert.AreSame(next, s.Fsm.CurrentState);
                observed.Add(next);
            };

            s.Fsm.Start<A>();

            CollectionAssert.AreEqual(new[] { s.B }, observed);
            CollectionAssert.AreEqual(new[] { "enter A", "exit A", "enter B" }, s.Owner.Trace);
        }

        [Test]
        public void EntryCanDestroyWithoutNotification()
        {
            var s = new Scenario();
            s.A.Enter = fsm => fsm.Destroy();
            var calls = 0;
            s.Fsm.StateChanged += (_, _) => calls++;

            s.Fsm.Start<A>();

            Assert.AreEqual(0, calls);
            Assert.IsFalse(s.Fsm.IsRunning);
            CollectionAssert.AreEqual(new[] { "enter A", "exit A", "destroy A", "destroy B" }, s.Owner.Trace);
        }

        [Test]
        public void EntryRoundTripSuppressesEarlierEntryOfSameInstance()
        {
            var s = new Scenario();
            s.A.Enter = fsm =>
            {
                s.A.Enter = null;
                fsm.ChangeState<B>();
            };
            s.B.Enter = fsm => fsm.ChangeState<A>();
            var observed = new List<FsmState<Owner>>();
            s.Fsm.StateChanged += (_, next) => observed.Add(next);

            s.Fsm.Start<A>();

            CollectionAssert.AreEqual(new[] { s.A }, observed);
            CollectionAssert.AreEqual(new[] { "enter A", "exit A", "enter B", "exit B", "enter A" }, s.Owner.Trace);
        }

        [Test]
        public void ObserverRoundTripSuppressesEarlierEntryOfSameInstance()
        {
            var s = new Scenario();
            var roundTrip = true;
            s.Fsm.StateChanged += (_, next) =>
            {
                if (!(next is A) || !roundTrip) return;
                roundTrip = false;
                s.Fsm.ChangeState<B>();
                s.Fsm.ChangeState<A>();
            };
            var observed = new List<FsmState<Owner>>();
            s.Fsm.StateChanged += (_, next) => observed.Add(next);

            s.Fsm.Start<A>();

            CollectionAssert.AreEqual(new FsmState<Owner>[] { s.B, s.A }, observed);
        }

        [Test]
        public void SameStateReentrySuppressesOuterNotificationAndResetsTime()
        {
            var s = new Scenario();
            var reenter = true;
            s.Fsm.StateChanged += (_, _) =>
            {
                if (!reenter) return;
                reenter = false;
                s.Fsm.Update(1f);
                s.Fsm.ChangeState<A>();
            };
            var calls = 0;
            s.Fsm.StateChanged += (_, _) => calls++;

            s.Fsm.Start<A>();

            Assert.AreEqual(1, calls);
            Assert.AreEqual(0f, s.Fsm.CurrentStateTime);
            CollectionAssert.AreEqual(new[] { "enter A", "update A", "exit A", "enter A" }, s.Owner.Trace);
        }

        [Test]
        public void ObserverSubscriptionsUseSnapshotTakenAfterEntry()
        {
            var s = new Scenario();
            var observed = new List<string>();
            Action<FsmState<Owner>, FsmState<Owner>> removed = (_, _) => observed.Add("removed");
            Action<FsmState<Owner>, FsmState<Owner>> added = (_, _) => observed.Add("added");
            s.Fsm.StateChanged += (_, _) =>
            {
                observed.Add("first");
                s.Fsm.StateChanged -= removed;
                s.Fsm.StateChanged += added;
            };
            s.A.Enter = fsm => fsm.StateChanged += removed;

            s.Fsm.Start<A>();
            CollectionAssert.AreEqual(new[] { "first", "removed" }, observed);

            observed.Clear();
            s.Fsm.ChangeState<B>();
            CollectionAssert.AreEqual(new[] { "first", "added" }, observed);
        }

        [Test]
        public void EntryFailureCommitsNewStateWithoutNotification()
        {
            var s = new Scenario();
            var failure = new InvalidOperationException("enter");
            s.A.Enter = _ => throw failure;
            var calls = 0;
            s.Fsm.StateChanged += (_, _) => calls++;

            Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(() => s.Fsm.Start<A>()));
            Assert.AreSame(s.A, s.Fsm.CurrentState);
            Assert.AreEqual(0, calls);
            s.Fsm.ChangeState<B>();
            Assert.AreEqual(1, calls);
            CollectionAssert.AreEqual(new[] { "enter A", "exit A", "enter B" }, s.Owner.Trace);
        }

        [Test]
        public void CaughtEntryFailureStillInvalidatesOuterNotification()
        {
            var s = new Scenario();
            s.B.Enter = _ => throw new InvalidOperationException("enter B");
            s.Fsm.StateChanged += (_, next) =>
            {
                if (next is A) Assert.Throws<InvalidOperationException>(() => s.Fsm.ChangeState<B>());
            };
            var laterCalls = 0;
            s.Fsm.StateChanged += (_, _) => laterCalls++;

            s.Fsm.Start<A>();

            Assert.AreSame(s.B, s.Fsm.CurrentState);
            Assert.AreEqual(0, laterCalls);
            CollectionAssert.AreEqual(new[] { "enter A", "exit A", "enter B" }, s.Owner.Trace);
        }

        [Test]
        public void ExitFailureAbortsTransitionAndRestoresGuard()
        {
            var s = new Scenario();
            var failure = new InvalidOperationException("exit");
            s.A.Exit = _ => throw failure;
            s.Fsm.Start<A>();
            s.Fsm.Update(0.5f);

            Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(() => s.Fsm.ChangeState<B>()));
            Assert.AreSame(s.A, s.Fsm.CurrentState);
            Assert.AreEqual(0.5f, s.Fsm.CurrentStateTime);
            s.A.Exit = null;
            s.Fsm.ChangeState<B>();
            Assert.AreSame(s.B, s.Fsm.CurrentState);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DestroyInsideExitIsRejectedWithoutRecursiveExit(bool destroying)
        {
            var s = new Scenario();
            var exitCalls = 0;
            s.A.Exit = fsm =>
            {
                // A bounded callback makes the original recursive-destruction bug reproducible without stack overflow.
                if (++exitCalls == 1) fsm.Destroy();
            };
            s.Fsm.Start<A>();

            Assert.Throws<InvalidOperationException>(() =>
            {
                if (destroying) s.Fsm.Destroy();
                else s.Fsm.ChangeState<B>();
            });

            Assert.AreEqual(1, exitCalls);
            Assert.AreSame(s.A, s.Fsm.CurrentState);
            Assert.IsTrue(s.Fsm.IsRunning);
            s.A.Exit = null;
            s.Fsm.ChangeState<B>();
            s.Fsm.Destroy();
            Assert.IsFalse(s.Fsm.IsRunning);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UpdateInsideExitIsIgnored(bool destroying)
        {
            var s = new Scenario();
            s.Fsm.Start<A>();
            s.Fsm.Update(0.5f);
            s.Owner.Trace.Clear();
            s.A.Exit = fsm =>
            {
                fsm.Update(10f);
                Assert.AreEqual(0.5f, fsm.CurrentStateTime);
                CollectionAssert.AreEqual(new[] { "exit A" }, s.Owner.Trace);
            };

            if (destroying) s.Fsm.Destroy();
            else s.Fsm.ChangeState<B>();
        }
    }
}
