using System.Collections.Generic;
using Framework.Core;

namespace Framework.GAS.Targeting
{
    /// <summary>目标数据类型。</summary>
    public enum GameplayTargetDataType
    {
        /// <summary>无目标。</summary>
        None,

        /// <summary>单个 Actor。</summary>
        SingleActor,

        /// <summary>世界位置。</summary>
        Location,

        /// <summary>多个 Actor。</summary>
        ActorList
    }

    /// <summary>技能目标数据（对应 UE TargetData 简化版）。</summary>
    public readonly struct GameplayTargetData
    {
        /// <summary>目标数据类型。</summary>
        public GameplayTargetDataType Type { get; }

        /// <summary>主目标 Actor。</summary>
        public ActorId PrimaryActor { get; }

        /// <summary>目标位置。</summary>
        public UnityEngine.Vector3 Location { get; }

        /// <summary>多目标列表。</summary>
        public IReadOnlyList<ActorId> Actors { get; }

        /// <summary>单 Actor 目标。</summary>
        public static GameplayTargetData FromActor(ActorId actor) =>
            new GameplayTargetData(GameplayTargetDataType.SingleActor, actor, default, null);

        /// <summary>位置目标。</summary>
        public static GameplayTargetData FromLocation(UnityEngine.Vector3 location) =>
            new GameplayTargetData(GameplayTargetDataType.Location, ActorId.Invalid, location, null);

        /// <summary>多 Actor 目标。</summary>
        public static GameplayTargetData FromActors(IReadOnlyList<ActorId> actors) =>
            new GameplayTargetData(GameplayTargetDataType.ActorList, ActorId.Invalid, default, actors);

        GameplayTargetData(
            GameplayTargetDataType type,
            ActorId primaryActor,
            UnityEngine.Vector3 location,
            IReadOnlyList<ActorId> actors)
        {
            Type = type;
            PrimaryActor = primaryActor;
            Location = location;
            Actors = actors;
        }
    }

    /// <summary>目标筛选条件。</summary>
    public readonly struct TargetDataFilter
    {
        /// <summary>源 Actor（排除自身）。</summary>
        public ActorId Source { get; }

        /// <summary>源队伍；&lt; 0 表示不筛选。</summary>
        public int SourceTeamId { get; }

        /// <summary>必须持有的 Tag（任一）。</summary>
        public IReadOnlyList<string> RequiredTags { get; }

        /// <summary>最大距离；&lt;= 0 表示不限。</summary>
        public float MaxDistance { get; }

        /// <summary>是否只选敌对单位。</summary>
        public bool EnemiesOnly { get; }

        /// <summary>构造筛选器。</summary>
        public TargetDataFilter(
            ActorId source,
            int sourceTeamId,
            bool enemiesOnly = true,
            float maxDistance = 0f,
            IReadOnlyList<string> requiredTags = null)
        {
            Source = source;
            SourceTeamId = sourceTeamId;
            EnemiesOnly = enemiesOnly;
            MaxDistance = maxDistance;
            RequiredTags = requiredTags;
        }
    }
}
