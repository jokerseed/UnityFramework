using Framework.Core;
using Framework.FixedMath;
using UnityEngine;

namespace Framework.ECS.Components
{
    /// <summary>空间变换组件，存储实体的世界坐标与朝向（定点，模拟权威）。</summary>
    public struct TransformComponent : IComponent
    {
        /// <summary>实体在世界空间的位置。</summary>
        public TSVector Position;

        /// <summary>实体的朝向向量（单位向量）。</summary>
        public TSVector Forward;

        /// <summary>转为 Unity 坐标，仅供表现层使用。</summary>
        /// <returns>浮点世界坐标。</returns>
        public Vector3 ToUnityPosition() => FPConversions.ToVector3(Position);

        /// <summary>转为 Unity 朝向，仅供表现层使用。</summary>
        /// <returns>浮点朝向。</returns>
        public Vector3 ToUnityForward() => FPConversions.ToVector3(Forward);
    }

    /// <summary>速度组件，驱动 <see cref="Systems.MovementSystem"/> 对实体进行位移。</summary>
    public struct VelocityComponent : IComponent
    {
        /// <summary>每秒位移向量（世界单位/秒），方向即为移动方向。</summary>
        public TSVector Value;
    }

    /// <summary>投射物组件，记录弹道的归属、伤害及生命周期信息。</summary>
    public struct ProjectileComponent : IComponent
    {
        /// <summary>发射该投射物的 Actor ID；碰撞时用于排除自伤。</summary>
        public ActorId Owner;

        /// <summary>触发该投射物的技能 ID；伤害计算时透传给 GAS。</summary>
        public string AbilityId;

        /// <summary>命中时造成的基础伤害值。</summary>
        public FP Damage;

        /// <summary>投射物的碰撞检测半径（世界单位）。</summary>
        public FP Radius;

        /// <summary>剩余存活时间（秒）；降至 0 时由 <see cref="Systems.ProjectileLifetimeSystem"/> 销毁实体。</summary>
        public FP RemainingLifetime;

        /// <summary>投射物所属队伍 ID；用于过滤友方碰撞。</summary>
        public int TeamId;

        /// <summary>剩余可穿透次数；0 表示下一次命中后销毁。</summary>
        public int PierceRemaining;

        /// <summary>命中后施加的效果 ID；空则不施加。</summary>
        public string HitEffectId;

        /// <summary>命中爆炸半径；≤0 不爆炸。</summary>
        public FP ExplodeRadius;

        /// <summary>伤害类型。</summary>
        public BattleDamageType DamageType;
    }

    /// <summary>击退冲量，由 <see cref="Systems.KnockbackSystem"/> 在移动之后叠加位移并衰减。</summary>
    public struct KnockbackComponent : IComponent
    {
        /// <summary>击退速度（米/秒）。</summary>
        public TSVector Velocity;

        /// <summary>剩余持续时间（秒）。</summary>
        public FP Remaining;
    }

    /// <summary>Actor 关联组件，将 ECS 实体与 GAS Actor ID 绑定，供系统查找对应的 <see cref="Framework.GamePlay.BattleActor"/>。</summary>
    public struct ActorLinkComponent : IComponent
    {
        /// <summary>关联的 GAS Actor ID。</summary>
        public ActorId ActorId;
    }

    /// <summary>ECS 侧仅存存活标记，生命数值权威在 GAS。</summary>
    public struct CombatStateComponent : IComponent
    {
        /// <summary>实体是否处于存活状态；置为 <c>false</c> 后该实体将被相关系统忽略。</summary>
        public bool IsAlive;
    }

    /// <summary>队伍组件，标识实体所属队伍，用于友伤过滤与敌人查询。</summary>
    public struct TeamComponent : IComponent
    {
        /// <summary>队伍编号；同 ID 视为友方，不同 ID 视为敌方。</summary>
        public int TeamId;
    }

    /// <summary>碰撞体积组件，定义实体的圆形碰撞半径。</summary>
    public struct CollisionComponent : IComponent
    {
        /// <summary>碰撞检测半径（世界单位）。</summary>
        public FP Radius;
    }
}
