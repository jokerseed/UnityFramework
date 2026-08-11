using Framework.Core;
using UnityEngine;

namespace Framework.ECS.Components
{
    public struct TransformComponent : IComponent
    {
        public Vector3 Position;
        public Vector3 Forward;
    }

    public struct VelocityComponent : IComponent
    {
        public Vector3 Value;
    }

    public struct ProjectileComponent : IComponent
    {
        public ActorId Owner;
        public string AbilityId;
        public float Damage;
        public float Radius;
        public float RemainingLifetime;
        public int TeamId;
    }

    public struct ActorLinkComponent : IComponent
    {
        public ActorId ActorId;
    }

    /// <summary>ECS 侧仅存存活标记，生命数值权威在 GAS。</summary>
    public struct CombatStateComponent : IComponent
    {
        public bool IsAlive;
    }

    public struct TeamComponent : IComponent
    {
        public int TeamId;
    }

    public struct CollisionComponent : IComponent
    {
        public float Radius;
    }
}
