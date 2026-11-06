using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.TestTools;
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
        public void FailedInitializationCleanupStopsWithManagerLifetime()
        {
            var host = new GameObject("resource-cleanup-stop-test");
            try
            {
                var manager = host.AddComponent<ResourceManager>();
                var wait = manager.WaitCleanupOperation(new PendingOperation());
                Assert.IsTrue(wait.MoveNext());
                manager.Shutdown();
                Assert.IsFalse(wait.MoveNext());
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [UnityTest]
        public IEnumerator InitializationWaitStopsEvenIfYooOperationNeverCompletes() => UniTask.ToCoroutine(async () =>
        {
            var host = new GameObject("resource-initialize-stop-test");
            try
            {
                var manager = host.AddComponent<ResourceManager>();
                var wait = manager.WaitInitializeOperation(new PendingOperation(), 0, CancellationToken.None);
                manager.Shutdown();
                try
                {
                    await wait;
                    Assert.Fail("Expected stopped initialization to be cancelled.");
                }
                catch (OperationCanceledException) { }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        });

        [UnityTest]
        public IEnumerator PreCancelledInitializationDoesNotChangeManagerState() => UniTask.ToCoroutine(async () =>
        {
            var host = new GameObject("resource-pre-cancel-test");
            try
            {
                var manager = host.AddComponent<ResourceManager>();
                var initialized = typeof(ResourceManager).GetField("_initialized",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                initialized.SetValue(manager, true);
                try
                {
                    await manager.InitializeAsync(new ResourceInitOptions(), new CancellationToken(true));
                    Assert.Fail("Expected cancellation.");
                }
                catch (OperationCanceledException) { }
                Assert.IsTrue(manager.IsInitialized);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        });

        sealed class PendingOperation : YooAsset.AsyncOperationBase
        {
            protected override void InternalStart() { }
            protected override void InternalUpdate() { }
        }

        [Test]
        public void RecursiveTickCannotBypassCallbackBudget()
        {
            var scheduler = Create();
            scheduler.EnqueueLoad("asset", typeof(GameObject), _ => scheduler.Tick(), 0);
            var second = scheduler.EnqueueLoad("asset", typeof(GameObject), null, 0);
            scheduler.Tick();
            Assert.IsFalse(second.IsDone, "A recursive Tick must share the outer frame budget.");
            scheduler.Tick();
            Assert.IsTrue(second.IsDone);
        }

        [UnityTest]
        public IEnumerator WorkerCancellationIsAppliedOnPlayerLoop() => UniTask.ToCoroutine(async () =>
        {
            var scheduler = Create();
            var request = scheduler.EnqueueLoad("asset", typeof(GameObject), _ => Assert.Fail("Cancelled callback"), 0);
            using (var cancellation = new CancellationTokenSource())
            {
                var wait = ResourceManager.WaitRequest(request, cancellation.Token);
                // Blocking here is intentional: no player-loop continuation can run while
                // the worker cancels, so this detects synchronous off-thread queue mutation.
                Assert.IsTrue(Task.Run(() => cancellation.Cancel()).Wait(3000));
                Assert.AreEqual(ResourceRequestStatus.Pending, request.Status);
                await wait;
                Assert.AreEqual(ResourceRequestStatus.Cancelled, request.Status);
                Assert.AreEqual(0, scheduler.PendingLoadCount);
            }
        });

        [UnityTest]
        public IEnumerator ShutdownUnblocksRequestWaiter() => UniTask.ToCoroutine(async () =>
        {
            var scheduler = Create();
            var request = scheduler.EnqueueLoad("asset", typeof(GameObject), null, 0);
            var wait = ResourceManager.WaitRequest(request, CancellationToken.None);
            scheduler.Shutdown();
            await wait;
            Assert.AreEqual(ResourceRequestStatus.Cancelled, request.Status);
        });

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
