using System.Collections.Generic;
using Battle;
using Framework.GamePlay;
using Framework.GAS.Abilities;
using Framework.Core;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 战斗输入：通过 <see cref="BattleInputActions"/> 采样并驱动 <see cref="GamePlayFramework"/>。
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
        readonly List<BattleIntentCommand> _intents = new List<BattleIntentCommand>(8);
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

        /// <summary>采样当前渲染帧输入，生成可供逻辑帧消费的输入快照。</summary>
        /// <param name="deltaTime">帧间隔。</param>
        /// <returns>本帧输入快照。</returns>
        public BattleInputFrame Sample(float deltaTime)
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

            _meleeBuffer = Mathf.Max(0f, _meleeBuffer - deltaTime);
            return new BattleInputFrame
            {
                MoveDirection = moveDirection,
                HasMoveInput = hasMoveInput,
                TriggerMelee = _meleeBuffer > 0f,
                TriggerFireball = battle.Fireball.WasPressedThisFrame(),
                TriggerDodge = battle.Dodge.WasPressedThisFrame(),
                AimDirection = hasMoveInput ? moveDirection : Vector3.zero,
            };
        }

        /// <summary>将本帧输入编码为行为指令并执行。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="heroId">英雄 ActorId。</param>
        /// <param name="inputFrame">当前待消费的输入快照。</param>
        public void Apply(GamePlayFramework framework, ActorId heroId, ref BattleInputFrame inputFrame)
        {
            _intents.Clear();
            Encode(framework, heroId, ref inputFrame, _intents);
            BattleIntentApplier.ApplyAll(framework, _intents);
        }

        void Encode(
            GamePlayFramework framework,
            ActorId heroId,
            ref BattleInputFrame inputFrame,
            List<BattleIntentCommand> dest)
        {
            var velocity = inputFrame.HasMoveInput
                ? inputFrame.MoveDirection * HeroMoveSpeed
                : Vector3.zero;
            var face = inputFrame.HasMoveInput ? inputFrame.MoveDirection : Vector3.zero;
            dest.Add(BattleIntentCommand.Move(heroId, velocity, face, BattleIntentSource.Player));

            TryEncodeMelee(framework, heroId, ref inputFrame, dest);
            TryEncodeFireball(framework, heroId, ref inputFrame, dest);
            TryEncodeDodge(framework, heroId, ref inputFrame, dest);
            inputFrame.ConsumeOneShotCommands();
        }

        void TryEncodeMelee(
            GamePlayFramework framework,
            ActorId heroId,
            ref BattleInputFrame inputFrame,
            List<BattleIntentCommand> dest)
        {
            if (!inputFrame.TriggerMelee)
            {
                return;
            }

            if (!framework.Registry.TryGet(heroId, out var hero) ||
                !framework.TryGetActor(heroId, out var asc) ||
                asc.IsDead)
            {
                _meleeBuffer = 0f;
                inputFrame.TriggerMelee = false;
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
                inputFrame.TriggerMelee = false;
                return;
            }
        }

        static void TryEncodeFireball(
            GamePlayFramework framework,
            ActorId heroId,
            ref BattleInputFrame inputFrame,
            List<BattleIntentCommand> dest)
        {
            if (!inputFrame.TriggerFireball || !framework.Registry.TryGet(heroId, out var hero))
            {
                return;
            }

            var targetId = framework.QueryNearestEnemy(heroId, hero.Position, 20f);
            var origin = hero.Position + Vector3.up * FireballCastHeight;
            var direction = inputFrame.AimDirection.sqrMagnitude > 0.0001f
                ? inputFrame.AimDirection
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
            inputFrame.TriggerFireball = false;
        }

        static void TryEncodeDodge(
            GamePlayFramework framework,
            ActorId heroId,
            ref BattleInputFrame inputFrame,
            List<BattleIntentCommand> dest)
        {
            if (!inputFrame.TriggerDodge || !framework.Registry.TryGet(heroId, out var hero))
            {
                return;
            }

            var forward = framework.Registry.GetForward(heroId);
            if (inputFrame.HasMoveInput)
            {
                forward = inputFrame.MoveDirection;
            }

            dest.Add(BattleIntentCommand.Cast(
                heroId,
                DodgeAbilityId,
                hero.Position,
                forward,
                ActorId.Invalid,
                BattleIntentSource.Player));
            inputFrame.TriggerDodge = false;
        }
    }
}
