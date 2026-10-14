using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.Core.Snapshot;
using Framework.FixedMath;
using Framework.GAS.Targeting;
using UnityEngine;

namespace Framework.GAS.Abilities.Tasks
{
    /// <summary>近战扇形判定：前摇 → 窗口内每帧查询且每目标只结算一次 → 后摇。</summary>
    public sealed class MeleeSweepTask : AbilityTask
    {
        enum Phase
        {
            Windup,
            Hitting,
            Recovery
        }

        readonly AbilitySystemComponent _owner;
        readonly BattleContext _battle;
        readonly ConeEnemyQuery _queryCone;
        readonly FP _windup;
        readonly FP _hitDuration;
        readonly FP _recovery;
        readonly FP _range;
        readonly FP _halfAngleDegrees;
        readonly FP _damage;
        readonly FP _knockback;
        readonly string _abilityId;
        readonly string _hitEffectId;
        readonly string _comboEffectId;
        readonly BattleDamageType _damageType;
        readonly HashSet<uint> _hitOnce = new HashSet<uint>();
        readonly List<ActorId> _scratch = new List<ActorId>(16);

        Phase _phase = Phase.Windup;
        FP _elapsed;
        bool _comboGranted;

        /// <summary>构造近战扇形判定任务。</summary>
        /// <param name="owner">施法者 ASC。</param>
        /// <param name="battle">战斗上下文。</param>
        /// <param name="queryCone">扇形敌对查询；不可为 null。</param>
        /// <param name="windup">前摇秒数。</param>
        /// <param name="hitDuration">判定窗口秒数；≤0 时在前摇结束瞬间打一帧。</param>
        /// <param name="recovery">后摇秒数。</param>
        /// <param name="range">扇形半径（米）。</param>
        /// <param name="halfAngleDegrees">扇形半角（度）。</param>
        /// <param name="damage">对每个目标的伤害。</param>
        /// <param name="knockback">击退位移长度（米）；≤0 不击退。</param>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="hitEffectId">命中施加的效果 ID；空则不施加。</param>
        /// <param name="comboEffectId">进入后摇时对自身施加的连招窗口效果 ID；空则不施加。</param>
        /// <param name="damageType">伤害类型。</param>
        public MeleeSweepTask(
            AbilitySystemComponent owner,
            BattleContext battle,
            ConeEnemyQuery queryCone,
            FP windup,
            FP hitDuration,
            FP recovery,
            FP range,
            FP halfAngleDegrees,
            FP damage,
            FP knockback,
            string abilityId,
            string hitEffectId,
            string comboEffectId,
            BattleDamageType damageType)
        {
            _owner = owner;
            _battle = battle;
            _queryCone = queryCone;
            _windup = windup;
            _hitDuration = hitDuration;
            _recovery = recovery;
            _range = range;
            _halfAngleDegrees = halfAngleDegrees;
            _damage = damage;
            _knockback = knockback;
            _abilityId = abilityId;
            _hitEffectId = hitEffectId;
            _comboEffectId = comboEffectId;
            _damageType = damageType;
        }

        /// <inheritdoc/>
        public override void Tick(FP deltaTime)
        {
            if (IsDone || IsCancelled)
            {
                return;
            }

            _elapsed += deltaTime;
            switch (_phase)
            {
                case Phase.Windup:
                    if (_elapsed >= _windup)
                    {
                        _phase = Phase.Hitting;
                        _elapsed = FP.Zero;
                        TryGrantComboWindow();
                        Sweep();
                        if (_hitDuration <= FP.Zero)
                        {
                            EnterRecovery();
                        }
                    }

                    break;
                case Phase.Hitting:
                    Sweep();
                    if (_elapsed >= _hitDuration)
                    {
                        EnterRecovery();
                    }

                    break;
                default:
                    if (_elapsed >= _recovery)
                    {
                        Finish();
                    }

                    break;
            }
        }

        /// <inheritdoc/>
        protected override void OnCancel() => Finish();

        /// <inheritdoc />
        public override int ProgressHash()
        {
            unchecked
            {
                var hash = base.ProgressHash();
                hash = (hash * 16777619) ^ (int)_phase;
                hash = (hash * 16777619) ^ _elapsed.RawValue.GetHashCode();
                hash = (hash * 16777619) ^ (_comboGranted ? 1 : 0);
                _scratch.Clear();
                foreach (var id in _hitOnce)
                {
                    _scratch.Add(new ActorId(id));
                }

                _scratch.Sort(CompareActor);
                hash = (hash * 16777619) ^ _scratch.Count;
                for (var i = 0; i < _scratch.Count; i++)
                {
                    hash = (hash * 16777619) ^ (int)_scratch[i].Value;
                }

                return hash;
            }
        }

        /// <summary>写入阶段、计时、连招标记和已命中目标。</summary>
        /// <param name="writer">写入器。</param>
        internal void WriteProgress(SnapshotWriter writer)
        {
            writer.WriteInt((int)_phase);
            writer.WriteFp(_elapsed);
            writer.WriteBool(_comboGranted);
            writer.WriteBool(IsDone);
            writer.WriteBool(IsCancelled);
            _scratch.Clear();
            foreach (var id in _hitOnce)
            {
                _scratch.Add(new ActorId(id));
            }

            _scratch.Sort(CompareActor);
            writer.WriteInt(_scratch.Count);
            for (var i = 0; i < _scratch.Count; i++)
            {
                writer.WriteUInt(_scratch[i].Value);
            }
        }

        /// <summary>读回阶段、计时、连招标记和已命中目标。</summary>
        /// <param name="reader">读取器。</param>
        internal void ReadProgress(SnapshotReader reader)
        {
            _phase = (Phase)reader.ReadInt();
            _elapsed = reader.ReadFp();
            _comboGranted = reader.ReadBool();
            RestoreFlags(reader.ReadBool(), reader.ReadBool());
            _hitOnce.Clear();
            var count = reader.ReadInt();
            for (var i = 0; i < count; i++)
            {
                _hitOnce.Add(reader.ReadUInt());
            }
        }

        static int CompareActor(ActorId left, ActorId right) => left.Value.CompareTo(right.Value);

        void EnterRecovery()
        {
            _phase = Phase.Recovery;
            _elapsed = FP.Zero;
            if (_recovery <= FP.Zero)
            {
                Finish();
            }
        }

        void Sweep()
        {
            if (_queryCone == null || _owner == null || _battle == null)
            {
                return;
            }

            var origin = _owner.CueSimPosition;
            var direction = Instance.Context.Direction;
            _queryCone(_owner.ActorId, origin, direction, _halfAngleDegrees, _range, _scratch);
            for (var i = 0; i < _scratch.Count; i++)
            {
                var target = _scratch[i];
                if (!_hitOnce.Add(target.Value))
                {
                    continue;
                }

                _battle.Commands.EnqueueApplyDamage(new ApplyDamageCommand
                {
                    Source = _owner.ActorId,
                    Target = target,
                    Damage = _damage,
                    AbilityId = _abilityId,
                    DamageType = _damageType
                });

                if (!string.IsNullOrEmpty(_hitEffectId))
                {
                    _battle.Commands.EnqueueApplyEffect(new ApplyEffectCommand
                    {
                        Source = _owner.ActorId,
                        Target = target,
                        EffectId = _hitEffectId
                    });
                }

                if (_knockback > FP.Zero)
                {
                    var dir = direction;
                    dir.y = FP.Zero;
                    if (dir.sqrMagnitude > FP.Zero)
                    {
                        dir.Normalize();
                    }

                    _battle.Commands.EnqueueApplyDisplace(new ApplyDisplaceCommand
                    {
                        Target = target,
                        Offset = dir * _knockback
                    });
                }
            }
        }

        void TryGrantComboWindow()
        {
            if (_comboGranted || string.IsNullOrEmpty(_comboEffectId) || _battle == null || _owner == null)
            {
                return;
            }

            _comboGranted = true;
            _battle.Commands.EnqueueApplyEffect(new ApplyEffectCommand
            {
                Source = _owner.ActorId,
                Target = _owner.ActorId,
                EffectId = _comboEffectId
            });
        }
    }
}
