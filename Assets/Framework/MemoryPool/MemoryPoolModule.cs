using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Bootstrap;
using Framework.Logging;

namespace Framework.MemoryPool
{
    /// <summary>内存池模块：配置严格检查，Shutdown 时清空所有池。</summary>
    public sealed class MemoryPoolModule : IGameModule
    {
        readonly bool _enableStrictCheck;

        /// <summary>构造内存池模块。</summary>
        /// <param name="enableStrictCheck">是否开启严格检查；开启后重复 Release 会抛出异常，便于调试。</param>
        public MemoryPoolModule(bool enableStrictCheck = false)
        {
            _enableStrictCheck = enableStrictCheck;
        }

        /// <summary>模块名称。</summary>
        public string Name => "MemoryPool";

        /// <summary>模块阶段，归属基础设施层。</summary>
        public ModulePhase Phase => ModulePhase.Infrastructure;

        /// <summary>模块依赖列表；需要日志模块先完成初始化。</summary>
        public IReadOnlyList<Type> Dependencies => new[] { typeof(LoggingModule) };

        /// <summary>初始化方式，同步完成。</summary>
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <summary>初始化内存池模块，将严格检查配置应用到 <see cref="MemoryPool"/>。</summary>
        public void Initialize()
        {
            MemoryPool.EnableStrictCheck = _enableStrictCheck;
            GameLog.Info(LogCategories.MemoryPool, $"MemoryPool ready. StrictCheck={_enableStrictCheck}");
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
            GameLog.Info(LogCategories.MemoryPool, "MemoryPool cleared.");
        }
    }
}
