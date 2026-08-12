using Framework.Core;
using UnityEngine;

namespace Framework.Logging
{
    /// <summary>
    /// 日志常驻管理器：在 Inspector 中配置 <see cref="LogInitOptions"/>，由 <see cref="LoggingModule"/> 读取并应用到 <see cref="GameLog"/>。
    /// </summary>
    public sealed class LoggingManager : PersistentSingleton<LoggingManager>
    {
        [SerializeField] LogInitOptions _options = new LogInitOptions();

        /// <summary>Inspector 中配置的日志初始化选项；未赋值时返回默认配置。</summary>
        public LogInitOptions Options
        {
            get
            {
                if (_options == null)
                {
                    _options = new LogInitOptions();
                }

                return _options;
            }
        }
    }
}
