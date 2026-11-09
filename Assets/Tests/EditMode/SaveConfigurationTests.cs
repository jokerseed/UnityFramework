using System;
using System.Collections.Generic;
using System.Text;
using Framework.Save;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    public sealed class SaveConfigurationTests
    {
        GameObject _host;
        SaveManager _saves;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("save-configuration-test");
            _saves = _host.AddComponent<SaveManager>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void InvalidReconfiguration_PreservesBothDependenciesAndSaveDestination(bool nullStorage, bool nullSerializer)
        {
            var originalStorage = new RecordingStorage();
            var originalSerializer = new RecordingSerializer("original");
            var replacementStorage = new RecordingStorage();
            var replacementSerializer = new RecordingSerializer("replacement");
            _saves.Configure(originalStorage, originalSerializer);

            var error = Assert.Throws<ArgumentNullException>(() => _saves.Configure(
                nullStorage ? null : replacementStorage, nullSerializer ? null : replacementSerializer));
            // 先执行保存，再检查两个依赖与实际目标，避免只验证异常而漏掉部分赋值。
            _saves.Save("profile", 42);

            Assert.AreEqual(nullStorage ? "storage" : "serializer", error.ParamName);
            Assert.IsTrue(_saves.IsConfigured);
            Assert.AreSame(originalStorage, _saves.Storage);
            CollectionAssert.AreEqual(new[] { "profile:original" }, originalStorage.Writes);
            Assert.IsEmpty(replacementStorage.Writes);
            Assert.AreEqual(1, originalSerializer.SerializeCalls);
            Assert.AreEqual(0, replacementSerializer.SerializeCalls);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void InvalidInitialConfiguration_RemainsUnconfigured(bool nullStorage, bool nullSerializer)
        {
            var storage = new RecordingStorage();
            var serializer = new RecordingSerializer("initial");

            var error = Assert.Throws<ArgumentNullException>(() => _saves.Configure(
                nullStorage ? null : storage, nullSerializer ? null : serializer));

            Assert.AreEqual(nullStorage ? "storage" : "serializer", error.ParamName);
            Assert.IsFalse(_saves.IsConfigured);
            Assert.IsNull(_saves.Storage);
            Assert.Throws<InvalidOperationException>(() => _saves.Save("profile", 42));
            Assert.IsEmpty(storage.Writes);
            Assert.AreEqual(0, serializer.SerializeCalls);
        }

        [Test]
        public void ValidReconfiguration_UsesBothNewDependencies()
        {
            var originalStorage = new RecordingStorage();
            var originalSerializer = new RecordingSerializer("original");
            var replacementStorage = new RecordingStorage();
            var replacementSerializer = new RecordingSerializer("replacement");
            _saves.Configure(originalStorage, originalSerializer);

            _saves.Configure(replacementStorage, replacementSerializer);
            _saves.Save("profile", 42);

            Assert.IsTrue(_saves.IsConfigured);
            Assert.AreSame(replacementStorage, _saves.Storage);
            Assert.IsEmpty(originalStorage.Writes);
            CollectionAssert.AreEqual(new[] { "profile:replacement" }, replacementStorage.Writes);
            Assert.AreEqual(0, originalSerializer.SerializeCalls);
            Assert.AreEqual(1, replacementSerializer.SerializeCalls);
        }

        sealed class RecordingStorage : ISaveStorage
        {
            public readonly List<string> Writes = new List<string>();
            public bool Exists(string key) => throw new NotSupportedException();
            public bool TryRead(string key, out byte[] data) => throw new NotSupportedException();
            public void Write(string key, byte[] data) => Writes.Add(key + ":" + Encoding.UTF8.GetString(data));
            public bool Delete(string key) => throw new NotSupportedException();
            public IReadOnlyList<string> ListKeys() => throw new NotSupportedException();
        }

        sealed class RecordingSerializer : ISaveSerializer
        {
            readonly string _marker;
            public int SerializeCalls { get; private set; }
            public RecordingSerializer(string marker) => _marker = marker;
            public byte[] Serialize<T>(T value)
            {
                SerializeCalls++;
                return Encoding.UTF8.GetBytes(_marker);
            }
            public T Deserialize<T>(byte[] data) => throw new NotSupportedException();
        }
    }
}
