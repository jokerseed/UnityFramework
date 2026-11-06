using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Bootstrap;
using Framework.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Framework.Tests
{
    public sealed class BootstrapLifecycleTests
    {
        GameObject _host;
        GameBootstrap _bootstrap;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Bootstrap lifecycle test");
            _bootstrap = _host.AddComponent<GameBootstrap>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void ObserverFailuresDoNotRollbackOrSkipOtherObservers()
        {
            var module = new Provider();
            var group = _bootstrap.SetModules("a", new[] { module });
            var ready = 0;
            group.StateChanged += _ => throw new InvalidOperationException("observer-state");
            group.ProgressChanged += (_, current, total) => throw new InvalidOperationException("observer-progress");
            group.Ready += _ => throw new InvalidOperationException("observer-ready");
            group.Ready += _ => ready++;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                _bootstrap.RunAsync("a").GetAwaiter().GetResult();
                Assert.IsTrue(group.IsReady);
                Assert.AreEqual(1, ready);
                Assert.AreEqual(0, module.ShutdownCount);
            }
            finally { LogAssert.ignoreFailingMessages = false; }
        }

        [Test]
        public void FailedObserverCannotEscapeRunOrHideOriginalFailure()
        {
            var group = _bootstrap.SetModules("a", new IGameModule[] { new Broken() });
            var failures = 0;
            group.Failed += (_, error) => throw new InvalidOperationException("observer-failed");
            group.Failed += (_, error) => failures++;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                Assert.DoesNotThrow(() => _bootstrap.RunAsync("a").GetAwaiter().GetResult());
                Assert.IsTrue(group.IsFailed);
                Assert.AreEqual("initializer failed", group.Error.Message);
                Assert.AreEqual(1, failures);
            }
            finally { LogAssert.ignoreFailingMessages = false; }
        }

        [Test]
        public void FailedObserverCannotRestartUntilNotificationFinishes()
        {
            var group = _bootstrap.SetModules("a", new IGameModule[] { new Broken() });
            var rejected = false;
            group.Failed += (_, error) =>
            {
                try { _bootstrap.RunAsync("a").GetAwaiter().GetResult(); }
                catch (InvalidOperationException) { rejected = true; }
            };
            LogAssert.ignoreFailingMessages = true;
            try
            {
                _bootstrap.RunAsync("a").GetAwaiter().GetResult();
                Assert.IsTrue(rejected);
                Assert.IsTrue(group.IsFailed);
            }
            finally { LogAssert.ignoreFailingMessages = false; }
        }

        [Test]
        public void ShutdownCallbackCanDestroyHostWithoutDoubleShutdown()
        {
            var module = new Provider { ShutdownAction = () => UnityEngine.Object.DestroyImmediate(_host) };
            _bootstrap.SetModules("a", new[] { module });
            _bootstrap.RunAsync("a").GetAwaiter().GetResult();
            _bootstrap.ResetGroup("a");
            Assert.AreEqual(1, module.ShutdownCount);
        }

        [Test]
        public void AlreadyCancelledEmptyGroupDoesNotBecomeReady()
        {
            var group = _bootstrap.SetModules("a", Array.Empty<IGameModule>());
            _bootstrap.RunAsync("a", new CancellationToken(true)).GetAwaiter().GetResult();
            Assert.IsTrue(group.IsFailed);
            Assert.IsInstanceOf<OperationCanceledException>(group.Error);
        }

        [Test]
        public void RegistrationRejectsSharedInstancesAndPreservesOldConfiguration()
        {
            var shared = new Provider();
            var original = new Consumer();
            _bootstrap.SetModules("a", new[] { shared });
            _bootstrap.SetModules("b", new IGameModule[] { original });
            Assert.Throws<InvalidOperationException>(() => _bootstrap.SetModules("b", new[] { shared }));
            Assert.Throws<ArgumentException>(() => _bootstrap.SetModules("a", new IGameModule[] { null }));
            _bootstrap.RunAsync("a").GetAwaiter().GetResult();
            _bootstrap.RunAsync("b").GetAwaiter().GetResult();
            Assert.AreEqual(1, shared.InitializeCount);
            Assert.AreEqual(1, original.InitializeCount);
        }

        [Test]
        public void ProviderCannotResetUntilDependentIsReset()
        {
            _bootstrap.SetModules("provider", new[] { new Provider() });
            _bootstrap.SetModules("consumer", new[] { new Consumer() });
            _bootstrap.RunAsync("provider").GetAwaiter().GetResult();
            _bootstrap.RunAsync("consumer").GetAwaiter().GetResult();
            Assert.Throws<InvalidOperationException>(() => _bootstrap.ResetGroup("provider"));
            _bootstrap.ResetGroup("consumer");
            Assert.DoesNotThrow(() => _bootstrap.ResetGroup("provider"));
        }

        [Test]
        public void DestroyShutsDownDependentsBeforeProviders()
        {
            var order = new List<string>();
            // Registration order is deliberately different from startup order.
            _bootstrap.SetModules("consumer", new[] { new Consumer { ShutdownAction = () => order.Add("consumer") } });
            _bootstrap.SetModules("provider", new[] { new Provider { ShutdownAction = () => order.Add("provider") } });
            _bootstrap.RunAsync("provider").GetAwaiter().GetResult();
            _bootstrap.RunAsync("consumer").GetAwaiter().GetResult();
            UnityEngine.Object.DestroyImmediate(_host);
            CollectionAssert.AreEqual(new[] { "consumer", "provider" }, order);
        }

        [UnityTest]
        public IEnumerator ConcurrentFailureWaitsForUncooperativeSiblingBeforeRetry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var pending = new Pending();
                var group = _bootstrap.SetModules("a", new IGameModule[] { pending, new Broken() });
                LogAssert.ignoreFailingMessages = true;
                try
                {
                    var run = _bootstrap.RunAsync("a");
                    Assert.IsTrue(group.IsRunning);
                    Assert.Throws<InvalidOperationException>(() => _bootstrap.ResetGroup("a"));
                    pending.Completion.TrySetResult();
                    await run;
                    Assert.IsTrue(group.IsFailed);
                    Assert.AreEqual(1, pending.ShutdownCount);
                }
                finally { LogAssert.ignoreFailingMessages = false; }
            });
        }

        class Provider : IGameModule
        {
            public int InitializeCount;
            public int ShutdownCount;
            public Action ShutdownAction;
            public string Name => GetType().Name;
            public ModulePhase Phase => ModulePhase.Infrastructure;
            public virtual IReadOnlyList<Type> Dependencies => Array.Empty<Type>();
            public void Initialize() { InitializeCount++; }
            public void Shutdown() { ShutdownCount++; ShutdownAction?.Invoke(); }
        }

        sealed class Consumer : Provider
        {
            public override IReadOnlyList<Type> Dependencies => new[] { typeof(Provider) };
        }

        sealed class Pending : Provider, IGameModule
        {
            public readonly UniTaskCompletionSource Completion = new UniTaskCompletionSource();
            public ModuleInitMode InitMode => ModuleInitMode.Asynchronous;
            public bool AllowConcurrentInitialization => true;
            public UniTask InitializeAsync(CancellationToken token) => Completion.Task;
        }

        sealed class Broken : Provider, IGameModule
        {
            public new ModulePhase Phase => ModulePhase.Data;
            public ModuleInitMode InitMode => ModuleInitMode.Asynchronous;
            public bool AllowConcurrentInitialization => true;
            public UniTask InitializeAsync(CancellationToken token) => UniTask.FromException(new InvalidOperationException("initializer failed"));
        }
    }
}
