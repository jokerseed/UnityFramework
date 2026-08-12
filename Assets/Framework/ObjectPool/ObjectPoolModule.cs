using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Bootstrap;
using Framework.Logging;
using Framework.MemoryPool;

namespace Framework.ObjectPool
{
    /// <summary>对象池模块：初始化 <see cref="ObjectPoolManager"/>，Shutdown 时销毁全部池。</summary>
    public sealed class ObjectPoolModule : IGameModule
    {
        /// <summary>模块名称。</summary>
        public string Name => "ObjectPool";

        /// <summary>模块阶段，归属基础设施层。</summary>
        public ModulePhase Phase => ModulePhase.Infrastructure;

        /// <summary>模块依赖列表；需要日志模块和内存池模块先完成初始化。</summary>
        public IReadOnlyList<Type> Dependencies => new[]
        {
            typeof(LoggingModule),
            typeof(MemoryPoolModule),
        };

        /// <summary>初始化方式，同步完成。</summary>
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <summary>初始化对象池模块，触发 <see cref="ObjectPoolManager"/> 单例创建。</summary>
        public void Initialize()
        {
            var manager = ObjectPoolManager.Instance;
            GameLog.Info(LogCategories.ObjectPool, $"Ready  Pools={LogStyle.Value(manager.Count)}");
        }

        /// <summary>异步初始化，内部直接调用同步 <see cref="Initialize"/>。</summary>
        /// <returns>协程迭代器。</returns>
        public IEnumerator InitializeAsync()
        {
            Initialize();
            yield break;
        }

        /// <summary>关闭模块，销毁全部对象池并释放 <see cref="ObjectPoolManager"/> 单例。</summary>
        public void Shutdown()
        {
            if (ObjectPoolManager.HasInstance)
            {
                ObjectPoolManager.Instance.DestroyAllObjectPools();
                ObjectPoolManager.DestroyInstance();
            }

            GameLog.Info(LogCategories.ObjectPool, LogStyle.Muted("shut down"));
        }
    }
}
