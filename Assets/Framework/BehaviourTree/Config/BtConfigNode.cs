using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.BehaviourTree
{
    /// <summary>单个行为树节点的可序列化配置。</summary>
    [Serializable]
    public sealed class BtConfigNode
    {
        /// <summary>节点唯一 id（GUID 字符串）。</summary>
        public string Id = string.Empty;

        /// <summary>节点类型。</summary>
        public BtNodeKind Kind = BtNodeKind.Sequence;

        /// <summary>编辑器显示名。</summary>
        public string DisplayName = string.Empty;

        /// <summary>图编辑器中的位置（仅 Editor 使用，运行时忽略）。</summary>
        public Vector2 EditorPosition;

        /// <summary>子节点 id 列表（组合节点有序；装饰节点仅第一个有效）。</summary>
        public List<string> ChildIds = new List<string>();

        /// <summary>整型参数（WaitFrames 帧数、Repeater 次数等）。</summary>
        public int IntParam;

        /// <summary>浮点参数（WaitTime 秒数等）。</summary>
        public float FloatParam;

        /// <summary>字符串参数（黑板键、自定义节点类型 id 等）。</summary>
        public string StringParam = string.Empty;

        /// <summary>并行节点策略（仅 <see cref="BtNodeKind.Parallel"/> 有效）。</summary>
        public BtParallelPolicy ParallelPolicy = BtParallelPolicy.RequireAll;
    }
}
