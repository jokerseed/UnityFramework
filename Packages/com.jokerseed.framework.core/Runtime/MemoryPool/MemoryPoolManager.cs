using Framework.Core;
using UnityEngine;

namespace Framework.MemoryPool
{
    /// <summary>
    /// 内存池常驻管理器：在 Inspector 中配置严格检查，由 <see cref="MemoryPoolModule"/> 应用到 <see cref="MemoryPool"/>。
    /// </summary>
    public sealed class MemoryPoolManager : PersistentSingleton<MemoryPoolManager>
    {
        [SerializeField] bool _enableStrictCheck = true;

        /// <summary>是否开启严格检查（重复 Release 时抛异常）。Editor / 调试建议开启。</summary>
        public bool EnableStrictCheck => _enableStrictCheck;
    }
}
