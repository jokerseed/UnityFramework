using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;
using UnityEngine;

namespace Framework.Bootstrap
{
    /// <summary>
    /// 游戏启动引导器：管理多组 <see cref="IGameModule"/>，按依赖顺序（拓扑排序波次）驱动初始化与关闭。
    /// 继承 <see cref="PersistentSingleton{T}"/>，跨场景常驻；不实现 <see cref="IGameModule"/>（Host 角色）。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : PersistentSingleton<GameBootstrap>
    {
        sealed class InitProgress
        {
            public int Value;
        }

        readonly Dictionary<string, ModuleGroup> _groups = new Dictionary<string, ModuleGroup>();

        /// <summary>
        /// 注册或替换指定组的模块列表；组不存在时自动创建。
        /// 组处于 <see cref="ModuleGroupState.Ready"/> 时禁止重新配置，需先调用 <see cref="ResetGroup"/>。
        /// </summary>
        /// <param name="groupName">组名，不可为空白字符串。</param>
        /// <param name="modules">要注册的模块集合；顺序无强制要求，由拓扑排序决定初始化顺序。</param>
        /// <returns>对应的 <see cref="ModuleGroup"/> 实例，可用于监听事件。</returns>
        /// <exception cref="ArgumentException"><paramref name="groupName"/> 为空或空白。</exception>
        /// <exception cref="InvalidOperationException">该组已处于 Ready 状态。</exception>
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

        /// <summary>获取已注册的模块组。</summary>
        /// <param name="groupName">组名。</param>
        /// <returns>对应的 <see cref="ModuleGroup"/> 实例。</returns>
        /// <exception cref="KeyNotFoundException">指定名称的模块组不存在。</exception>
        public ModuleGroup GetGroup(string groupName)
        {
            if (!_groups.TryGetValue(groupName, out var group))
            {
                throw new KeyNotFoundException($"Module group not found: {groupName}");
            }

            return group;
        }

        /// <summary>尝试获取已注册的模块组，不存在时不抛异常。</summary>
        /// <param name="groupName">组名。</param>
        /// <param name="group">找到时输出对应的 <see cref="ModuleGroup"/>；否则为 null。</param>
        /// <returns>找到返回 <see langword="true"/>，否则 <see langword="false"/>。</returns>
        public bool TryGetGroup(string groupName, out ModuleGroup group)
        {
            return _groups.TryGetValue(groupName, out group);
        }

        /// <summary>
        /// 启动指定组的异步初始化协程（内部调用 <see cref="RunAsync"/>）。
        /// 若组已在运行则等待其完成；已就绪则立即返回。
        /// </summary>
        /// <param name="groupName">要启动的组名。</param>
        /// <exception cref="KeyNotFoundException">指定名称的模块组不存在。</exception>
        public void Run(string groupName)
        {
            GetGroup(groupName);
            StartCoroutine(RunAsync(groupName));
        }

        /// <summary>
        /// 异步初始化指定模块组的协程。
        /// 若组已在运行则等待其完成；已就绪则立即 yield break。
        /// </summary>
        /// <param name="groupName">要启动的组名。</param>
        /// <returns>可被 StartCoroutine 驱动的协程枚举器。</returns>
        /// <exception cref="KeyNotFoundException">指定名称的模块组不存在。</exception>
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

        /// <summary>
        /// 关闭并重置指定模块组：逆序调用所有已初始化模块的 Shutdown，清空模块列表，并将组状态置回 Idle。
        /// </summary>
        /// <param name="groupName">要重置的组名。</param>
        /// <exception cref="KeyNotFoundException">指定名称的模块组不存在。</exception>
        /// <exception cref="InvalidOperationException">该组仍在 <see cref="ModuleGroupState.Running"/>。</exception>
        public void ResetGroup(string groupName)
        {
            var group = GetGroup(groupName);
            if (group.State == ModuleGroupState.Running)
            {
                throw new InvalidOperationException(
                    $"Group '{groupName}' is running. Wait until it finishes before ResetGroup.");
            }

            ShutdownGroup(group);
            group.Modules.Clear();
            group.Initialized.Clear();
            group.SetState(ModuleGroupState.Idle);
        }

        IEnumerator RunGroup(ModuleGroup group)
        {
            var serial = ++group.RunSerial;
            group.SetState(ModuleGroupState.Running);

            Exception failure = null;
            var driven = NestedCoroutine.Drive(RunGroupInternal(group, serial), ex => failure = ex);
            while (failure == null && driven.MoveNext())
            {
                yield return driven.Current;
            }

            if (failure != null)
            {
                GameLog.Exception(LogCategories.Bootstrap, failure);
                group.RunSerial++;
                try
                {
                    ShutdownGroup(group);
                }
                catch (Exception shutdownError)
                {
                    GameLog.Exception(LogCategories.Bootstrap, shutdownError);
                }
                group.SetState(ModuleGroupState.Failed, failure);
            }
        }

        List<IGameModule> CollectReadyModules(string exceptGroup)
        {
            var ready = new List<IGameModule>();
            foreach (var pair in _groups)
            {
                if (pair.Key == exceptGroup || pair.Value.State != ModuleGroupState.Ready)
                {
                    continue;
                }

                var modules = pair.Value.Modules;
                for (var i = 0; i < modules.Count; i++)
                {
                    ready.Add(modules[i]);
                }
            }

            return ready;
        }

        IEnumerator RunGroupInternal(ModuleGroup group, int serial)
        {
            if (group.Modules.Count == 0)
            {
                GameLog.Warning(LogCategories.Bootstrap, $"Group {LogStyle.Name(group.Name)} has no modules.");
                group.SetState(ModuleGroupState.Ready);
                yield break;
            }

            var waves = ModuleSorter.SortIntoWaves(group.Modules, CollectReadyModules(group.Name));
            var progress = new InitProgress();
            var totalModules = group.Modules.Count;

            for (var waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                var wave = waves[waveIndex];
                var useConcurrent = CanRunConcurrently(wave);

                if (useConcurrent)
                {
                    GameLog.Info(
                        LogCategories.Bootstrap,
                        $"Group {LogStyle.Name(group.Name)} wave {LogStyle.Value($"{waveIndex + 1}/{waves.Count}")} {LogStyle.Muted("concurrent")} ({LogStyle.Value(wave.Count)} modules)");
                    yield return InitializeWaveConcurrent(group, wave, progress, totalModules, serial);
                }
                else
                {
                    for (var i = 0; i < wave.Count; i++)
                    {
                        yield return InitializeModuleSequential(group, wave[i], progress, totalModules, serial);
                    }
                }
            }

            group.SetState(ModuleGroupState.Ready);
            GameLog.Info(LogCategories.Bootstrap, $"Group {LogStyle.Name(group.Name)} {LogStyle.Ok("ready")}");
        }

        IEnumerator InitializeModuleSequential(
            ModuleGroup group,
            IGameModule module,
            InitProgress progress,
            int totalModules,
            int serial)
        {
            progress.Value++;
            group.ReportProgress(progress.Value, totalModules);
            GameLog.Info(
                LogCategories.Bootstrap,
                $"Group {LogStyle.Name(group.Name)} init {LogStyle.Name(module.Name)} ({LogStyle.Value($"{progress.Value}/{totalModules}")}, {LogStyle.Muted(module.InitMode)})");

            if (module.InitMode == ModuleInitMode.Synchronous)
            {
                module.Initialize();
            }
            else
            {
                yield return module.InitializeAsync();
            }

            CompleteModule(group, module, serial);
        }

        IEnumerator InitializeWaveConcurrent(
            ModuleGroup group,
            IReadOnlyList<IGameModule> wave,
            InitProgress progress,
            int totalModules,
            int serial)
        {
            var pendingAsync = 0;
            Exception failure = null;

            for (var i = 0; i < wave.Count; i++)
            {
                var module = wave[i];
                progress.Value++;
                group.ReportProgress(progress.Value, totalModules);
                GameLog.Info(
                    LogCategories.Bootstrap,
                    $"Group {LogStyle.Name(group.Name)} init {LogStyle.Name(module.Name)} ({LogStyle.Value($"{progress.Value}/{totalModules}")}, {LogStyle.Muted(module.InitMode)}, {LogStyle.Muted("concurrent")})");

                if (module.InitMode == ModuleInitMode.Synchronous)
                {
                    try
                    {
                        module.Initialize();
                        CompleteModule(group, module, serial);
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                        break;
                    }

                    continue;
                }

                pendingAsync++;
                StartCoroutine(RunAsyncModule(group, module, serial, () => pendingAsync--, ex =>
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
            int serial,
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

            Exception failure = null;
            var driven = NestedCoroutine.Drive(routine, ex => failure = ex);
            while (failure == null && driven.MoveNext())
            {
                yield return driven.Current;
            }

            if (failure != null)
            {
                onError?.Invoke(failure);
                yield break;
            }

            CompleteModule(group, module, serial);
            onComplete?.Invoke();
        }

        static void CompleteModule(ModuleGroup group, IGameModule module, int serial)
        {
            if (group.RunSerial != serial || group.State != ModuleGroupState.Running)
            {
                try
                {
                    module.Shutdown();
                }
                catch (Exception ex)
                {
                    GameLog.Exception(LogCategories.Bootstrap, ex, $"Late shutdown failed: {module.Name}");
                }

                return;
            }

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
                var module = group.Initialized[i];
                try
                {
                    module.Shutdown();
                }
                catch (Exception ex)
                {
                    GameLog.Exception(LogCategories.Bootstrap, ex, $"Shutdown failed: {module.Name}");
                }
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
