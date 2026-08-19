using System.Collections.Generic;
using Framework.Core;
using Framework.FixedMath;
using Framework.GAS.Abilities;
using Framework.GAS.Tags;
using Framework.Logging;

namespace Framework.GamePlay
{
    /// <summary>
    /// 行为指令执行器：玩家与 AI 共用同一套 Move / Cast 规则。
    /// </summary>
    public static class BattleIntentApplier
    {
        static readonly GameplayTag StunnedTag = new GameplayTag(BattleConstants.TagStunned);
        static readonly GameplayTag KnockedDownTag = new GameplayTag(BattleConstants.TagKnockedDown);
        static readonly GameplayTag DodgingTag = new GameplayTag(BattleConstants.TagDodging);

        /// <summary>按顺序执行一组行为指令。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="commands">指令列表。</param>
        public static void ApplyAll(GamePlayFramework framework, IReadOnlyList<BattleIntentCommand> commands)
        {
            if (framework == null || commands == null)
            {
                return;
            }

            for (var i = 0; i < commands.Count; i++)
            {
                Apply(framework, commands[i]);
            }
        }

        /// <summary>执行单条行为指令。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="command">指令。</param>
        /// <returns>施法指令是否激活成功；移动指令在可执行时返回 true。</returns>
        public static bool Apply(GamePlayFramework framework, in BattleIntentCommand command)
        {
            if (framework == null)
            {
                return false;
            }

            switch (command.Kind)
            {
                case BattleIntentKind.Move:
                    return ApplyMove(framework, command);
                case BattleIntentKind.Cast:
                    return ApplyCast(framework, command);
                default:
                    return false;
            }
        }

        static bool ApplyMove(GamePlayFramework framework, in BattleIntentCommand command)
        {
            if (!framework.TryGetActor(command.ActorId, out var asc) || asc.IsDead)
            {
                return false;
            }

            if (asc.Tags.HasTag(StunnedTag) || asc.Tags.HasTag(KnockedDownTag))
            {
                framework.SetActorVelocity(command.ActorId, TSVector.zero);
                return false;
            }

            if (asc.Tags.HasTag(DodgingTag))
            {
                return false;
            }

            TrySetFace(framework, command.ActorId, command.FaceDirection, command.MoveVelocity);
            framework.SetActorVelocity(command.ActorId, command.MoveVelocity);
            return true;
        }

        static bool ApplyCast(GamePlayFramework framework, in BattleIntentCommand command)
        {
            if (string.IsNullOrEmpty(command.AbilityId) ||
                !framework.Registry.TryGet(command.ActorId, out _))
            {
                return false;
            }

            var direction = command.Direction;
            direction.y = FP.Zero;
            if (direction.sqrMagnitude > FP.EN4)
            {
                direction.Normalize();
                framework.Registry.SetForward(command.ActorId, direction);
            }
            else
            {
                direction = framework.Registry.GetSimForward(command.ActorId);
            }

            var origin = command.Origin;
            if (origin.sqrMagnitude < FP.EN4 &&
                framework.Registry.TryGetSimPosition(command.ActorId, out var sim))
            {
                origin = sim;
            }

            var context = new AbilityActivationContext(origin, direction, command.TargetId);
            var success = framework.TryActivateAbility(command.ActorId, command.AbilityId, context).Success;
            if (success && command.Source == BattleIntentSource.Player)
            {
                GameLog.Info(LogCategories.GamePlay, $"Hero cast {LogStyle.Name(command.AbilityId)}");
            }

            return success;
        }

        static void TrySetFace(GamePlayFramework framework, ActorId actorId, TSVector face, TSVector velocity)
        {
            var dir = face.sqrMagnitude > FP.EN4 ? face : velocity;
            dir.y = FP.Zero;
            if (dir.sqrMagnitude < FP.EN4)
            {
                return;
            }

            framework.Registry.SetForward(actorId, dir.normalized);
        }
    }
}
