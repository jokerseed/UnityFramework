using System;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 节点在图编辑器里的位置（仅 Editor 使用，不参与运行时）。
    /// 字段名与 Unity <c>Vector2</c> 的 JSON 形状一致，旧的 <c>.bt.json</c> 可以原样读取。
    /// </summary>
    [Serializable]
    [Preserve]
    public struct BtEditorPosition
    {
        /// <summary>横坐标。</summary>
        public float x;

        /// <summary>纵坐标。</summary>
        public float y;

        /// <summary>构造编辑器坐标。</summary>
        /// <param name="x">横坐标。</param>
        /// <param name="y">纵坐标。</param>
        public BtEditorPosition(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
    }

    /// <summary>
    /// 让 IL2CPP 托管代码裁剪保留只经反射序列化访问的配置类型。
    /// UnityLinker 按名字识别任意命名空间下的 <c>PreserveAttribute</c>，所以不需要引用 UnityEngine。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Constructor, Inherited = false)]
    internal sealed class PreserveAttribute : Attribute
    {
    }
}
