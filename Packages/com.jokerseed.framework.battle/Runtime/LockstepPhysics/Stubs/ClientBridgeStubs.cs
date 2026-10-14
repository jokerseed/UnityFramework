using System;
using System.Collections.Generic;
using Framework.FixedMath;
using Framework.Lockstep;
using UnityEngine;

namespace Framework.Lockstep.Physics3D
{
    /// <summary>
    /// 占位：Client 侧 <c>PhysicsManager</c> 静态入口。完整 Unity 包装未迁入前，供 Jitter 层碰撞过滤调用。
    /// </summary>
    public static class PhysicsManager
    {
        /// <summary>当前物理管理器；默认 <see cref="NullPhysicsManager"/>。</summary>
        public static IPhysicsManager instance { get; set; } = new NullPhysicsManager();
    }

    /// <summary>
    /// 占位：Client 侧 <c>PhysicsWorldManager</c>。仅保留 gameObjectMap 供调试命名；完整场景桥未迁入。
    /// </summary>
    public sealed class PhysicsWorldManager
    {
        /// <summary>单例占位。</summary>
        public static PhysicsWorldManager instance { get; } = new PhysicsWorldManager();

        /// <summary>刚体到 GameObject 的可选映射。</summary>
        public Dictionary<IBody, GameObject> gameObjectMap = new Dictionary<IBody, GameObject>();
    }

    /// <summary>
    /// 占位：Client 帧日志系统。未迁入前为空操作。
    /// </summary>
    public sealed class FrameWriterSystem
    {
        /// <summary>单例占位。</summary>
        public static FrameWriterSystem Instance { get; } = new FrameWriterSystem();

        /// <summary>是否记录日志；默认关闭。</summary>
        public bool RecordLog { get; set; }

        /// <summary>追加日志（空操作）。</summary>
        /// <param name="message">日志内容。</param>
        public void AddLog(string message) { }
    }

    /// <summary>最小 <see cref="IPhysicsManager"/>，仅满足编译与默认层过滤。</summary>
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
        public GameObject GetGameObject(IBody rigidBody) => null;

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
