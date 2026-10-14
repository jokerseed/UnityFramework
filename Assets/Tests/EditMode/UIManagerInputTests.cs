using Framework.UI;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    /// <summary>UIManager 输入屏蔽计数与返回键分发的边界。</summary>
    public sealed class UIManagerInputTests
    {
        GameObject _host;
        UIManager _ui;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("UIManagerInputTests");
            _ui = _host.AddComponent<UIManager>();
            _ui.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void NestedBlocksReleaseOnlyAfterLastDispose()
        {
            var first = _ui.BlockInput();
            var second = _ui.BlockInput();
            Assert.IsTrue(_ui.IsInputBlocked);

            first.Dispose();
            first.Dispose();
            Assert.IsTrue(_ui.IsInputBlocked, "double dispose must not release the second block");

            second.Dispose();
            Assert.IsFalse(_ui.IsInputBlocked);
        }

        [Test]
        public void BackIsNotHandledWhileBlockedOrEmpty()
        {
            Assert.IsFalse(_ui.HandleBack());
            using (_ui.BlockInput())
            {
                Assert.IsFalse(_ui.HandleBack());
            }
        }
    }
}
