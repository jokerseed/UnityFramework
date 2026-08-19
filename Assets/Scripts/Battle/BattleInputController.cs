using System.Collections.Generic;
using Battle;
using Framework.GamePlay;
using Framework.GAS.Abilities;
using Framework.Core;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 战斗输入：采样锁存到意图帧，由 <see cref="LocalLockstepHost"/> 在逻辑步编码执行。
    /// </summary>
    public sealed class BattleInputController
    {
        const string FireballAbilityId = "Fireball";
        const float FireballCastHeight = 1.2f;
        const string DodgeAbilityId = "Dodge";
        const float HeroMoveSpeed = 3.5f;
        const float ComboBufferSeconds = 0.28f;
        static readonly string[] MeleeComboIds = { "Slash3", "Slash2", "Slash" };

        readonly BattleInputActions _actions = new BattleInputActions();
        BattleInputFrame _pending;
        float _meleeBuffer;
        bool _enabled;

        /// <summary>启用战斗 Action Map。</summary>
        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _actions.Battle.Enable();
            _enabled = true;
        }

        /// <summary>禁用战斗 Action Map。</summary>
        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _actions.Battle.Disable();
            _enabled = false;
        }

        /// <summary>释放 Input Action 资源。</summary>
        public void Dispose()
        {
            Disable();
            _actions.Dispose();
        }

        /// <summary>采样当前渲染帧输入，锁存到下一逻辑帧编码。须使用 unscaled 时间。</summary>
        /// <param name="unscaledDeltaTime">不受 timeScale 影响的帧间隔。</param>
        public void Sample(float unscaledDeltaTime)
        {
            var battle = _actions.Battle;
            if (battle.Melee.WasPressedThisFrame())
            {
                _meleeBuffer = ComboBufferSeconds;
            }

            var move2 = battle.Move.ReadValue<Vector2>();
            var moveDirection = new Vector3(move2.x, 0f, move2.y);
            var hasMoveInput = moveDirection.sqrMagnitude > 0.01f;
            if (hasMoveInput)
            {
                moveDirection.Normalize();
            }

            _pending.MoveDirection = moveDirection;
            _pending.HasMoveInput = hasMoveInput;
            _pending.TriggerMelee = _meleeBuffer > 0f;
            _pending.TriggerFireball = _pending.TriggerFireball || battle.Fireball.WasPressedThisFrame();
            _pending.TriggerDodge = _pending.TriggerDodge || battle.Dodge.WasPressedThisFrame();
            _pending.AimDirection = hasMoveInput ? moveDirection : Vector3.zero;
        }

        /// <summary>将锁存输入编码为当前逻辑帧的行为指令。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="heroId">英雄 ActorId。</param>
        /// <param name="dest">输出指令列表。</param>
        /// <param name="fixedDeltaTime">逻辑步长，用于衰减连招缓冲。</param>
        public void Encode(
            GamePlayFramework framework,
            ActorId heroId,
            List<BattleIntentCommand> dest,
            float fixedDeltaTime)
        {
            EncodePending(framework, heroId, dest);
            _meleeBuffer = Mathf.Max(0f, _meleeBuffer - fixedDeltaTime);
        }

        void EncodePending(
            GamePlayFramework framework,
            ActorId heroId,
            List<BattleIntentCommand> dest)
        {
            var velocity = _pending.HasMoveInput
                ? _pending.MoveDirection * HeroMoveSpeed
                : Vector3.zero;
            var face = _pending.HasMoveInput ? _pending.MoveDirection : Vector3.zero;
            dest.Add(BattleIntentCommand.Move(heroId, velocity, face, BattleIntentSource.Player));

            TryEncodeMelee(framework, heroId, dest);
            TryEncodeFireball(framework, heroId, dest);
            TryEncodeDodge(framework, heroId, dest);
            _pending.ConsumeOneShotCommands();
        }

        void TryEncodeMelee(
            GamePlayFramework framework,
            ActorId heroId,
            List<BattleIntentCommand> dest)
        {
            if (!_pending.TriggerMelee)
            {
                return;
            }

            if (!framework.Registry.TryGet(heroId, out var hero) ||
                !framework.TryGetActor(heroId, out var asc) ||
                asc.IsDead)
            {
                _meleeBuffer = 0f;
                _pending.TriggerMelee = false;
                return;
            }

            var forward = framework.Registry.GetForward(heroId);
            var context = new AbilityActivationContext(hero.Position, forward);
            for (var i = 0; i < MeleeComboIds.Length; i++)
            {
                if (!framework.CanActivateAbility(heroId, MeleeComboIds[i], context).Success)
                {
                    continue;
                }

                dest.Add(BattleIntentCommand.Cast(
                    heroId,
                    MeleeComboIds[i],
                    hero.Position,
                    forward,
                    ActorId.Invalid,
                    BattleIntentSource.Player));
                _meleeBuffer = 0f;
                _pending.TriggerMelee = false;
                return;
            }
        }

        void TryEncodeFireball(
            GamePlayFramework framework,
            ActorId heroId,
            List<BattleIntentCommand> dest)
        {
            if (!_pending.TriggerFireball || !framework.Registry.TryGet(heroId, out var hero))
            {
                return;
            }

            var targetId = framework.QueryNearestEnemy(heroId, hero.Position, 20f);
            var origin = hero.Position + Vector3.up * FireballCastHeight;
            var direction = _pending.AimDirection.sqrMagnitude > 0.0001f
                ? _pending.AimDirection
                : framework.Registry.GetForward(heroId);
            if (framework.Registry.TryGet(targetId, out var target))
            {
                var toTarget = target.Position - hero.Position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    direction = toTarget.normalized;
                }
            }

            dest.Add(BattleIntentCommand.Cast(
                heroId,
                FireballAbilityId,
                origin,
                direction,
                targetId,
                BattleIntentSource.Player));
            _pending.TriggerFireball = false;
        }

        void TryEncodeDodge(
            GamePlayFramework framework,
            ActorId heroId,
            List<BattleIntentCommand> dest)
        {
            if (!_pending.TriggerDodge || !framework.Registry.TryGet(heroId, out var hero))
            {
                return;
            }

            var forward = framework.Registry.GetForward(heroId);
            if (_pending.HasMoveInput)
            {
                forward = _pending.MoveDirection;
            }

            dest.Add(BattleIntentCommand.Cast(
                heroId,
                DodgeAbilityId,
                hero.Position,
                forward,
                ActorId.Invalid,
                BattleIntentSource.Player));
            _pending.TriggerDodge = false;
        }
    }
}
