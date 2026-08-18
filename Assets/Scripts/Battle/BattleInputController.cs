using Battle;
using Framework.GamePlay;
using Framework.GAS.Abilities;
using Framework.GAS.Tags;
using Framework.Core;
using Framework.Logging;
using UnityEngine;
using UnityEngine.InputSystem;

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
        const float ComboBufferSeconds = 0.28f;
        static readonly string[] MeleeComboIds = { "Slash3", "Slash2", "Slash" };
        static readonly GameplayTag StunnedTag = new GameplayTag(BattleConstants.TagStunned);
        static readonly GameplayTag KnockedDownTag = new GameplayTag(BattleConstants.TagKnockedDown);
        static readonly GameplayTag DodgingTag = new GameplayTag(BattleConstants.TagDodging);

        readonly BattleInputActions _actions = new BattleInputActions();
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

        /// <summary>将采样输入应用到一个逻辑步。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="heroId">英雄 ActorId。</param>
        /// <param name="inputFrame">当前待消费的输入快照。</param>
        public void Apply(GamePlayFramework framework, ActorId heroId, ref BattleInputFrame inputFrame)
        {
            TryMoveHero(framework, heroId, inputFrame);
            TryHeroMelee(framework, heroId, ref inputFrame);
            TryCastHeroFireball(framework, heroId, ref inputFrame);
            TryHeroDodge(framework, heroId, ref inputFrame);
            inputFrame.ConsumeOneShotCommands();
        }

        static void TryMoveHero(GamePlayFramework framework, ActorId heroId, in BattleInputFrame inputFrame)
        {
            if (!framework.TryGetActor(heroId, out var asc) || asc.IsDead)
            {
                return;
            }

            if (asc.Tags.HasTag(StunnedTag) || asc.Tags.HasTag(KnockedDownTag))
            {
                framework.SetActorVelocity(heroId, Vector3.zero);
                return;
            }

            if (asc.Tags.HasTag(DodgingTag))
            {
                return;
            }

            if (!inputFrame.HasMoveInput)
            {
                framework.SetActorVelocity(heroId, Vector3.zero);
                return;
            }

            framework.Registry.SetForward(heroId, inputFrame.MoveDirection);
            framework.SetActorVelocity(heroId, inputFrame.MoveDirection * 3.5f);
        }

        void TryHeroMelee(GamePlayFramework framework, ActorId heroId, ref BattleInputFrame inputFrame)
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
                if (!framework.TryActivateAbility(heroId, MeleeComboIds[i], context).Success)
                {
                    continue;
                }

                _meleeBuffer = 0f;
                inputFrame.TriggerMelee = false;
                GameLog.Info(LogCategories.GamePlay, $"Hero melee {LogStyle.Name(MeleeComboIds[i])}");
                return;
            }
        }

        static void TryCastHeroFireball(GamePlayFramework framework, ActorId heroId, ref BattleInputFrame inputFrame)
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
                    framework.Registry.SetForward(heroId, direction);
                }
            }

            var result = framework.TryActivateAbility(heroId, FireballAbilityId, new AbilityActivationContext(origin, direction, targetId));
            inputFrame.TriggerFireball = false;
            if (result.Success)
            {
                GameLog.Info(LogCategories.GamePlay, $"Hero cast {LogStyle.Name(FireballAbilityId)}");
            }
        }

        static void TryHeroDodge(GamePlayFramework framework, ActorId heroId, ref BattleInputFrame inputFrame)
        {
            if (!inputFrame.TriggerDodge || !framework.Registry.TryGet(heroId, out var hero))
            {
                return;
            }

            var forward = framework.Registry.GetForward(heroId);
            if (inputFrame.HasMoveInput)
            {
                forward = inputFrame.MoveDirection;
                framework.Registry.SetForward(heroId, forward);
            }

            var result = framework.TryActivateAbility(heroId, DodgeAbilityId, new AbilityActivationContext(hero.Position, forward));
            inputFrame.TriggerDodge = false;
            if (result.Success)
            {
                GameLog.Info(LogCategories.GamePlay, $"Hero {LogStyle.Name(DodgeAbilityId)}");
            }
        }
    }
}
