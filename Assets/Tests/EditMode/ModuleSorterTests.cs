using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Bootstrap;
using Framework.Battle;
using Framework.Core;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>模块依赖可以写接口，多个实现时必须失败。</summary>
    public sealed class ModuleSorterTests
    {
        interface IReadyService
        {
        }

        [Test]
        public void InterfaceDependencySortsAfterTheSingleImplementation()
        {
            var service = new ServiceModule("service");
            var consumer = new PlainModule("consumer", typeof(IReadyService));
            var waves = ModuleSorter.SortIntoWaves(new IGameModule[] { consumer, service });
            Assert.AreEqual(2, waves.Count);
            Assert.AreSame(service, waves[0][0]);
            Assert.AreSame(consumer, waves[1][0]);
        }

        [Test]
        public void ReadyGroupSatisfiesDependencyAndTwoMatchesFail()
        {
            var outside = new ServiceModule("outside");
            var consumer = new PlainModule("consumer", typeof(ServiceModule));
            var waves = ModuleSorter.SortIntoWaves(
                new IGameModule[] { consumer },
                new IGameModule[] { outside });
            Assert.AreEqual(1, waves.Count);
            Assert.AreSame(consumer, waves[0][0]);

            var first = new ServiceModule("first");
            var second = new OtherServiceModule("second");
            var ambiguous = new PlainModule("ambiguous", typeof(IReadyService));
            var error = Assert.Throws<InvalidOperationException>(() =>
                ModuleSorter.SortIntoWaves(new IGameModule[] { ambiguous, first, second }));
            StringAssert.Contains("matches", error.Message);
        }

        [Test]
        public void DerivedCompletionDoesNotReleaseBaseDependents()
        {
            var gate = new ServiceModule("gate");
            var derived = new DerivedModule("derived");
            var baseModule = new BaseModule("base", typeof(ServiceModule));
            var consumer = new PlainModule("consumer", typeof(BaseModule));
            var waves = ModuleSorter.SortIntoWaves(new IGameModule[] { consumer, derived, baseModule, gate });
            Assert.Greater(WaveOf(waves, consumer), WaveOf(waves, baseModule));
            Assert.Greater(WaveOf(waves, baseModule), WaveOf(waves, derived));
        }

        [Test]
        public void UnrelatedReadyGroupDuplicatesAreIgnored()
        {
            var consumer = new ServiceModule("consumer");
            var waves = ModuleSorter.SortIntoWaves(
                new IGameModule[] { consumer },
                new IGameModule[] { new PlainModule("outside-a"), new PlainModule("outside-b") });
            Assert.AreEqual(1, waves.Count);
            Assert.AreSame(consumer, waves[0][0]);

            var dependent = new BaseModule("needs-plain", typeof(PlainModule));
            var duplicateError = Assert.Throws<InvalidOperationException>(() =>
                ModuleSorter.SortIntoWaves(
                    new IGameModule[] { dependent },
                    new IGameModule[] { new PlainModule("outside-a"), new PlainModule("outside-b") }));
            StringAssert.Contains("matches", duplicateError.Message);
        }

        static int WaveOf(IReadOnlyList<IReadOnlyList<IGameModule>> waves, IGameModule module)
        {
            for (var i = 0; i < waves.Count; i++)
            {
                for (var j = 0; j < waves[i].Count; j++)
                {
                    if (ReferenceEquals(waves[i][j], module))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        sealed class OtherServiceModule : IGameModule, IReadyService
        {
            public OtherServiceModule(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public ModulePhase Phase => ModulePhase.Infrastructure;

            public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

            public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

            public void Initialize()
            {
            }

            public void Shutdown()
            {
            }
        }

        class BaseModule : IGameModule
        {
            readonly Type[] _dependencies;

            public BaseModule(string name, params Type[] dependencies)
            {
                Name = name;
                _dependencies = dependencies ?? Array.Empty<Type>();
            }

            public string Name { get; }

            public ModulePhase Phase => ModulePhase.Infrastructure;

            public virtual IReadOnlyList<Type> Dependencies => _dependencies;

            public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

            public void Initialize()
            {
            }

            public void Shutdown()
            {
            }
        }

        sealed class DerivedModule : BaseModule
        {
            public DerivedModule(string name)
                : base(name)
            {
            }

            public override IReadOnlyList<Type> Dependencies => Array.Empty<Type>();
        }

        sealed class ServiceModule : IGameModule, IReadyService
        {
            public ServiceModule(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public ModulePhase Phase => ModulePhase.Infrastructure;

            public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

            public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

            public void Initialize()
            {
            }

            public void Shutdown()
            {
            }
        }

        sealed class PlainModule : IGameModule
        {
            readonly Type[] _dependencies;

            public PlainModule(string name, params Type[] dependencies)
            {
                Name = name;
                _dependencies = dependencies ?? Array.Empty<Type>();
            }

            public string Name { get; }

            public ModulePhase Phase => ModulePhase.Infrastructure;

            public IReadOnlyList<Type> Dependencies => _dependencies;

            public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

            public void Initialize()
            {
            }

            public void Shutdown()
            {
            }
        }
    }
}
