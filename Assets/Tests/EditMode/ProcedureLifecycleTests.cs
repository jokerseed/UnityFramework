using System;
using System.Collections.Generic;
using Framework.Fsm;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    public sealed class ProcedureLifecycleTests
    {
        GameObject _host;
        ProcedureManager _manager;
        A _a;
        B _b;
        readonly List<string> _trace = new List<string>();

        abstract class TracedProcedure : ProcedureBase
        {
            public List<string> Trace;
            public Action<Fsm<ProcedureManager>> Enter;

            protected override void OnEnter(Fsm<ProcedureManager> fsm)
            {
                Trace.Add("enter " + GetType().Name);
                Enter?.Invoke(fsm);
            }

            protected override void OnExit(Fsm<ProcedureManager> fsm) => Trace.Add("exit " + GetType().Name);
            protected override void OnDestroy(Fsm<ProcedureManager> fsm) => Trace.Add("destroy " + GetType().Name);
        }

        sealed class A : TracedProcedure { }
        sealed class B : TracedProcedure { }

        [SetUp]
        public void SetUp()
        {
            _trace.Clear();
            _host = new GameObject("procedure-lifecycle-test");
            _manager = _host.AddComponent<ProcedureManager>();
            _a = new A { Trace = _trace };
            _b = new B { Trace = _trace };
            _manager.Initialize(_a, _b);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_host);

        [Test]
        public void ObserverTransitionsAfterEntryAndStopsOldNotification()
        {
            _manager.ProcedureChanged += (_, next) =>
            {
                _trace.Add("event " + next.GetType().Name);
                if (next is A) _manager.ChangeProcedure<B>();
            };
            var observed = new List<ProcedureBase>();
            _manager.ProcedureChanged += (_, next) => observed.Add(next);

            _manager.StartProcedure<A>();

            Assert.AreSame(_b, _manager.CurrentProcedure);
            CollectionAssert.AreEqual(new[] { _b }, observed);
            CollectionAssert.AreEqual(new[] { "enter A", "event A", "exit A", "enter B", "event B" }, _trace);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ObserverReturningToSameInstanceStopsOldNotification(bool roundTrip)
        {
            var reenter = true;
            _manager.ProcedureChanged += (_, next) =>
            {
                if (!(next is A) || !reenter) return;
                reenter = false;
                if (roundTrip) _manager.ChangeProcedure<B>();
                _manager.ChangeProcedure<A>();
            };
            var observed = new List<ProcedureBase>();
            _manager.ProcedureChanged += (_, next) => observed.Add(next);

            _manager.StartProcedure<A>();

            CollectionAssert.AreEqual(roundTrip ? new ProcedureBase[] { _b, _a } : new ProcedureBase[] { _a }, observed);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ObserverShutdownStopsOldNotificationEvenAfterReinitialization(bool restart)
        {
            var shutdown = true;
            var observed = new List<ProcedureBase>();
            var replacement = new A { Trace = _trace };
            _manager.ProcedureChanged += (_, _) =>
            {
                if (!shutdown) return;
                shutdown = false;
                _manager.Shutdown();
                if (!restart) return;
                _manager.Initialize(replacement);
                _manager.StartProcedure<A>();
            };
            _manager.ProcedureChanged += (_, next) => observed.Add(next);

            _manager.StartProcedure<A>();

            CollectionAssert.AreEqual(restart ? new ProcedureBase[] { replacement } : Array.Empty<ProcedureBase>(), observed);
            Assert.AreEqual(restart, _manager.IsInitialized);
            Assert.AreSame(restart ? replacement : null, _manager.CurrentProcedure);
            CollectionAssert.AreEqual(restart
                ? new[] { "enter A", "exit A", "destroy A", "destroy B", "enter A" }
                : new[] { "enter A", "exit A", "destroy A", "destroy B" }, _trace);
        }

        [Test]
        public void EntryCanImmediatelyTransitionAndOnlyNotifyCurrentEntry()
        {
            _a.Enter = fsm => fsm.Owner.ChangeProcedure<B>();
            var observed = new List<ProcedureBase>();
            _manager.ProcedureChanged += (_, next) => observed.Add(next);

            _manager.StartProcedure<A>();

            CollectionAssert.AreEqual(new[] { _b }, observed);
            CollectionAssert.AreEqual(new[] { "enter A", "exit A", "enter B" }, _trace);
        }

        [Test]
        public void CaughtEntryFailureStopsOldNotificationWithoutNewEvent()
        {
            _b.Enter = _ => throw new InvalidOperationException("enter B");
            _manager.ProcedureChanged += (_, next) =>
            {
                if (next is A) Assert.Throws<InvalidOperationException>(() => _manager.ChangeProcedure<B>());
            };
            var laterCalls = 0;
            _manager.ProcedureChanged += (_, _) => laterCalls++;

            _manager.StartProcedure<A>();

            Assert.AreSame(_b, _manager.CurrentProcedure);
            Assert.AreEqual(0, laterCalls);
            CollectionAssert.AreEqual(new[] { "enter A", "exit A", "enter B" }, _trace);
        }

        [Test]
        public void ObserverFailureDoesNotSkipEntryAndManagerCanContinue()
        {
            var failure = new InvalidOperationException("procedure observer");
            Action<ProcedureBase, ProcedureBase> throwing = (_, _) => throw failure;
            _manager.ProcedureChanged += throwing;

            Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(() => _manager.StartProcedure<A>()));
            CollectionAssert.AreEqual(new[] { "enter A" }, _trace);
            Assert.AreSame(_a, _manager.CurrentProcedure);

            _manager.ProcedureChanged -= throwing;
            _manager.ChangeProcedure<B>();
            CollectionAssert.AreEqual(new[] { "enter A", "exit A", "enter B" }, _trace);
        }
    }
}
