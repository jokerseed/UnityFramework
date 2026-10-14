using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;

namespace Framework.Fsm
{
    /// <summary>
    /// 流程模块：初始化时把流程注册到 <see cref="ProcedureManager"/>，不自动启动。
    /// 启动组 Ready 后由业务调用 <see cref="ProcedureManager.StartProcedure{TProcedure}"/>，保证入口流程用到的模块都已就绪。
    /// </summary>
    public sealed class ProcedureModule : IGameModule
    {
        readonly ProcedureBase[] _procedures;

        /// <summary>创建流程模块。</summary>
        /// <param name="procedures">全部流程实例。</param>
        /// <exception cref="ArgumentNullException"><paramref name="procedures"/> 为 null。</exception>
        public ProcedureModule(params ProcedureBase[] procedures)
        {
            _procedures = procedures ?? throw new ArgumentNullException(nameof(procedures));
        }

        /// <inheritdoc />
        public string Name => "Procedure";

        /// <inheritdoc />
        public ModulePhase Phase => ModulePhase.Gameplay;

        /// <inheritdoc />
        public IReadOnlyList<Type> Dependencies => new[] { typeof(LoggingModule) };

        /// <inheritdoc />
        public void Initialize()
        {
            ProcedureManager.Instance.Initialize(_procedures);
            GameLog.Info(LogCategories.Procedure, $"Ready  Procedures={LogStyle.Value(_procedures.Length)}");
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            if (ProcedureManager.HasInstance)
            {
                ProcedureManager.Instance.Shutdown();
                ProcedureManager.DestroyInstance();
            }

            GameLog.Info(LogCategories.Procedure, LogStyle.Muted("shut down"));
        }
    }
}
