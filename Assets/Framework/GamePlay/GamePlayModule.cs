using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Bootstrap;
using Framework.Config;
using Framework.Logging;

namespace Framework.GamePlay
{
    /// <summary>
    /// 玩法模块：创建并持有 <see cref="GamePlayFramework"/>，作为 GAS + ECS 玩法的 Bootstrap 入口。
    /// </summary>
    public sealed class GamePlayModule : IGameModule
    {
        static GamePlayModule s_instance;

        /// <summary>当前已初始化的玩法模块实例；未初始化时为 null。</summary>
        public static GamePlayModule Instance => s_instance;

        /// <summary>玩法运行时框架；Initialize 后可用。</summary>
        public GamePlayFramework Framework { get; private set; }

        /// <inheritdoc/>
        public string Name => "GamePlay";

        /// <inheritdoc/>
        public ModulePhase Phase => ModulePhase.Gameplay;

        /// <inheritdoc/>
        public IReadOnlyList<Type> Dependencies => new[] { typeof(ConfigModule) };

        /// <inheritdoc/>
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <inheritdoc/>
        public void Initialize()
        {
            s_instance = this;
            Framework = new GamePlayFramework();
            GameLog.Info(LogCategories.GamePlay, $"{LogStyle.Name(Name)} {LogStyle.Ok("ready")}");
        }

        /// <inheritdoc/>
        public IEnumerator InitializeAsync()
        {
            Initialize();
            yield break;
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            Framework?.Dispose();
            Framework = null;
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
    }
}
