using System.Collections;
using Framework.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Framework.Tests
{
    public sealed class SingletonLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator DeferredOldObjectDestructionDoesNotCleanUpReplacement()
        {
            DeferredCleanupProbe.ResetStaticState();
            DeferredCleanupProbe.CleanupCount = 0;
            var original = DeferredCleanupProbe.Instance;
            DeferredCleanupProbe.DestroyInstance();
            Assert.AreEqual(1, DeferredCleanupProbe.CleanupCount);
            var replacement = DeferredCleanupProbe.Instance;
            Assert.AreNotSame(original, replacement);
            yield return null;
            Assert.AreEqual(1, DeferredCleanupProbe.CleanupCount);
            Assert.AreSame(replacement, DeferredCleanupProbe.Instance);
            DeferredCleanupProbe.DestroyInstance();
            yield return null;
            Assert.AreEqual(2, DeferredCleanupProbe.CleanupCount);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DeferredCleanupProbe.DestroyInstance();
            yield return null;
        }
    }

    public sealed class DeferredCleanupProbe : PersistentSingleton<DeferredCleanupProbe>
    {
        public static int CleanupCount;
        protected override void OnSingletonDestroy() { CleanupCount++; }
    }
}
