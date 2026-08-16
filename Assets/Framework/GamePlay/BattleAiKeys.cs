namespace Framework.GamePlay
{
    /// <summary>战斗 AI 黑板权威键（字符串键，禁止依赖遍历顺序）。</summary>
    public static class BattleAiBlackboardKeys
    {
        /// <summary>追击目标 <see cref="Framework.Core.ActorId.Value"/>（uint）。</summary>
        public const string FocusTarget = "FocusTarget";

        /// <summary>技能 Id（object 袋字符串；可覆盖节点 StringParam）。</summary>
        public const string AbilityId = "AbilityId";

        /// <summary>近战距离（FP）；可覆盖节点 FloatParam。</summary>
        public const string MeleeRange = "MeleeRange";

        /// <summary>移动速度（FP）；可覆盖节点 FloatParam。</summary>
        public const string MoveSpeed = "MoveSpeed";
    }

    /// <summary>战斗 AI 自定义叶子 TypeId。</summary>
    public static class BattleAiTypeIds
    {
        /// <summary>存活且非硬控。</summary>
        public const string IsAlive = "Battle.IsAlive";

        /// <summary>与目标距离或围攻槽到位。</summary>
        public const string InRange = "Battle.InRange";

        /// <summary>停步。</summary>
        public const string Stop = "Battle.Stop";

        /// <summary>朝目标/槽位移动。</summary>
        public const string MoveTo = "Battle.MoveTo";

        /// <summary>尝试激活技能。</summary>
        public const string ActivateAbility = "Battle.ActivateAbility";
    }
}
