namespace Framework.Core
{
    public static class BattleConstants
    {
        public const string Health = "Health";
        public const string MaxHealth = "MaxHealth";
        public const string Attack = "Attack";
        public const string Defense = "Defense";

        public const string TagDead = "State.Dead";
        public const string TagStunned = "State.CrowdControl.Stunned";
        public const string TagImmuneDamage = "Immunity.Damage";

        public const float DefaultActorCollisionRadius = 0.5f;
        public const float SpatialCellSize = 2f;
    }
}
