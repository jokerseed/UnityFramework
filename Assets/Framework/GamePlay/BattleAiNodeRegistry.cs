using Framework.BehaviourTree;
using Framework.Core;
using Framework.FixedMath;
using Framework.GAS.Tags;
using Framework.Logging;
using UnityEngine;

namespace Framework.GamePlay
{
    /// <summary>
    /// 将战斗 AI 叶子注册到 <see cref="BtNodeFactory.Default"/>，供资产/JSON 编译。
    /// </summary>
    public static class BattleAiNodeRegistry
    {
        const float DefaultMoveSpeed = 2.2f;
        const float DefaultMeleeRange = 2f;
        const float ArriveSlotRange = 0.28f;
        const string SpeedParamKey = "speed";
        const string StopRangeParamKey = "stopRange";

        static bool _registered;

        /// <summary>确保自定义 TypeId 已注册（幂等）。</summary>
        public static void EnsureRegistered()
        {
            if (_registered)
            {
                return;
            }

            var factory = BtNodeFactory.Default;
            factory.Register(BattleAiTypeIds.IsAlive, _ => BtTreeBuilder.Condition(IsAlive));
            factory.Register(BattleAiTypeIds.InRange, CreateInRange);
            factory.Register(BattleAiTypeIds.Stop, _ => CreateStop());
            factory.Register(BattleAiTypeIds.MoveTo, CreateMoveTo);
            factory.Register(BattleAiTypeIds.ActivateAbility, CreateActivateAbility);
            _registered = true;
            GameLog.Debug(LogCategories.GamePlay, "Battle AI BT node types registered");
        }

        static BtNode CreateInRange(BtConfigNode config)
        {
            var range = ResolveFloat(config, null, DefaultMeleeRange, BattleAiBlackboardKeys.MeleeRange);
            return BtTreeBuilder.Condition(ctx =>
            {
                var owner = ctx.GetOwner<BattleAiOwner>();
                if (!TryGetFocusTarget(ctx, out var targetId) ||
                    owner.Framework == null ||
                    !owner.Framework.Registry.TryGet(owner.ActorId, out var self) ||
                    !owner.Framework.Registry.TryGet(targetId, out var target))
                {
                    return false;
                }

                var effectiveRange = ResolveFloat(config, ctx, range, BattleAiBlackboardKeys.MeleeRange);
                if (Vector3.Distance(self.Position, target.Position) <= effectiveRange)
                {
                    return true;
                }

                return owner.Framework.TryGetEngagePoint(owner.ActorId, out var slot) &&
                       HorizontalDistanceSqr(self.Position, slot) <= ArriveSlotRange * ArriveSlotRange;
            });
        }

        static BtNode CreateStop()
        {
            return BtTreeBuilder.Action(ctx =>
            {
                var owner = ctx.GetOwner<BattleAiOwner>();
                if (owner.Framework != null)
                {
                    BattleIntentApplier.Apply(
                        owner.Framework,
                        BattleIntentCommand.Move(owner.ActorId, Vector3.zero, Vector3.zero, BattleIntentSource.Ai));
                }

                return BtStatus.Success;
            });
        }

        static BtNode CreateMoveTo(BtConfigNode config)
        {
            var speedDefault = ReadFloatParam(config, SpeedParamKey, config.FloatParam > 0f ? config.FloatParam : DefaultMoveSpeed);
            var stopDefault = ReadFloatParam(config, StopRangeParamKey, DefaultMeleeRange * 0.85f);

            return BtTreeBuilder.Action(ctx =>
            {
                var owner = ctx.GetOwner<BattleAiOwner>();
                if (!TryGetFocusTarget(ctx, out var targetId) ||
                    owner.Framework == null ||
                    !owner.Framework.Registry.TryGet(owner.ActorId, out var self) ||
                    !owner.Framework.Registry.TryGet(targetId, out var target))
                {
                    return BtStatus.Failure;
                }

                var speed = speedDefault;
                if (ctx.Blackboard.TryGetFp(BattleAiBlackboardKeys.MoveSpeed, out var speedFp))
                {
                    speed = speedFp.AsFloat();
                }

                var stopRange = stopDefault;
                if (ctx.Blackboard.TryGetFp(BattleAiBlackboardKeys.MeleeRange, out var meleeFp))
                {
                    stopRange = meleeFp.AsFloat() * 0.85f;
                }

                var dest = target.Position;
                var arrive = stopRange;
                if (owner.Framework.TryGetEngagePoint(owner.ActorId, out var slot))
                {
                    dest = slot;
                    arrive = ArriveSlotRange;
                }

                var face = target.Position - self.Position;
                var toDest = dest - self.Position;
                toDest.y = 0f;
                var distance = toDest.magnitude;
                var velocity = distance <= arrive ? Vector3.zero : toDest / distance * speed;
                BattleIntentApplier.Apply(
                    owner.Framework,
                    BattleIntentCommand.Move(owner.ActorId, velocity, face, BattleIntentSource.Ai));
                return distance <= arrive ? BtStatus.Success : BtStatus.Running;
            });
        }

        static BtNode CreateActivateAbility(BtConfigNode config)
        {
            var abilityFromConfig = config.StringParam;

            return BtTreeBuilder.Action(ctx =>
            {
                var owner = ctx.GetOwner<BattleAiOwner>();
                if (!TryGetFocusTarget(ctx, out var targetId) ||
                    owner.Framework == null ||
                    !owner.Framework.Registry.TryGet(owner.ActorId, out var self) ||
                    !owner.Framework.Registry.TryGet(targetId, out var target))
                {
                    return BtStatus.Failure;
                }

                var abilityId = abilityFromConfig;
                if (ctx.Blackboard.TryGet(BattleAiBlackboardKeys.AbilityId, out string bbAbility) &&
                    !string.IsNullOrEmpty(bbAbility))
                {
                    abilityId = bbAbility;
                }

                if (string.IsNullOrEmpty(abilityId))
                {
                    return BtStatus.Failure;
                }

                var toTarget = target.Position - self.Position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.0001f)
                {
                    toTarget = Vector3.forward;
                }

                var success = BattleIntentApplier.Apply(
                    owner.Framework,
                    BattleIntentCommand.Cast(
                        owner.ActorId,
                        abilityId,
                        self.Position,
                        toTarget,
                        targetId,
                        BattleIntentSource.Ai));
                return success ? BtStatus.Success : BtStatus.Failure;
            });
        }

        static bool IsAlive(BtContext ctx)
        {
            var owner = ctx.GetOwner<BattleAiOwner>();
            return owner.Framework != null &&
                   owner.Framework.TryGetActor(owner.ActorId, out var asc) &&
                   !asc.IsDead &&
                   !asc.Tags.HasTag(new GameplayTag(BattleConstants.TagStunned)) &&
                   !asc.Tags.HasTag(new GameplayTag(BattleConstants.TagKnockedDown));
        }

        static bool TryGetFocusTarget(BtContext ctx, out ActorId targetId)
        {
            targetId = ActorId.Invalid;
            if (ctx.Blackboard.TryGetId(BattleAiBlackboardKeys.FocusTarget, out var value) && value != 0)
            {
                targetId = new ActorId(value);
                return true;
            }

            return false;
        }

        static float ResolveFloat(
            BtConfigNode config,
            BtContext ctx,
            float fallback,
            string blackboardKey)
        {
            if (ctx != null &&
                ctx.Blackboard.TryGetFp(blackboardKey, out var fp))
            {
                return fp.AsFloat();
            }

            if (config != null && config.FloatParam > 0f)
            {
                return config.FloatParam;
            }

            return fallback;
        }

        static float ReadFloatParam(BtConfigNode config, string key, float fallback)
        {
            if (config != null &&
                config.TryGetParam(key, out var kv) &&
                kv.Type == BtParamType.Float)
            {
                return kv.FloatValue;
            }

            return fallback;
        }

        static float HorizontalDistanceSqr(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
