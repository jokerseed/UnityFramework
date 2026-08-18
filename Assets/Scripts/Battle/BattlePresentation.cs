using System;
using Framework.Config;
using Framework.GamePlay;
using Framework.GAS.Events;
using Framework.Core;
using Framework.Logging;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 战斗事件驱动表现：Cue、HitStop、伤害/死亡日志。
    /// </summary>
    public sealed class BattlePresentation
    {
        const float HitStopScale = 0.18f;
        const float HitStopSeconds = 0.05f;

        readonly BattleCuePresenter _cuePresenter = new BattleCuePresenter();
        Action<DamageDealtEvent> _onDamageDealt;
        Action<ActorDiedEvent> _onActorDied;
        Action<GameplayCueEvent> _onCue;
        GamePlayFramework _framework;
        ActorId _heroId;
        float _hitStopUntilUnscaled;

        /// <summary>订阅事件并绑定 Cue 表。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="heroId">英雄 ActorId（HitStop 判定用）。</param>
        public void Bind(GamePlayFramework framework, ActorId heroId)
        {
            _framework = framework;
            _heroId = heroId;

            var tables = ConfigManager.Instance.GetTables();
            _cuePresenter.Bind(tables.CfgTbCue);

            _onDamageDealt = OnDamageDealt;
            framework.EventBus.Subscribe(_onDamageDealt);
            _onActorDied = OnActorDied;
            framework.EventBus.Subscribe(_onActorDied);
            _onCue = OnGameplayCue;
            framework.EventBus.Subscribe(_onCue);
        }

        /// <summary>取消订阅并清理 Cue 表现。</summary>
        public void Unbind()
        {
            if (_framework != null)
            {
                if (_onDamageDealt != null) { _framework.EventBus.Unsubscribe(_onDamageDealt); _onDamageDealt = null; }
                if (_onActorDied != null) { _framework.EventBus.Unsubscribe(_onActorDied); _onActorDied = null; }
                if (_onCue != null) { _framework.EventBus.Unsubscribe(_onCue); _onCue = null; }
            }

            _cuePresenter.Clear();
            _framework = null;
            _heroId = default;
        }

        /// <summary>恢复已过期的 HitStop 时间缩放。</summary>
        public void RestoreHitStopIfExpired()
        {
            if (Time.timeScale < 1f && Time.unscaledTime >= _hitStopUntilUnscaled)
            {
                Time.timeScale = 1f;
            }
        }

        /// <summary>更新持续 Cue 与到期瞬时 Cue。</summary>
        public void Tick()
        {
            _cuePresenter.Tick(ResolveCuePosition);
        }

        void OnDamageDealt(DamageDealtEvent e)
        {
            GameLog.Info(LogCategories.GamePlay,
                $"Damage {LogStyle.Value(e.Source.Value)} → {LogStyle.Value(e.Target.Value)}  ability={LogStyle.Name(e.AbilityId)}  final={LogStyle.Value(e.FinalDamage.ToString("F1"))}  crit={e.IsCrit}");

            if (e.Source != _heroId || e.FinalDamage <= 0f)
            {
                return;
            }

            Time.timeScale = HitStopScale;
            _hitStopUntilUnscaled = Time.unscaledTime + HitStopSeconds;
        }

        static void OnActorDied(ActorDiedEvent e)
        {
            GameLog.Info(LogCategories.GamePlay,
                $"Actor died {LogStyle.Value(e.Actor.Value)}  killer={LogStyle.Value(e.Killer.Value)}  ability={LogStyle.Name(e.AbilityId ?? "")}");
        }

        void OnGameplayCue(GameplayCueEvent e) => _cuePresenter.Handle(e);

        Vector3? ResolveCuePosition(ActorId actorId)
        {
            if (_framework != null && _framework.Registry.TryGet(actorId, out var actor))
            {
                return actor.Position;
            }

            return null;
        }
    }
}
