using System.Collections;
using Framework.Coroutine;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Framework.Tests
{
    /// <summary>Real frame and lifecycle coverage, without bundles or the Launch scene.</summary>
    public sealed class CoroutinePlayModeTests
    {
        GameObject _host;
        GameObject _owner;
        CoroutineManager _manager;
        Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("playmode-coroutine");
            _manager = _host.AddComponent<CoroutineManager>();
            _owner = new GameObject("playmode-coroutine-owner");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_manager != null) _manager.Shutdown();
            if (_owner != null) Object.Destroy(_owner);
            if (_host != null) Object.Destroy(_host);
            if (_scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GlobalCoroutineAdvancesAcrossFrames()
        {
            var step = 0;
            var handle = _manager.StartGlobal(Advance());
            Assert.AreEqual(1, step);
            Assert.IsTrue(handle.IsRunning);
            // Do not depend on the order of two coroutines resuming in one frame.
            for (var i = 0; i < 5 && handle.IsRunning; i++) yield return null;
            Assert.AreEqual(2, step);
            Assert.IsFalse(handle.IsRunning);

            IEnumerator Advance()
            {
                step = 1;
                yield return null;
                step = 2;
            }
        }

        [UnityTest]
        public IEnumerator StopIsIdempotentAndDoesNotStopOtherCoroutines()
        {
            var stoppedTicks = 0;
            var liveTicks = 0;
            var stopped = _manager.StartGlobal(Repeat(() => stoppedTicks++));
            var live = _manager.StartGlobal(Repeat(() => liveTicks++));
            stopped.Stop();
            stopped.Stop();
            stopped.Dispose();
            var before = stoppedTicks;
            var liveBefore = liveTicks;
            yield return null;
            yield return null;
            Assert.IsFalse(stopped.IsRunning);
            Assert.AreEqual(before, stoppedTicks);
            Assert.IsTrue(live.IsRunning);
            Assert.Greater(liveTicks, liveBefore);
        }

        [UnityTest]
        public IEnumerator DestroyingOwnerCompletesHandleAndStopsWork()
        {
            var ticks = 0;
            var handle = _manager.StartOnGameObject(_owner, Repeat(() => ticks++));
            Assert.IsTrue(handle.IsRunning);
            Object.Destroy(_owner);
            yield return null;
            var stoppedAt = ticks;
            yield return null;
            Assert.IsFalse(handle.IsRunning);
            Assert.AreEqual(stoppedAt, ticks);
            handle.Stop();
        }

        [UnityTest]
        public IEnumerator UnloadingSceneStopsSceneHandleButKeepsGlobalHandle()
        {
            var previous = SceneManager.GetActiveScene();
            _scene = SceneManager.CreateScene("CoroutineLifetimeTest");
            ICoroutineHandle sceneHandle;
            try
            {
                Assert.IsTrue(SceneManager.SetActiveScene(_scene));
                sceneHandle = _manager.StartScene(Repeat(() => { }));
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
            }
            var global = _manager.StartGlobal(Repeat(() => { }));
            yield return SceneManager.UnloadSceneAsync(_scene);
            Assert.IsFalse(sceneHandle.IsRunning);
            Assert.IsTrue(global.IsRunning);
        }

        [UnityTest]
        public IEnumerator ShutdownStopsManagedWorkAndAllowsFreshStart()
        {
            var old = _manager.StartGlobal(Repeat(() => { }));
            _manager.Shutdown();
            _manager.Shutdown();
            Assert.IsFalse(old.IsRunning);
            var ticks = 0;
            var fresh = _manager.StartGlobal(Repeat(() => ticks++));
            yield return null;
            yield return null;
            Assert.IsTrue(fresh.IsRunning);
            Assert.Greater(ticks, 1);
        }

        static IEnumerator Repeat(System.Action step)
        {
            while (true)
            {
                step();
                yield return null;
            }
        }
    }
}
