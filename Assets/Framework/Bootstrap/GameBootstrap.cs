using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Core;
using UnityEngine;

namespace Framework.Bootstrap
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : PersistentSingleton<GameBootstrap>
    {
        sealed class InitProgress
        {
            public int Value;
        }

        readonly Dictionary<string, ModuleGroup> _groups = new Dictionary<string, ModuleGroup>();

        public ModuleGroup SetModules(string groupName, IEnumerable<IGameModule> modules)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                throw new ArgumentException("Group name is required.", nameof(groupName));
            }

            if (!_groups.TryGetValue(groupName, out var group))
            {
                group = new ModuleGroup(groupName);
                _groups[groupName] = group;
            }

            if (group.State == ModuleGroupState.Ready)
            {
                throw new InvalidOperationException(
                    $"Group '{groupName}' is already ready. Call ResetGroup(\"{groupName}\") before reconfiguring.");
            }

            group.Configure(modules);
            return group;
        }

        public ModuleGroup GetGroup(string groupName)
        {
            if (!_groups.TryGetValue(groupName, out var group))
            {
                throw new KeyNotFoundException($"Module group not found: {groupName}");
            }

            return group;
        }

        public bool TryGetGroup(string groupName, out ModuleGroup group)
        {
            return _groups.TryGetValue(groupName, out group);
        }

        public void Run(string groupName)
        {
            StartCoroutine(RunAsync(groupName));
        }

        public IEnumerator RunAsync(string groupName)
        {
            var group = GetGroup(groupName);
            if (group.State == ModuleGroupState.Running)
            {
                while (group.State == ModuleGroupState.Running)
                {
                    yield return null;
                }

                yield break;
            }

            if (group.State == ModuleGroupState.Ready)
            {
                yield break;
            }

            yield return RunGroup(group);
        }

        public void ResetGroup(string groupName)
        {
            var group = GetGroup(groupName);
            ShutdownGroup(group);
            group.Modules.Clear();
            group.Initialized.Clear();
            group.SetState(ModuleGroupState.Idle);
        }

        IEnumerator RunGroup(ModuleGroup group)
        {
            group.SetState(ModuleGroupState.Running);

            Exception failure = null;
            var routine = RunGroupInternal(group);
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext())
                    {
                        break;
                    }

                    current = routine.Current;
                }
                catch (Exception ex)
                {
                    failure = ex;
                    break;
                }

                yield return current;
            }

            if (failure != null)
            {
                Debug.LogException(failure);
                group.SetState(ModuleGroupState.Failed, failure);
                yield break;
            }
        }

        IEnumerator RunGroupInternal(ModuleGroup group)
        {
            if (group.Modules.Count == 0)
            {
                Debug.LogWarning($"[Bootstrap] Group '{group.Name}' has no modules.");
                group.SetState(ModuleGroupState.Ready);
                yield break;
            }

            var waves = ModuleSorter.SortIntoWaves(group.Modules);
            var progress = new InitProgress();
            var totalModules = group.Modules.Count;

            for (var waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                var wave = waves[waveIndex];
                var useConcurrent = CanRunConcurrently(wave);

                if (useConcurrent)
                {
                    Debug.Log(
                        $"[Bootstrap][{group.Name}] Wave {waveIndex + 1}/{waves.Count} concurrent ({wave.Count} modules)...");
                    yield return InitializeWaveConcurrent(group, wave, progress, totalModules);
                }
                else
                {
                    for (var i = 0; i < wave.Count; i++)
                    {
                        yield return InitializeModuleSequential(group, wave[i], progress, totalModules);
                    }
                }
            }

            group.SetState(ModuleGroupState.Ready);
            Debug.Log($"[Bootstrap] Group '{group.Name}' ready.");
        }

        IEnumerator InitializeModuleSequential(
            ModuleGroup group,
            IGameModule module,
            InitProgress progress,
            int totalModules)
        {
            progress.Value++;
            group.ReportProgress(progress.Value, totalModules);
            Debug.Log(
                $"[Bootstrap][{group.Name}] Initializing {module.Name} ({progress.Value}/{totalModules}, {module.InitMode})...");

            if (module.InitMode == ModuleInitMode.Synchronous)
            {
                module.Initialize();
            }
            else
            {
                yield return module.InitializeAsync();
            }

            CompleteModule(group, module);
        }

        IEnumerator InitializeWaveConcurrent(
            ModuleGroup group,
            IReadOnlyList<IGameModule> wave,
            InitProgress progress,
            int totalModules)
        {
            var pendingAsync = 0;
            Exception failure = null;

            for (var i = 0; i < wave.Count; i++)
            {
                var module = wave[i];
                progress.Value++;
                group.ReportProgress(progress.Value, totalModules);
                Debug.Log(
                    $"[Bootstrap][{group.Name}] Initializing {module.Name} ({progress.Value}/{totalModules}, {module.InitMode}, concurrent)...");

                if (module.InitMode == ModuleInitMode.Synchronous)
                {
                    try
                    {
                        module.Initialize();
                        CompleteModule(group, module);
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                        break;
                    }

                    continue;
                }

                pendingAsync++;
                StartCoroutine(RunAsyncModule(group, module, () => pendingAsync--, ex =>
                {
                    failure = ex;
                    pendingAsync--;
                }));
            }

            while (pendingAsync > 0 && failure == null)
            {
                yield return null;
            }

            if (failure != null)
            {
                throw failure;
            }
        }

        IEnumerator RunAsyncModule(
            ModuleGroup group,
            IGameModule module,
            Action onComplete,
            Action<Exception> onError)
        {
            IEnumerator routine;
            try
            {
                routine = module.InitializeAsync();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                yield break;
            }

            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext())
                    {
                        break;
                    }

                    current = routine.Current;
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                    yield break;
                }

                yield return current;
            }

            CompleteModule(group, module);
            onComplete?.Invoke();
        }

        static void CompleteModule(ModuleGroup group, IGameModule module)
        {
            group.Initialized.Add(module);
        }

        static bool CanRunConcurrently(IReadOnlyList<IGameModule> wave)
        {
            if (wave.Count <= 1)
            {
                return false;
            }

            for (var i = 0; i < wave.Count; i++)
            {
                if (!wave[i].AllowConcurrentInitialization)
                {
                    return false;
                }
            }

            return true;
        }

        static void ShutdownGroup(ModuleGroup group)
        {
            for (var i = group.Initialized.Count - 1; i >= 0; i--)
            {
                group.Initialized[i].Shutdown();
            }

            group.Initialized.Clear();
        }

        protected override void OnDestroy()
        {
            foreach (var pair in _groups)
            {
                ShutdownGroup(pair.Value);
            }

            _groups.Clear();
            base.OnDestroy();
        }
    }
}
