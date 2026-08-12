using System.Collections.Generic;

namespace Framework.ECS
{
    /// <summary>ECS 组件的标记接口，所有组件结构体须实现此接口。</summary>
    public interface IComponent { }

    /// <summary>系统执行阶段，<see cref="World.Tick"/> 按 Simulate → Cleanup 顺序驱动。</summary>
    public enum EcsSystemPhase
    {
        /// <summary>模拟阶段：位移、空间索引、碰撞、生命周期。</summary>
        Simulate,

        /// <summary>清理阶段（预留）。</summary>
        Cleanup,
    }

    /// <summary>组件存储的非泛型接口，供 <see cref="World"/> 统一管理各类型存储。</summary>
    public interface IComponentStorage
    {
        /// <summary>移除指定实体的组件数据。</summary>
        /// <param name="entityId">要移除组件的实体 ID。</param>
        void Remove(uint entityId);

        /// <summary>清空该存储中的所有组件数据。</summary>
        void Clear();
    }

    /// <summary>ECS 系统接口，所有战斗系统须实现此接口并注册到 <see cref="World"/>。</summary>
    public interface ISystem
    {
        /// <summary>系统所属执行阶段。</summary>
        EcsSystemPhase Phase { get; }

        /// <summary>系统被添加到 World 时调用，用于初始化系统内部状态。</summary>
        /// <param name="world">拥有该系统的 ECS 世界实例。</param>
        void OnCreate(World world);

        /// <summary>World 销毁前调用，用于释放系统持有的资源。</summary>
        /// <param name="world">拥有该系统的 ECS 世界实例。</param>
        void OnDestroy(World world);

        /// <summary>每帧由 World.Tick 驱动，执行系统逻辑。</summary>
        /// <param name="world">拥有该系统的 ECS 世界实例。</param>
        /// <param name="deltaTime">距上一帧的时间间隔（秒）。</param>
        void Update(World world, float deltaTime);
    }
}
