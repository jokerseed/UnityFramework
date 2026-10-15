using System.Collections;
using Framework.Coroutine;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Framework.Tests
{
    /// <summary>PlayMode 冒烟：协程能跨帧跑完。不加载 Launch 场景，避免依赖资源包初始化。</summary>
    public sealed class CoroutinePlayModeTests
    {
        [UnityTest]
        public IEnumerator GlobalCoroutineAdvancesAcrossFrames()
        {
            var host = new GameObject("playmode-coroutine");
            var manager = host.AddComponent<CoroutineManager>();
            var step = 0;
            var handle = manager.StartGlobal(Advance());
            yield return null;
            Assert.AreEqual(1, step);
            Assert.IsTrue(handle.IsRunning);
            yield return null;
            Assert.AreEqual(2, step);
            Assert.IsFalse(handle.IsRunning);
            Object.Destroy(host);

            IEnumerator Advance()
            {
                step = 1;
                yield return null;
                step = 2;
            }
        }
    }
}
