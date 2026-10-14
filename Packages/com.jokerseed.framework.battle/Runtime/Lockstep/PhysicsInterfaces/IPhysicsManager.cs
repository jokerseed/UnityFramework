using Framework.FixedMath;
using UnityEngine;

namespace Framework.Lockstep
{
    /// <summary>
    /// Unity 侧物理世界管理接口（含 GameObject 映射）。纯模拟可不实现本接口，仅实现 <see cref="IPhysicsManagerBase"/>。
    /// </summary>
    public interface IPhysicsManager : IPhysicsManagerBase
    {
        /// <summary>重力。</summary>
        TSVector Gravity { get; set; }

        /// <summary>是否启用推测接触。</summary>
        bool SpeculativeContacts { get; set; }

        /// <summary>锁定时间步长。</summary>
        FP LockedTimeStep { get; set; }

        /// <summary>刚体对应的 Unity GameObject。</summary>
        /// <param name="rigidBody">刚体。</param>
        /// <returns>GameObject；无映射时可为 null。</returns>
        GameObject GetGameObject(IBody rigidBody);

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
