using System;
using System.Collections.Generic;

namespace Framework.BehaviourTree
{
    /// <summary>完整行为树配置（可 JSON 序列化）。</summary>
    [Serializable]
    public sealed class BtTreeDefinition
    {
        /// <summary>配置格式版本。</summary>
        public const int CurrentVersion = 2;

        /// <summary>格式版本号。</summary>
        public int Version = CurrentVersion;

        /// <summary>行为树名称。</summary>
        public string TreeName = "BehaviourTree";

        /// <summary>根节点 id。</summary>
        public string RootNodeId = string.Empty;

        /// <summary>全部节点。</summary>
        public List<BtConfigNode> Nodes = new List<BtConfigNode>();
    }
}
