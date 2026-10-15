using System;
using Framework.Audio;
using Framework.Config;
using Framework.Core;
using Framework.ObjectPool;
using Framework.Res;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    /// <summary>资源寻址、配置注册、音量和对象池。不启动完整的 YooAsset 下载。</summary>
    public sealed class ModuleCoverageTests
    {
        [Test]
        public void ResourceAddressesLowercaseBundlePaths()
        {
            Assert.AreEqual("bundles/configs/tbability.unity3d", ResourceAddresses.ConfigTable("tbability"));
            Assert.AreEqual("bundles/scenes/launch.unity3d", ResourceAddresses.Scene("Launch"));
            Assert.AreEqual("bundles/behaviourtrees/monstercommon.unity3d", ResourceAddresses.BehaviourTree("MonsterCommon"));
        }

        [Test]
        public void ConfigManagerRejectsLoadBeforeRegister()
        {
            var manager = ConfigManager.Instance;
            try
            {
                Assert.IsFalse(manager.IsRegistered);
                var ex = Assert.Throws<InvalidOperationException>(() => manager.LoadTables<object>());
                StringAssert.Contains("No tables registered", ex.Message);
            }
            finally
            {
                DestroySingleton(manager);
            }
        }

        [Test]
        public void AudioVolumeMutesAndScales()
        {
            var settings = new AudioVolumeSettings
            {
                MasterVolume = 0.5f,
                BgmVolume = 0.5f,
                SfxVolume = 0.25f,
            };

            Assert.AreEqual(0.25f, settings.GetBgmVolume(), 0.0001f);
            Assert.AreEqual(0.125f, settings.GetSfxVolume(), 0.0001f);
            settings.Mute = true;
            Assert.AreEqual(0f, settings.GetBgmVolume());
            Assert.AreEqual(0f, settings.GetSfxVolume(4f));
        }

        [Test]
        public void ObjectPoolReusesUnspawnedInstance()
        {
            var manager = ObjectPoolManager.Instance;
            try
            {
                var pool = manager.CreateSingleSpawnObjectPool<PoolProbe>("coverage", capacity: 4);
                var target = new object();
                var first = pool.SpawnOrCreate(() => PoolProbe.Create(target));
                Assert.AreEqual(1, pool.Count);
                pool.Unspawn(first);
                Assert.IsTrue(pool.CanSpawn());
                var second = pool.Spawn();
                Assert.AreSame(first, second);
                Assert.Throws<ArgumentNullException>(() => pool.SpawnOrCreate(null));
            }
            finally
            {
                DestroySingleton(manager);
            }
        }

        static void DestroySingleton(MonoBehaviour behaviour)
        {
            if (behaviour == null)
            {
                return;
            }

            var host = behaviour.gameObject;
            if (behaviour is ConfigManager)
            {
                ConfigManager.ResetStaticState();
            }
            else if (behaviour is ObjectPoolManager)
            {
                ObjectPoolManager.ResetStaticState();
            }

            UnityEngine.Object.DestroyImmediate(host);
        }

        public sealed class PoolProbe : ObjectBase
        {
            public static PoolProbe Create(object target)
            {
                return CreateInstance<PoolProbe>("probe", target);
            }

            protected override void Release(bool isShutdown)
            {
            }
        }
    }
}
