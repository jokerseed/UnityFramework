using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;

namespace Framework.MemoryPool
{
    /// <summary>内存池模块：从 <see cref="MemoryPoolManager"/> 读取严格检查，Shutdown 时清空所有池。</summary>
    public sealed class MemoryPoolModule : IGameModule
    {
        /// <summary>模块名称。</summary>
        public string Name => "MemoryPool";

        /// <summary>模块阶段，归属基础设施层。</summary>
        public ModulePhase Phase => ModulePhase.Infrastructure;

        /// <summary>模块依赖列表；需要日志模块先完成初始化。</summary>
        public IReadOnlyList<Type> Dependencies => new[] { typeof(LoggingModule) };

        /// <summary>初始化方式，同步完成。</summary>
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <summary>初始化内存池模块，将 <see cref="MemoryPoolManager.EnableStrictCheck"/> 应用到 <see cref="MemoryPool"/>。</summary>
        public void Initialize()
        {
            var enableStrictCheck = MemoryPoolManager.Instance.EnableStrictCheck;
            MemoryPool.EnableStrictCheck = enableStrictCheck;
            GameLog.Info(LogCategories.MemoryPool, $"Ready  StrictCheck={LogStyle.Value(enableStrictCheck)}");
        }

        /// <summary>异步初始化，内部直接调用同步 <see cref="Initialize"/>。</summary>
        /// <returns>协程迭代器。</returns>
        public IEnumerator InitializeAsync()
        {
            Initialize();
            yield break;
        }

        /// <summary>关闭模块，清空所有内存池中的闲置对象。</summary>
        public void Shutdown()
        {
            MemoryPool.ClearAll();
            GameLog.Info(LogCategories.MemoryPool, LogStyle.Muted("cleared"));
        }
    }
}
