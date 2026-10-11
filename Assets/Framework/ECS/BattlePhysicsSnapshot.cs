using Framework.Lockstep.Physics2D;

namespace Framework.ECS
{
    /// <summary>
    /// <see cref="BattlePhysicsWorld"/> 的完整内存快照。
    /// 内含 Farseer 接触、宽相、岛和 TOI，不是可移植的字节流。
    /// 同一实例可以再次 <see cref="BattlePhysicsWorld.SaveFull"/>，会覆盖上一份。
    /// </summary>
    public sealed class BattlePhysicsSnapshot
    {
        internal readonly WorldClone2D Clone = new WorldClone2D();
    }
}
