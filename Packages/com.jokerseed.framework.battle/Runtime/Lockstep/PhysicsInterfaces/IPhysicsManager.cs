using Framework.FixedMath;

namespace Framework.Lockstep
{
    /// <summary>
    /// 物理世界管理接口：层过滤、碰撞开关和刚体生命周期通知。
    /// 不引用引擎类型；需要把刚体映射到场景对象时，由实现方自己维护映射。
    /// </summary>
    public interface IPhysicsManager : IPhysicsManagerBase
    {
        /// <summary>重力。</summary>
        TSVector Gravity { get; set; }

        /// <summary>是否启用推测接触。</summary>
        bool SpeculativeContacts { get; set; }

        /// <summary>锁定时间步长。</summary>
        FP LockedTimeStep { get; set; }

        /// <summary>
        /// 回滚恢复时，一个之前被禁用的刚体重新变为启用。
        /// 表现层实现可在这里重新激活对应的场景对象；纯模拟实现留空即可。
        /// </summary>
        /// <param name="rigidBody">重新启用的刚体。</param>
        void OnBodyRestoredEnabled(IBody rigidBody);

        /// <summary>刚体层。</summary>
        /// <param name="rigidBody">刚体。</param>
        /// <returns>层索引。</returns>
        int GetBodyLayer(IBody rigidBody);

        /// <summary>两刚体是否允许碰撞。</summary>
        /// <param name="rigidBody1">刚体 A。</param>
        /// <param name="rigidBody2">刚体 B。</param>
        /// <returns>可碰撞为 true。</returns>
        bool IsCollisionEnabled(IBody rigidBody1, IBody rigidBody2);

        /// <summary>添加碰撞体。</summary>
        /// <param name="iCollider">碰撞体。</param>
        void AddBody(ICollider iCollider);

        /// <summary>注册刚体移除回调。</summary>
        /// <param name="OnRemoveBody">回调。</param>
        void OnRemoveBody(System.Action<IBody> OnRemoveBody);

        /// <summary>清空物理世界。</summary>
        void Clear();
    }
}
