using System;
using System.Linq;
using Framework.UI;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    [TestFixture]
    public sealed class WindowRegistryTests
    {
        sealed class First : Window { }
        sealed class Second : Window { }
        sealed class Third : Window { }
        class Window
        {
            internal UILayer Layer = UILayer.UI;
            internal bool FullScreen;
        }

        WindowRegistry<Window> _registry;

        [SetUp]
        public void SetUp() => _registry = new WindowRegistry<Window>(w => w.Layer, w => w.FullScreen);

        [Test]
        public void BringToTopOnlyReordersWithinItsLayer()
        {
            var first = new First();
            var second = new Second();
            var top = new Third { Layer = UILayer.Top };
            _registry.Register(top);
            _registry.Register(first);
            _registry.Register(second);
            _registry.BringToTop(first);
            Assert.That(_registry.Stack, Is.EqualTo(new Window[] { second, first, top }));
            Assert.That(_registry.SnapshotLayout().Select(x => x.SortingOrder), Is.EqualTo(new[] { 5000, 5010, 10000 }));
        }

        [Test]
        public void HideOnlyKeepsIdentityButRemovesVisibilityAndOcclusion()
        {
            var bottom = new First();
            var fullScreen = new Second { FullScreen = true };
            _registry.Register(bottom);
            _registry.Register(fullScreen);
            _registry.Hide(fullScreen);
            Assert.That(_registry.TryGetOpened(typeof(Second), out var retained), Is.True);
            Assert.That(retained, Is.SameAs(fullScreen));
            Assert.That(_registry.IsVisible(fullScreen), Is.False);
            Assert.That(_registry.SnapshotLayout().Single().Occluded, Is.False);
            _registry.BringToTop(fullScreen);
            Assert.That(_registry.Stack.Count, Is.EqualTo(2));
        }

        [Test]
        public void CacheAndReviveTransferIdentityWithoutDuplicates()
        {
            var window = new First();
            _registry.Register(window);
            _registry.Cache(window);
            Assert.That(_registry.TryGetOpened(typeof(First), out _), Is.False);
            Assert.That(_registry.TryGetCached(typeof(First), out var cached), Is.True);
            Assert.That(cached, Is.SameAs(window));
            Assert.That(_registry.Stack, Is.Empty);
            _registry.Register(cached);
            _registry.Register(cached);
            Assert.That(_registry.TryGetCached(typeof(First), out _), Is.False);
            Assert.That(_registry.Stack.Count, Is.EqualTo(1));
        }

        [Test]
        public void FullScreenOccludesOnlyWindowsBelowIt()
        {
            var bottom = new First { Layer = UILayer.Bottom };
            var fullScreen = new Second { FullScreen = true };
            var tip = new Third { Layer = UILayer.Tips };
            _registry.Register(bottom);
            _registry.Register(fullScreen);
            _registry.Register(tip);
            Assert.That(_registry.SnapshotLayout().Select(x => x.Occluded), Is.EqualTo(new[] { true, false, false }));
        }

        [Test]
        public void LayoutSnapshotSurvivesLaterRemovals()
        {
            var window = new First();
            _registry.Register(window);
            var snapshot = _registry.SnapshotLayout();
            _registry.Remove(window);
            Assert.That(snapshot.Single().Window, Is.SameAs(window));
            Assert.That(_registry.Stack, Is.Empty);
        }

        [Test]
        public void FullLayerRejectsNewWindowAtomicallyButAllowsReordering()
        {
            var first = new Window { Layer = UILayer.System };
            _registry.BringToTop(first);
            for (var i = 1; i < WindowRegistry<Window>.LayerCapacity; i++)
                _registry.BringToTop(new Window { Layer = UILayer.System });
            var before = _registry.Stack.ToArray();
            Assert.Throws<InvalidOperationException>(() => _registry.Register(new First { Layer = UILayer.System }));
            Assert.That(_registry.Stack, Is.EqualTo(before));
            Assert.That(_registry.TryGetOpened(typeof(First), out _), Is.False);
            _registry.BringToTop(first);
            Assert.That(_registry.Stack.Last(), Is.SameAs(first));
            Assert.That(_registry.SnapshotLayout().Last().SortingOrder, Is.EqualTo(24990));
        }

        [Test]
        public void FailedReviveLeavesCachedIdentityIntact()
        {
            var cached = new First();
            _registry.Register(cached);
            _registry.Cache(cached);
            for (var i = 0; i < WindowRegistry<Window>.LayerCapacity; i++) _registry.BringToTop(new Window());
            Assert.Throws<InvalidOperationException>(() => _registry.Register(cached));
            Assert.That(_registry.TryGetCached(typeof(First), out var retained), Is.True);
            Assert.That(retained, Is.SameAs(cached));
        }

        [Test]
        public void UndefinedLayerIsRejectedBeforeMutation()
        {
            Assert.Throws<InvalidOperationException>(() => _registry.Register(new First { Layer = (UILayer)1 }));
            Assert.That(_registry.Stack, Is.Empty);
        }

        [Test]
        public void RemovingStaleInstanceDoesNotUnregisterReplacement()
        {
            var old = new First();
            var replacement = new First();
            _registry.Register(old);
            _registry.Remove(old);
            _registry.Register(replacement);
            _registry.Remove(old);
            Assert.That(_registry.TryGetOpened(typeof(First), out var current), Is.True);
            Assert.That(current, Is.SameAs(replacement));
        }

        [Test]
        public void DrainIncludesHiddenAndCachedWindowsAndIsIdempotent()
        {
            var hidden = new First();
            var cached = new Second();
            var visible = new Third();
            _registry.Register(hidden);
            _registry.Hide(hidden);
            _registry.Register(cached);
            _registry.Cache(cached);
            _registry.Register(visible);
            Assert.That(_registry.Drain(), Is.EquivalentTo(new Window[] { hidden, cached, visible }));
            Assert.That(_registry.Stack, Is.Empty);
            Assert.That(_registry.TryGetOpened(typeof(First), out _), Is.False);
            Assert.That(_registry.TryGetCached(typeof(Second), out _), Is.False);
            Assert.That(_registry.Drain(), Is.Empty);
        }
    }
}
