using System.Collections.Generic;
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
}
