using Framework.Core.Snapshot;

namespace Framework.ECS
{
    /// <summary>
    /// 业务组件的快照扩展。内置战斗组件由 <see cref="World"/> 自己写；
    /// 新增组件类型实现本接口并 <see cref="World.AddSnapshotExtension"/>。
    /// <see cref="TypeId"/> 必须大于等于 100，避免和内置块冲突。
    /// </summary>
    public interface IEcsSnapshotExtension
    {
        /// <summary>稳定的块类型号，须 &gt;= 100。</summary>
        int TypeId { get; }

        /// <summary>追加该扩展的状态。调用时类型号已经由 World 写过。</summary>
        /// <param name="world">ECS 世界。</param>
        /// <param name="writer">写入器。</param>
        void Save(World world, SnapshotWriter writer);

        /// <summary>读回该扩展的状态。</summary>
        /// <param name="world">ECS 世界；实体已经按快照重建。</param>
        /// <param name="reader">读取器。</param>
        void Restore(World world, SnapshotReader reader);
    }
}
