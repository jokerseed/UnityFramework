using System.Collections.Generic;
using Framework.Core;
using Framework.FixedMath;

namespace Framework.GamePlay
{
    /// <summary>杂兵波次：槽位常驻，全灭后延迟回收复用到英雄周围。</summary>
    public sealed class BattleWaveDirector
    {
        readonly List<ActorId> _slots;
        readonly ActorId _heroId;
        readonly FP _baseHealth;
        readonly FP _waveDelay;
        readonly string _abilityId;
        FP _allDeadTimer;
        int _wave = 1;

        /// <summary>本波怪物近战技能 ID。</summary>
        public string AbilityId => _abilityId;

        /// <summary>当前波次，从 1 起。</summary>
        public int Wave => _wave;

        /// <summary>构造波次导演。</summary>
        /// <param name="slots">常驻怪物 ActorId 槽位；不可为 null。</param>
        /// <param name="heroId">追击的英雄 ID。</param>
        /// <param name="baseHealth">第 1 波生命。</param>
        /// <param name="abilityId">近战技能 ID。</param>
        /// <param name="waveDelay">全灭后到下一波的秒数。</param>
        public BattleWaveDirector(
            List<ActorId> slots,
            ActorId heroId,
            float baseHealth,
            string abilityId,
            float waveDelay = 2f)
        {
            _slots = slots;
            _heroId = heroId;
            _baseHealth = baseHealth;
            _abilityId = abilityId;
            _waveDelay = waveDelay > 0f ? waveDelay : (FP)2;
        }

        /// <summary>若本波全灭则计时，到点后把槽位复活到英雄周围。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="heroPosition">英雄当前仿真坐标。</param>
        /// <param name="deltaTime">帧间隔。</param>
        /// <returns>若本帧刷出新一波返回 true。</returns>
        public bool Tick(GamePlayFramework framework, TSVector heroPosition, FP deltaTime)
        {
            if (framework == null || _slots == null || _slots.Count == 0)
            {
                return false;
            }

            var alive = 0;
            for (var i = 0; i < _slots.Count; i++)
            {
                if (framework.TryGetActor(_slots[i], out var asc) && !asc.IsDead)
                {
                    alive++;
                }
            }

            if (alive > 0)
            {
                _allDeadTimer = FP.Zero;
                return false;
            }

            _allDeadTimer += deltaTime;
            if (_allDeadTimer < _waveDelay)
            {
                return false;
            }

            _wave++;
            _allDeadTimer = FP.Zero;
            var health = _baseHealth + (FP)(_wave - 1) * (FP)8;
            SpawnRing(framework, heroPosition, health);
            return true;
        }

        void SpawnRing(GamePlayFramework framework, TSVector heroPosition, FP health)
        {
            var count = _slots.Count;
            var radius = (FP)4.2f;
            var waveBias = (FP)_wave * (FP)0.35f;
            for (var i = 0; i < count; i++)
            {
                var angle = FP.Pi * 2 * i / count + waveBias;
                var position = heroPosition + new TSVector(TSMath.Cos(angle) * radius, 0, TSMath.Sin(angle) * radius);
                framework.ReviveActor(_slots[i], position, health);
            }
        }
    }
}
