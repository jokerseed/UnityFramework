using System;
using Framework.FixedMath;

namespace Framework.Lockstep
{
    /// <summary>最小 <see cref="IPhysicsManager"/>：不做层过滤，所有刚体两两可碰撞；纯模拟与测试默认使用。</summary>
    public sealed class NullPhysicsManager : IPhysicsManager
    {
        /// <inheritdoc/>
        public TSVector Gravity { get; set; } = new TSVector(0, -10, 0);

        /// <inheritdoc/>
        public bool SpeculativeContacts { get; set; }

        /// <inheritdoc/>
        public FP LockedTimeStep { get; set; } = FP.EN2;

        /// <inheritdoc/>
        public void Init() { }

        /// <inheritdoc/>
        public void UpdateStep() { }

        /// <inheritdoc/>
        public IWorld GetWorld() => null;

        /// <inheritdoc/>
        public IWorldClone GetWorldClone() => null;

        /// <inheritdoc/>
        public void RemoveBody(IBody iBody) { }

        /// <inheritdoc/>
        public void OnBodyRestoredEnabled(IBody rigidBody) { }

        /// <inheritdoc/>
        public int GetBodyLayer(IBody rigidBody) => 0;

        /// <inheritdoc/>
        public bool IsCollisionEnabled(IBody rigidBody1, IBody rigidBody2) => true;

        /// <inheritdoc/>
        public void AddBody(ICollider iCollider) { }

        /// <inheritdoc/>
        public void OnRemoveBody(Action<IBody> OnRemoveBody) { }

        /// <inheritdoc/>
        public void Clear() { }
    }
}
