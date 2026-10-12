using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Bootstrap;
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
            var second = new ServiceModule("second");
            var ambiguous = new PlainModule("ambiguous", typeof(IReadyService));
            Assert.Throws<InvalidOperationException>(() =>
                ModuleSorter.SortIntoWaves(new IGameModule[] { ambiguous, first, second }));
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

            public IEnumerator InitializeAsync()
            {
                yield break;
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

            public IEnumerator InitializeAsync()
            {
                yield break;
            }

            public void Shutdown()
            {
            }
        }
    }
}
