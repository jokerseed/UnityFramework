using System;
using System.Collections.Generic;
using Framework.Core;

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
        /// <param name="modules">待排序的模块列表，不可含重复类型；可为 null 或空。模块的 <see cref="IGameModule.Dependencies"/> 为 null 时视为无依赖，重复类型只计一次。依赖可以是具体类型或接口；同一个依赖匹配到多个模块时失败。</param>
        /// <param name="alreadyAvailable">其它组里已经就绪的模块。它们能满足依赖，但不会出现在返回的波次里。可为 null。</param>
        /// <returns>波次列表，每个波次为可并发执行的模块子集；输入为空时返回空数组。</returns>
        /// <exception cref="InvalidOperationException">存在循环依赖、重复注册、依赖为 null、依赖匹配到多个模块，或依赖模块未注册。</exception>
        public static IReadOnlyList<IReadOnlyList<IGameModule>> SortIntoWaves(
            IReadOnlyList<IGameModule> modules,
            IReadOnlyList<IGameModule> alreadyAvailable = null)
        {
            if (modules == null || modules.Count == 0)
            {
                return Array.Empty<IReadOnlyList<IGameModule>>();
            }

            var moduleByType = BuildModuleMap(modules);
            var external = BuildExternalMap(alreadyAvailable, moduleByType);
            var inDegree = BuildInDegree(modules, moduleByType, external);

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
                        if (!DependsOn(candidate.Dependencies, type))
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

        static Dictionary<Type, IGameModule> BuildExternalMap(
            IReadOnlyList<IGameModule> alreadyAvailable,
            Dictionary<Type, IGameModule> local)
        {
            var external = new Dictionary<Type, IGameModule>();
            if (alreadyAvailable == null)
            {
                return external;
            }

            for (var i = 0; i < alreadyAvailable.Count; i++)
            {
                var module = alreadyAvailable[i];
                if (module == null)
                {
                    continue;
                }

                var type = module.GetType();
                if (local.ContainsKey(type))
                {
                    continue;
                }

                if (external.ContainsKey(type))
                {
                    throw new InvalidOperationException($"Duplicate module registered: {type.Name}");
                }

                external[type] = module;
            }

            return external;
        }

        static Dictionary<Type, int> BuildInDegree(
            IReadOnlyList<IGameModule> modules,
            Dictionary<Type, IGameModule> moduleByType,
            Dictionary<Type, IGameModule> external)
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
                inDegree[module.GetType()] = CountDependencies(module, dependencies, moduleByType, external);
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

        static bool DependsOn(IReadOnlyList<Type> dependencies, Type completedType)
        {
            if (dependencies == null)
            {
                return false;
            }

            for (var i = 0; i < dependencies.Count; i++)
            {
                var dependency = dependencies[i];
                if (dependency == completedType || dependency.IsAssignableFrom(completedType))
                {
                    return true;
                }
            }

            return false;
        }

        static int CountDependencies(
            IGameModule module,
            IReadOnlyList<Type> dependencies,
            Dictionary<Type, IGameModule> moduleByType,
            Dictionary<Type, IGameModule> external)
        {
            if (dependencies == null)
            {
                return 0;
            }

            var seen = new List<Type>(dependencies.Count);
            for (var i = 0; i < dependencies.Count; i++)
            {
                var dependency = dependencies[i];
                if (dependency == null)
                {
                    throw new InvalidOperationException($"Module '{module.Name}' has a null dependency.");
                }

                var resolved = ResolveDependency(module, dependency, moduleByType, external, out var satisfiedOutside);
                if (satisfiedOutside)
                {
                    continue;
                }

                var duplicate = false;
                for (var j = 0; j < seen.Count; j++)
                {
                    if (seen[j] == resolved)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    seen.Add(resolved);
                }
            }

            return seen.Count;
        }

        static Type ResolveDependency(
            IGameModule module,
            Type dependency,
            Dictionary<Type, IGameModule> moduleByType,
            Dictionary<Type, IGameModule> external,
            out bool satisfiedOutside)
        {
            if (moduleByType.ContainsKey(dependency))
            {
                satisfiedOutside = false;
                return dependency;
            }

            if (external != null && external.ContainsKey(dependency))
            {
                satisfiedOutside = true;
                return dependency;
            }

            Type localMatch = null;
            var localCount = 0;
            foreach (var pair in moduleByType)
            {
                if (dependency.IsAssignableFrom(pair.Key))
                {
                    localCount++;
                    localMatch = pair.Key;
                }
            }

            Type externalMatch = null;
            var externalCount = 0;
            if (external != null)
            {
                foreach (var pair in external)
                {
                    if (dependency.IsAssignableFrom(pair.Key))
                    {
                        externalCount++;
                        externalMatch = pair.Key;
                    }
                }
            }

            var total = localCount + externalCount;
            if (total > 1)
            {
                throw new InvalidOperationException(
                    $"Module '{module.Name}' depends on '{dependency.Name}', which matches {total} modules.");
            }

            if (localCount == 1)
            {
                satisfiedOutside = false;
                return localMatch;
            }

            if (externalCount == 1)
            {
                satisfiedOutside = true;
                return externalMatch;
            }

            throw new InvalidOperationException(
                $"Module '{module.Name}' depends on '{dependency.Name}', but it is not registered.");
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
