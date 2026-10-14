using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;
using UnityEngine;

namespace Framework.GamePlay
{
    /// <summary>
    /// 玩法模块：持有 <see cref="BattleDirector"/>，作为 GAS + ECS 玩法的 Bootstrap 入口。
    /// 业务层通过 <see cref="Director"/> 创建 / 销毁 / 获取 <see cref="BattleSession"/>。
    /// </summary>
    public sealed class GamePlayModule : IGameModule
    {
        static GamePlayModule s_instance;

        /// <summary>当前已初始化的玩法模块实例；未初始化时为 null。</summary>
        public static GamePlayModule Instance => s_instance;

        /// <summary>战斗会话管理器：Session 工厂 + 集中 Tick。</summary>
        public BattleDirector Director { get; private set; }

        /// <inheritdoc/>
        public string Name => "GamePlay";

        /// <inheritdoc/>
        public ModulePhase Phase => ModulePhase.Gameplay;

        /// <inheritdoc/>
        public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

        /// <inheritdoc/>
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <inheritdoc/>
        public void Initialize()
        {
            s_instance = this;
            Director = new BattleDirector();
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
            Director?.Dispose();
            Director = null;
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
    }
}
