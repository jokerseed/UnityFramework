using System;
using System.Collections.Generic;

namespace Framework.Bootstrap
{
    /// <summary>模块拓扑排序工具：按依赖关系将模块排列为有序波次（Kahn 算法）。</summary>
    public static class ModuleSorter
    {
        /// <summary>
        /// 将模块列表按依赖关系拓扑排序，返回可顺序初始化的平铺列表。
        /// </summary>
        /// <param name="modules">待排序的模块列表，不可含重复类型。</param>
        /// <returns>拓扑排序后的有序模块列表；输入为空时返回空数组。</returns>
        /// <exception cref="InvalidOperationException">存在循环依赖、重复注册或依赖模块未注册。</exception>
        public static IReadOnlyList<IGameModule> Sort(IReadOnlyList<IGameModule> modules)
        {
            var waves = SortIntoWaves(modules);
            if (waves.Count == 0)
            {
                return Array.Empty<IGameModule>();
            }

            var ordered = new List<IGameModule>(modules.Count);
            for (var i = 0; i < waves.Count; i++)
            {
                ordered.AddRange(waves[i]);
            }

            return ordered;
        }

        /// <summary>
        /// 将模块列表按依赖关系拓扑排序，返回可并发执行的波次列表。
        /// 同一波次内的模块互无依赖，可并发初始化。
        /// </summary>
        /// <param name="modules">待排序的模块列表，不可含重复类型；可为 null 或空。</param>
        /// <returns>波次列表，每个波次为可并发执行的模块子集；输入为空时返回空数组。</returns>
        /// <exception cref="InvalidOperationException">存在循环依赖、重复注册或依赖模块未注册。</exception>
        public static IReadOnlyList<IReadOnlyList<IGameModule>> SortIntoWaves(IReadOnlyList<IGameModule> modules)
        {
            if (modules == null || modules.Count == 0)
            {
                return Array.Empty<IReadOnlyList<IGameModule>>();
            }

            var moduleByType = BuildModuleMap(modules);
            var inDegree = BuildInDegree(modules, moduleByType);

            var ready = CollectZeroInDegree(inDegree);
            var waves = new List<IReadOnlyList<IGameModule>>();

            while (ready.Count > 0)
            {
                ready.Sort((a, b) => CompareModules(moduleByType[a], moduleByType[b]));

                var wave = new List<IGameModule>(ready.Count);
                for (var i = 0; i < ready.Count; i++)
                {
                    wave.Add(moduleByType[ready[i]]);
                }

                waves.Add(wave);

                var currentWave = ready.ToArray();
                ready.Clear();

                for (var i = 0; i < currentWave.Length; i++)
                {
                    var type = currentWave[i];
                    for (var j = 0; j < modules.Count; j++)
                    {
                        var candidate = modules[j];
                        if (!ContainsDependency(candidate.Dependencies, type))
                        {
                            continue;
                        }

                        var candidateType = candidate.GetType();
                        inDegree[candidateType]--;
                        if (inDegree[candidateType] != 0)
                        {
                            continue;
                        }

                        ready.Add(candidateType);
                    }
                }

                ready.Sort((a, b) => CompareModules(moduleByType[a], moduleByType[b]));
            }

            var total = 0;
            for (var i = 0; i < waves.Count; i++)
            {
                total += waves[i].Count;
            }

            if (total != modules.Count)
            {
                throw new InvalidOperationException("Circular module dependency detected.");
            }

            return waves;
        }

        static Dictionary<Type, IGameModule> BuildModuleMap(IReadOnlyList<IGameModule> modules)
        {
            var moduleByType = new Dictionary<Type, IGameModule>(modules.Count);
            for (var i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                var type = module.GetType();
                if (moduleByType.ContainsKey(type))
                {
                    throw new InvalidOperationException($"Duplicate module registered: {type.Name}");
                }

                moduleByType[type] = module;
            }

            return moduleByType;
        }

        static Dictionary<Type, int> BuildInDegree(
            IReadOnlyList<IGameModule> modules,
            Dictionary<Type, IGameModule> moduleByType)
        {
            var inDegree = new Dictionary<Type, int>(modules.Count);
            foreach (var pair in moduleByType)
            {
                inDegree[pair.Key] = 0;
            }

            for (var i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                var dependencies = module.Dependencies;
                inDegree[module.GetType()] = dependencies.Count;

                for (var j = 0; j < dependencies.Count; j++)
                {
                    var dependency = dependencies[j];
                    if (!moduleByType.ContainsKey(dependency))
                    {
                        throw new InvalidOperationException(
                            $"Module '{module.Name}' depends on '{dependency.Name}', but it is not registered.");
                    }
                }
            }

            return inDegree;
        }

        static List<Type> CollectZeroInDegree(Dictionary<Type, int> inDegree)
        {
            var ready = new List<Type>();
            foreach (var pair in inDegree)
            {
                if (pair.Value == 0)
                {
                    ready.Add(pair.Key);
                }
            }

            return ready;
        }

        static bool ContainsDependency(IReadOnlyList<Type> dependencies, Type dependency)
        {
            for (var i = 0; i < dependencies.Count; i++)
            {
                if (dependencies[i] == dependency)
                {
                    return true;
                }
            }

            return false;
        }

        static int CompareModules(IGameModule left, IGameModule right)
        {
            var phaseCompare = left.Phase.CompareTo(right.Phase);
            if (phaseCompare != 0)
            {
                return phaseCompare;
            }

            return string.Compare(left.Name, right.Name, StringComparison.Ordinal);
        }
    }
}
