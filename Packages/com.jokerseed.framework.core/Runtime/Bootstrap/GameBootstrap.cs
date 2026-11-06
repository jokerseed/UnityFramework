using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        readonly List<ModuleGroup> _startOrder = new List<ModuleGroup>();
        bool _destroying;
        readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        /// <summary>
        /// 注册或替换指定组的模块列表；组不存在时自动创建。
        /// 组处于 <see cref="ModuleGroupState.Ready"/> 时禁止重新配置，需先调用 <see cref="ResetGroup"/>。
        /// </summary>
        /// <param name="groupName">组名，不可为空白字符串。</param>
        /// <param name="modules">要注册的模块集合；顺序无强制要求，由拓扑排序决定初始化顺序。</param>
        /// <returns>对应的 <see cref="ModuleGroup"/> 实例，可用于监听事件。</returns>
        /// <exception cref="ArgumentException"><paramref name="groupName"/> 为空或空白，或模块集合含 null。</exception>
        /// <exception cref="InvalidOperationException">组正在运行、就绪或派发通知；模块类型重复、实例已由其它组拥有，或独占服务模块类型已由其它组注册。</exception>
        public ModuleGroup SetModules(string groupName, IEnumerable<IGameModule> modules)
        {
            ThrowIfDestroyed();
            if (string.IsNullOrWhiteSpace(groupName))
            {
                throw new ArgumentException("Group name is required.", nameof(groupName));
            }

            if (!_groups.TryGetValue(groupName, out var group))
            {
                group = new ModuleGroup(groupName);
                _groups[groupName] = group;
            }

            if (group.State == ModuleGroupState.Ready || group.State == ModuleGroupState.Running || group.IsNotifying || group.IsResetting)
            {
                throw new InvalidOperationException(
                    $"Group '{groupName}' is already ready. Call ResetGroup(\"{groupName}\") before reconfiguring.");
            }

            // Validate a snapshot before changing the existing configuration.
            var snapshot = modules == null ? new List<IGameModule>() : new List<IGameModule>(modules);
            var types = new HashSet<Type>();
            foreach (var module in snapshot)
            {
                if (module == null) throw new ArgumentException("Modules cannot contain null.", nameof(modules));
                if (!types.Add(module.GetType())) throw new InvalidOperationException($"Duplicate module: {module.GetType().Name}");
                foreach (var other in _groups.Values)
                {
                    if (ReferenceEquals(other, group)) continue;
                    foreach (var owned in other.Modules)
                        if (ReferenceEquals(owned, module) ||
                            (owned.GetType() == module.GetType() &&
                             (owned is IExclusiveGameModule || module is IExclusiveGameModule)))
                            throw new InvalidOperationException($"Module '{module.Name}' is owned by group '{other.Name}'.");
                }
            }
            group.Configure(snapshot);
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
        /// 启动指定组的初始化，不等待结果（内部调用 <see cref="RunAsync"/>）。
        /// 结果通过 <see cref="ModuleGroup"/> 的 Ready / Failed 事件获知。
        /// </summary>
        /// <param name="groupName">要启动的组名。</param>
        /// <exception cref="KeyNotFoundException">指定名称的模块组不存在。</exception>
        public void Run(string groupName)
        {
            GetGroup(groupName);
            RunAsync(groupName).Forget();
        }

        /// <summary>
        /// 初始化指定模块组并等待结束。
        /// 若组已在运行则等待其结束；已就绪则立即返回。
        /// 模块初始化失败不会抛出：组进入 <see cref="ModuleGroupState.Failed"/>、触发 <see cref="ModuleGroup.Failed"/>，调用方检查 <see cref="ModuleGroup.State"/>。
        /// </summary>
        /// <param name="groupName">要启动的组名。</param>
        /// <param name="cancellationToken">取消时中止初始化，已初始化的模块逆序关闭，组进入 Failed。Bootstrap 销毁时也会取消。</param>
        /// <returns>初始化结束（成功、失败或取消）的任务。</returns>
        /// <exception cref="KeyNotFoundException">指定名称的模块组不存在。</exception>
        public async UniTask RunAsync(string groupName, CancellationToken cancellationToken = default)
        {
            ThrowIfDestroyed();
            var group = GetGroup(groupName);
            if (group.IsResetting || group.IsNotifying)
                throw new InvalidOperationException($"Group '{groupName}' is resetting or notifying observers.");
            if (group.State == ModuleGroupState.Running)
            {
                await UniTask.WaitWhile(() => group.State == ModuleGroupState.Running, cancellationToken: cancellationToken);
                return;
            }

            if (group.State == ModuleGroupState.Ready)
            {
                return;
            }

            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token))
            {
                await RunGroup(group, linked);
            }
        }

        /// <summary>
        /// 关闭并重置指定模块组：逆序调用所有已初始化模块的 Shutdown，清空模块列表，并将组状态置回 Idle。
        /// </summary>
        /// <param name="groupName">要重置的组名。</param>
        /// <exception cref="KeyNotFoundException">指定名称的模块组不存在。</exception>
        /// <exception cref="InvalidOperationException">该组仍在运行、派发通知或重置；或其它活动组依赖该组。</exception>
        public void ResetGroup(string groupName)
        {
            ThrowIfDestroyed();
            var group = GetGroup(groupName);
            if (group.State == ModuleGroupState.Running || group.IsNotifying || group.IsResetting)
            {
                throw new InvalidOperationException(
                    $"Group '{groupName}' is running. Wait until it finishes before ResetGroup.");
            }

            foreach (var dependent in _groups.Values)
                if (dependent.Providers.Contains(group))
                    throw new InvalidOperationException($"Reset dependent group '{dependent.Name}' before '{group.Name}'.");

            group.IsResetting = true;
            try
            {
                ShutdownGroup(group);
                _startOrder.Remove(group);
                group.Providers.Clear();
                group.Modules.Clear();
                group.Initialized.Clear();
                group.SetState(ModuleGroupState.Idle);
            }
            finally { group.IsResetting = false; }
        }

        async UniTask RunGroup(ModuleGroup group, CancellationTokenSource runCancellation)
        {
            _startOrder.Remove(group);
            _startOrder.Add(group);
            var serial = ++group.RunSerial;
            group.SetState(ModuleGroupState.Running);

            try
            {
                await RunGroupInternal(group, serial, runCancellation);
            }
            catch (Exception failure)
            {
                CancelSafely(runCancellation);
                if (failure is OperationCanceledException)
                {
                    GameLog.Warning(LogCategories.Bootstrap, $"Group {LogStyle.Name(group.Name)} cancelled.");
                }
                else
                {
                    GameLog.Exception(LogCategories.Bootstrap, failure);
                }

                group.RunSerial++;
                try
                {
                    ShutdownGroup(group);
                }
                catch (Exception shutdownError)
                {
                    GameLog.Exception(LogCategories.Bootstrap, shutdownError);
                }

                group.Providers.Clear();
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

        async UniTask RunGroupInternal(ModuleGroup group, int serial, CancellationTokenSource runCancellation)
        {
            runCancellation.Token.ThrowIfCancellationRequested();
            if (group.Modules.Count == 0)
            {
                GameLog.Warning(LogCategories.Bootstrap, $"Group {LogStyle.Name(group.Name)} has no modules.");
                group.SetState(ModuleGroupState.Ready);
                return;
            }

            var available = CollectReadyModules(group.Name);
            var waves = ModuleSorter.SortIntoWaves(group.Modules, available);
            var dependencies = ModuleSorter.FindExternalDependencies(group.Modules, available);
            foreach (var provider in _groups.Values)
                foreach (var module in provider.Modules)
                    if (dependencies.Contains(module)) group.Providers.Add(provider);
            var progress = new InitProgress();
            var totalModules = group.Modules.Count;
            var token = runCancellation.Token;

            for (var waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                token.ThrowIfCancellationRequested();
                var wave = waves[waveIndex];
                if (CanRunConcurrently(wave))
                {
                    GameLog.Info(
                        LogCategories.Bootstrap,
                        $"Group {LogStyle.Name(group.Name)} wave {LogStyle.Value($"{waveIndex + 1}/{waves.Count}")} {LogStyle.Muted("concurrent")} ({LogStyle.Value(wave.Count)} modules)");
                    await InitializeWaveConcurrent(group, wave, progress, totalModules, serial, runCancellation);
                }
                else
                {
                    for (var i = 0; i < wave.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        await InitializeModule(group, wave[i], progress, totalModules, serial, token, false);
                    }
                }
            }

            token.ThrowIfCancellationRequested();
            group.SetState(ModuleGroupState.Ready);
            GameLog.Info(LogCategories.Bootstrap, $"Group {LogStyle.Name(group.Name)} {LogStyle.Ok("ready")}");
        }

        static async UniTask InitializeModule(
            ModuleGroup group,
            IGameModule module,
            InitProgress progress,
            int totalModules,
            int serial,
            CancellationToken token,
            bool concurrent)
        {
            var mode = concurrent ? $", {LogStyle.Muted("concurrent")}" : string.Empty;
            GameLog.Info(
                LogCategories.Bootstrap,
                $"Group {LogStyle.Name(group.Name)} init {LogStyle.Name(module.Name)} ({LogStyle.Value($"{progress.Value}/{totalModules}")} done, {LogStyle.Muted(module.InitMode)}{mode})");

            if (module.InitMode == ModuleInitMode.Synchronous)
            {
                module.Initialize();
            }
            else
            {
                await module.InitializeAsync(token);
            }

            if (CompleteModule(group, module, serial))
            {
                // 进度是"已完成数"：模块初始化成功后才计入，最后一个模块完成时才报 n/n。
                progress.Value++;
                group.ReportProgress(progress.Value, totalModules);
            }
        }

        /// <summary>
        /// 同波次全部模块同时开始；任一模块失败立即取消其余模块并抛出该异常。
        /// 失败后才完成的模块由 <see cref="CompleteModule"/> 按代号判定为迟到并立即关闭。
        /// </summary>
        static async UniTask InitializeWaveConcurrent(
            ModuleGroup group,
            IReadOnlyList<IGameModule> wave,
            InitProgress progress,
            int totalModules,
            int serial,
            CancellationTokenSource runCancellation)
        {
            // Each wrapper absorbs its failure, so WhenAll drains every initializer before
            // rollback/retry can reuse an instance. Cancellation is still signalled immediately.
            var tasks = new UniTask[wave.Count];
            Exception failure = null;
            for (var i = 0; i < wave.Count; i++)
                tasks[i] = Observe(wave[i]);
            await UniTask.WhenAll(tasks);
            if (failure != null) throw failure;

            async UniTask Observe(IGameModule module)
            {
                try
                {
                    runCancellation.Token.ThrowIfCancellationRequested();
                    await InitializeModule(group, module, progress, totalModules, serial, runCancellation.Token, true);
                }
                catch (Exception error)
                {
                    if (failure == null) failure = error;
                    CancelSafely(runCancellation);
                }
            }
        }

        /// <returns>模块计入本次初始化时返回 true；迟到（组已失败或重启）时立即关闭并返回 false。</returns>
        static bool CompleteModule(ModuleGroup group, IGameModule module, int serial)
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

                return false;
            }

            group.Initialized.Add(module);
            return true;
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
            // Detach ownership before calling user code, which may destroy the host.
            var initialized = group.Initialized.ToArray();
            group.Initialized.Clear();
            for (var i = initialized.Length - 1; i >= 0; i--)
            {
                var module = initialized[i];
                try
                {
                    module.Shutdown();
                }
                catch (Exception ex)
                {
                    GameLog.Exception(LogCategories.Bootstrap, ex, $"Shutdown failed: {module.Name}");
                }
            }

        }

        void ThrowIfDestroyed()
        {
            if (_destroying) throw new ObjectDisposedException(nameof(GameBootstrap));
        }

        static void CancelSafely(CancellationTokenSource source)
        {
            try { source.Cancel(); }
            catch (Exception error) { GameLog.Exception(LogCategories.Bootstrap, error); }
        }

        /// <inheritdoc />
        protected override void OnSingletonDestroy()
        {
            _destroying = true;
            // Invalidate completions before cancellation invokes user callbacks.
            foreach (var group in _startOrder) group.RunSerial++;
            CancelSafely(_lifetime);
            for (var i = _startOrder.Count - 1; i >= 0; i--)
                ShutdownGroup(_startOrder[i]);
            _startOrder.Clear();
            _groups.Clear();
            _lifetime.Dispose();
            base.OnSingletonDestroy();
        }
    }
}
