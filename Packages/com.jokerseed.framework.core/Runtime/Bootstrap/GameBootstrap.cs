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
        readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

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
            var group = GetGroup(groupName);
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

        async UniTask RunGroup(ModuleGroup group, CancellationTokenSource runCancellation)
        {
            var serial = ++group.RunSerial;
            group.SetState(ModuleGroupState.Running);

            try
            {
                await RunGroupInternal(group, serial, runCancellation);
            }
            catch (Exception failure)
            {
                runCancellation.Cancel();
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
            if (group.Modules.Count == 0)
            {
                GameLog.Warning(LogCategories.Bootstrap, $"Group {LogStyle.Name(group.Name)} has no modules.");
                group.SetState(ModuleGroupState.Ready);
                return;
            }

            var waves = ModuleSorter.SortIntoWaves(group.Modules, CollectReadyModules(group.Name));
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
            progress.Value++;
            group.ReportProgress(progress.Value, totalModules);
            var mode = concurrent ? $", {LogStyle.Muted("concurrent")}" : string.Empty;
            GameLog.Info(
                LogCategories.Bootstrap,
                $"Group {LogStyle.Name(group.Name)} init {LogStyle.Name(module.Name)} ({LogStyle.Value($"{progress.Value}/{totalModules}")}, {LogStyle.Muted(module.InitMode)}{mode})");

            if (module.InitMode == ModuleInitMode.Synchronous)
            {
                module.Initialize();
            }
            else
            {
                await module.InitializeAsync(token);
            }

            CompleteModule(group, module, serial);
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
            var tasks = new UniTask[wave.Count];
            for (var i = 0; i < wave.Count; i++)
            {
                tasks[i] = InitializeModule(group, wave[i], progress, totalModules, serial, runCancellation.Token, true);
            }

            try
            {
                await UniTask.WhenAll(tasks);
            }
            catch
            {
                runCancellation.Cancel();
                throw;
            }
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
            _lifetime.Cancel();
            foreach (var pair in _groups)
            {
                ShutdownGroup(pair.Value);
            }

            _groups.Clear();
            _lifetime.Dispose();
            base.OnDestroy();
        }
    }
}
