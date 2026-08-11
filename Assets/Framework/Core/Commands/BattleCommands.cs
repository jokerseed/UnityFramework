using UnityEngine;

namespace Framework.Core.Commands
{
    public struct SpawnProjectileCommand
    {
        public ActorId Owner;
        public string AbilityId;
        public Vector3 Position;
        public Vector3 Direction;
        public float Speed;
        public float Radius;
        public float Lifetime;
        public float Damage;
        public int TeamId;
    }

    public struct ApplyDamageCommand
    {
        public ActorId Source;
        public ActorId Target;
        public float Damage;
        public string AbilityId;
    }
}
