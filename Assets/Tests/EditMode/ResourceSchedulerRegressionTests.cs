using System;
using Framework.Res;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    public sealed class ResourceSchedulerRegressionTests
    {
        static ResourceScheduler Create(int callbackLimit = 1)
        {
            return new ResourceScheduler(new ResourceSchedulerOptions
            {
                MaxFrameBudgetMs = 10000, CallbackBudgetMs = 10000,
                MaxLoadStartsPerFrame = 100, MaxCallbacksPerFrame = callbackLimit,
            }, (location, type) => throw new InvalidOperationException("Expected load failure"), () => null);
        }

        [Test]
        public void MergedFailuresDispatchOneWaiterPerBudget()
        {
            var scheduler = Create();
            var callbacks = 0;
            var first = scheduler.EnqueueLoad("asset", typeof(GameObject), _ => callbacks++, 0);
            var second = scheduler.EnqueueLoad("asset", typeof(GameObject), _ => callbacks++, 0);
            scheduler.Tick();
            Assert.AreEqual(1, callbacks);
            Assert.AreEqual(ResourceRequestStatus.Failed, first.Status);
            Assert.IsFalse(second.IsDone);
            scheduler.Tick();
            Assert.AreEqual(2, callbacks);
            Assert.AreEqual(ResourceRequestStatus.Failed, second.Status);
        }

        [Test]
        public void CallbackTimeBudgetStopsBetweenMergedWaiters()
        {
            var scheduler = new ResourceScheduler(new ResourceSchedulerOptions
            {
                MaxFrameBudgetMs = 10000, CallbackBudgetMs = 10, MaxCallbacksPerFrame = 100,
            }, (location, type) => throw new InvalidOperationException("Expected load failure"), () => null);
            scheduler.EnqueueLoad("asset", typeof(GameObject), _ => System.Threading.Thread.Sleep(30), 0);
            var second = scheduler.EnqueueLoad("asset", typeof(GameObject), null, 0);
            scheduler.Tick();
            Assert.IsFalse(second.IsDone);
            scheduler.Tick();
            Assert.IsTrue(second.IsDone);
        }

        [Test]
        public void CompletedWaiterCanBeCancelledBetweenFrames()
        {
            var scheduler = Create();
            scheduler.EnqueueLoad("asset", typeof(GameObject), null, 0);
            var second = scheduler.EnqueueLoad("asset", typeof(GameObject), _ => Assert.Fail("Cancelled callback"), 0);
            scheduler.Tick();
            second.Cancel();
            scheduler.Tick();
            Assert.AreEqual(ResourceRequestStatus.Cancelled, second.Status);
        }

        [Test]
        public void CallbackCanCancelAnotherMergedWaiter()
        {
            var scheduler = Create(100);
            ResourceRequestHandle second = null;
            var callbacks = 0;
            scheduler.EnqueueLoad("asset", typeof(GameObject), _ => second.Cancel(), 0);
            second = scheduler.EnqueueLoad("asset", typeof(GameObject), _ => callbacks++, 0);
            scheduler.Tick();
            Assert.AreEqual(ResourceRequestStatus.Cancelled, second.Status);
            Assert.AreEqual(0, callbacks);
        }

        [Test]
        public void ShutdownFromCallbackCancelsRemainingWaitersAndRejectsNewWork()
        {
            var scheduler = Create(100);
            scheduler.EnqueueLoad("asset", typeof(GameObject), _ => scheduler.Shutdown(), 0);
            var remaining = scheduler.EnqueueLoad("asset", typeof(GameObject), _ => Assert.Fail("Cancelled callback"), 0);
            scheduler.Tick();
            Assert.AreEqual(ResourceRequestStatus.Cancelled, remaining.Status);
            Assert.AreEqual(ResourceRequestStatus.Cancelled,
                scheduler.EnqueueLoad("new", typeof(GameObject), null, 0).Status);
            Assert.AreEqual(ResourceRequestStatus.Cancelled,
                scheduler.EnqueueInstantiate(default, null, false, null, 0).Status);
        }

        [Test]
        public void CallbackCanEnqueueSameKeyWithoutLosingIt()
        {
            var scheduler = Create();
            ResourceRequestHandle added = null;
            scheduler.EnqueueLoad("asset", typeof(GameObject), _ =>
                added = scheduler.EnqueueLoad("asset", typeof(GameObject), null, 0), 0);
            var second = scheduler.EnqueueLoad("asset", typeof(GameObject), null, 0);
            scheduler.Tick();
            second.Cancel();
            scheduler.Tick();
            Assert.IsTrue(added.IsDone);
        }
    }
}
