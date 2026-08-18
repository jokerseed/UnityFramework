using Framework.Core;
using UnityEngine;

namespace Framework.GamePlay
{
    /// <summary>
    /// 玩家 / AI 共用的行为指令。与 <see cref="Framework.Core.Commands.BattleCommandBuffer"/> 的模拟结算命令分离。
    /// </summary>
    public struct BattleIntentCommand
    {
        /// <summary>指令类型。</summary>
        public BattleIntentKind Kind { get; set; }

        /// <summary>指令来源。</summary>
        public BattleIntentSource Source { get; set; }

        /// <summary>发出指令的 Actor。</summary>
        public ActorId ActorId { get; set; }

        /// <summary><see cref="BattleIntentKind.Move"/> 的世界速度；零向量表示停步。</summary>
        public Vector3 MoveVelocity { get; set; }

        /// <summary>期望朝向；平方长度过小时沿用当前朝向或移动方向。</summary>
        public Vector3 FaceDirection { get; set; }

        /// <summary><see cref="BattleIntentKind.Cast"/> 的技能 ID。</summary>
        public string AbilityId { get; set; }

        /// <summary>施法原点。</summary>
        public Vector3 Origin { get; set; }

        /// <summary>施法方向。</summary>
        public Vector3 Direction { get; set; }

        /// <summary>主目标；无效时表示无锁定目标。</summary>
        public ActorId TargetId { get; set; }

        /// <summary>创建一条移动 / 停步指令。</summary>
        /// <param name="actorId">发出者。</param>
        /// <param name="velocity">世界速度。</param>
        /// <param name="faceDirection">朝向；为零时用速度方向。</param>
        /// <param name="source">来源。</param>
        /// <returns>移动指令。</returns>
        public static BattleIntentCommand Move(
            ActorId actorId,
            Vector3 velocity,
            Vector3 faceDirection,
            BattleIntentSource source)
        {
            return new BattleIntentCommand
            {
                Kind = BattleIntentKind.Move,
                Source = source,
                ActorId = actorId,
                MoveVelocity = velocity,
                FaceDirection = faceDirection,
            };
        }

        /// <summary>创建一条施法指令。</summary>
        /// <param name="actorId">发出者。</param>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="origin">施法原点。</param>
        /// <param name="direction">施法方向。</param>
        /// <param name="targetId">主目标。</param>
        /// <param name="source">来源。</param>
        /// <returns>施法指令。</returns>
        public static BattleIntentCommand Cast(
            ActorId actorId,
            string abilityId,
            Vector3 origin,
            Vector3 direction,
            ActorId targetId,
            BattleIntentSource source)
        {
            return new BattleIntentCommand
            {
                Kind = BattleIntentKind.Cast,
                Source = source,
                ActorId = actorId,
                AbilityId = abilityId,
                Origin = origin,
                Direction = direction,
                TargetId = targetId,
                FaceDirection = direction,
            };
        }
    }
}
