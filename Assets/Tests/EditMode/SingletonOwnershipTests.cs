using System;
using System.Collections.Generic;
using Framework.Bootstrap;
using Framework.Core;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    public sealed class SingletonOwnershipTests
    {
        [SetUp]
        public void SetUp()
        {
            OwnershipProbeSingleton.ResetStaticState();
            OwnershipProbeSingleton.CleanupCount = 0;
            OwnershipProbeSingleton.Cleanup = null;
        }

        [TearDown]
        public void TearDown()
        {
            OwnershipProbeSingleton.Cleanup = null;
            OwnershipProbeSingleton.DestroyInstance();
        }

        [Test]
        public void DuplicateDestructionDoesNotCleanUpTheOwner()
        {
            var owner = OwnershipProbeSingleton.Instance;
            var duplicate = new GameObject("duplicate singleton");
            duplicate.AddComponent<OwnershipProbeSingleton>();
            if (duplicate != null) UnityEngine.Object.DestroyImmediate(duplicate);
            Assert.AreSame(owner, OwnershipProbeSingleton.Instance);
            Assert.AreEqual(0, OwnershipProbeSingleton.CleanupCount);
        }

        [Test]
        public void DestroyInstanceCleansOnceBeforeReplacementCanBeCreated()
        {
            var original = OwnershipProbeSingleton.Instance;
            OwnershipProbeSingleton.DestroyInstance();
            Assert.AreEqual(1, OwnershipProbeSingleton.CleanupCount);
            Assert.IsFalse(OwnershipProbeSingleton.HasInstance);
            var replacement = OwnershipProbeSingleton.Instance;
            Assert.AreNotSame(original, replacement);
            Assert.AreEqual(1, OwnershipProbeSingleton.CleanupCount);
        }

        [Test]
        public void CleanupCannotRecreateItselfAndRepeatedDestroyIsHarmless()
        {
            _ = OwnershipProbeSingleton.Instance;
            OwnershipProbeSingleton.Cleanup = () =>
            {
                Assert.Throws<InvalidOperationException>(() => { _ = OwnershipProbeSingleton.Instance; });
                OwnershipProbeSingleton.DestroyInstance();
            };
            OwnershipProbeSingleton.DestroyInstance();
            OwnershipProbeSingleton.DestroyInstance();
            Assert.AreEqual(1, OwnershipProbeSingleton.CleanupCount);
            Assert.IsFalse(OwnershipProbeSingleton.HasInstance);
        }

        [Test]
        public void CleanupFailureStillReleasesOwnership()
        {
            _ = OwnershipProbeSingleton.Instance;
            OwnershipProbeSingleton.Cleanup = () => throw new InvalidOperationException("cleanup failed");
            Assert.Throws<InvalidOperationException>(() => OwnershipProbeSingleton.DestroyInstance());
            Assert.IsFalse(OwnershipProbeSingleton.HasInstance);
            OwnershipProbeSingleton.Cleanup = null;
            Assert.IsNotNull(OwnershipProbeSingleton.Instance);
        }

        [Test]
        public void ExclusiveModuleTypeCannotBeOwnedByTwoGroupsUntilReset()
        {
            var host = new GameObject("exclusive modules");
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.SetModules("first", new IGameModule[] { new ExclusiveProbeModule() });
                Assert.Throws<InvalidOperationException>(() => bootstrap.SetModules("second", new IGameModule[] { new ExclusiveProbeModule() }));
                bootstrap.ResetGroup("first");
                Assert.DoesNotThrow(() => bootstrap.SetModules("second", new IGameModule[] { new ExclusiveProbeModule() }));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        sealed class ExclusiveProbeModule : IExclusiveGameModule
        {
            public string Name => "exclusive";
            public ModulePhase Phase => ModulePhase.Infrastructure;
            public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();
            public void Initialize() { }
            public void Shutdown() { }
        }
    }

    public sealed class OwnershipProbeSingleton : PersistentSingleton<OwnershipProbeSingleton>
    {
        public static int CleanupCount;
        public static Action Cleanup;
        protected override void OnSingletonDestroy()
        {
            CleanupCount++;
            Cleanup?.Invoke();
        }
    }
}
