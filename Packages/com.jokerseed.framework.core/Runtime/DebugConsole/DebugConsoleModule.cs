using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;
using UnityEngine;

namespace Framework.DebugConsole
{
    /// <summary>
    /// 调试控制台模块：Editor 与 Development Build 下创建 <see cref="RuntimeConsole"/>；正式包默认不创建。
    /// </summary>
    public sealed class DebugConsoleModule : IGameModule
    {
        readonly bool _enableInRelease;
        bool _created;

        /// <summary>创建模块。</summary>
        /// <param name="enableInRelease">为 true 时正式包也创建控制台（例如内测包）。</param>
        public DebugConsoleModule(bool enableInRelease = false)
        {
            _enableInRelease = enableInRelease;
        }

        /// <inheritdoc />
        public string Name => "DebugConsole";

        /// <inheritdoc />
        public ModulePhase Phase => ModulePhase.Infrastructure;

        /// <inheritdoc />
        public IReadOnlyList<Type> Dependencies => new[] { typeof(LoggingModule) };

        /// <inheritdoc />
        public void Initialize()
        {
            if (!Debug.isDebugBuild && !_enableInRelease)
            {
                GameLog.Info(LogCategories.Debug, LogStyle.Muted("disabled in release build"));
                return;
            }

            RuntimeConsole.Instance.StartCapture();
            _created = true;
            GameLog.Info(LogCategories.Debug, "Ready");
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            if (_created && RuntimeConsole.HasInstance)
            {
                RuntimeConsole.Instance.StopCapture();
                RuntimeConsole.DestroyInstance();
            }

            _created = false;
            GameLog.Info(LogCategories.Debug, LogStyle.Muted("shut down"));
        }
    }
}
