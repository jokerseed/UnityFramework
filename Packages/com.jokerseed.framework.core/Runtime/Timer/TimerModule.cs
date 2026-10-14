using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;

namespace Framework.Timer
{
    /// <summary>定时器模块：初始化 <see cref="TimerManager"/>，Shutdown 时取消全部定时器。</summary>
    public sealed class TimerModule : IGameModule
    {
        /// <inheritdoc />
        public string Name => "Timer";

        /// <inheritdoc />
        public ModulePhase Phase => ModulePhase.Infrastructure;

        /// <inheritdoc />
        public IReadOnlyList<Type> Dependencies => new[] { typeof(LoggingModule) };

        /// <inheritdoc />
        public void Initialize()
        {
            _ = TimerManager.Instance;
            GameLog.Info(LogCategories.Timer, "Ready");
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            if (TimerManager.HasInstance)
            {
                TimerManager.Instance.CancelAll();
                TimerManager.DestroyInstance();
            }

            GameLog.Info(LogCategories.Timer, LogStyle.Muted("shut down"));
        }
    }
}
