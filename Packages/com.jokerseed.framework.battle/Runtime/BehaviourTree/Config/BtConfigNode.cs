using System;
using System.Collections.Generic;
using Framework.FixedMath;
using UnityEngine;

namespace Framework.BehaviourTree
{
    /// <summary>可序列化键值参数。</summary>
    [Serializable]
    public sealed class BtParamKv
    {
        /// <summary>参数键。</summary>
        public string Key = string.Empty;

        /// <summary>值类型。</summary>
        public BtParamType Type = BtParamType.Int;

        /// <summary>整数。</summary>
        public int IntValue;

        /// <summary>浮点（仅编辑展示）。</summary>
        public float FloatValue;

        /// <summary>布尔。</summary>
        public bool BoolValue;

        /// <summary>字符串。</summary>
        public string StringValue = string.Empty;

        /// <summary>定点原始值。</summary>
        public long FpRaw;
    }

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

        /// <summary>图编辑器中的位置（仅 Editor 使用）。</summary>
        public Vector2 EditorPosition;

        /// <summary>子节点 id 列表。</summary>
        public List<string> ChildIds = new List<string>();

        /// <summary>整型参数。</summary>
        public int IntParam;

        /// <summary>浮点参数（展示秒数等）。</summary>
        public float FloatParam;

        /// <summary>字符串参数。</summary>
        public string StringParam = string.Empty;

        /// <summary>并行节点策略。</summary>
        public BtParallelPolicy ParallelPolicy = BtParallelPolicy.RequireAll;

        /// <summary>条件打断类型。</summary>
        public BtAbortType AbortType = BtAbortType.None;

        /// <summary>编辑器断点。</summary>
        public bool Breakpoint;

        /// <summary>自定义/工厂类型 id；空则回退 StringParam。</summary>
        public string TypeId = string.Empty;

        /// <summary>子树解析 id；空则回退 StringParam。</summary>
        public string SubtreeId = string.Empty;

        /// <summary>是否写入了权威定点原始值。</summary>
        public bool HasFpRaw;

        /// <summary>权威定点原始值。</summary>
        public long FpRawParam;

        /// <summary>Repeater 失败是否重试。</summary>
        public bool RepeatOnFailure;

        /// <summary>并行：失败是否立刻结束。</summary>
        public bool FailFast = true;

        /// <summary>并行：成功是否立刻结束。</summary>
        public bool SucceedFast = true;

        /// <summary>WeightedSelector 权重。</summary>
        public List<int> Weights = new List<int>();

        /// <summary>扩展键值参数。</summary>
        public List<BtParamKv> Params = new List<BtParamKv>();

        /// <summary>解析自定义类型 id。</summary>
        /// <returns>TypeId 或 StringParam。</returns>
        public string ResolveTypeId() =>
            !string.IsNullOrEmpty(TypeId) ? TypeId : StringParam;

        /// <summary>解析子树 id。</summary>
        /// <returns>SubtreeId 或 StringParam。</returns>
        public string ResolveSubtreeId() =>
            !string.IsNullOrEmpty(SubtreeId) ? SubtreeId : StringParam;

        /// <summary>解析权威时长：优先 FpRaw，否则 FloatParam。</summary>
        /// <returns>定点数时长。</returns>
        public FP ResolveDuration()
        {
            if (HasFpRaw)
            {
                return FP.FromRaw(FpRawParam);
            }

            if (TryGetParam(BtParamKeys.Duration, out var kv) && kv.Type == BtParamType.FpRaw)
            {
                return FP.FromRaw(kv.FpRaw);
            }

            return FP.FromFloat(FloatParam);
        }

        /// <summary>写入权威时长（同时更新展示用 FloatParam）。</summary>
        /// <param name="seconds">秒（展示）。</param>
        public void SetDurationSeconds(float seconds)
        {
            FloatParam = seconds;
            HasFpRaw = true;
            FpRawParam = FP.FromFloat(seconds).RawValue;
        }

        /// <summary>按键取扩展参数。</summary>
        /// <param name="key">键。</param>
        /// <param name="entry">条目。</param>
        /// <returns>找到则为 true。</returns>
        public bool TryGetParam(string key, out BtParamKv entry)
        {
            entry = null;
            if (Params == null || string.IsNullOrEmpty(key))
            {
                return false;
            }

            for (var i = 0; i < Params.Count; i++)
            {
                if (Params[i] != null && Params[i].Key == key)
                {
                    entry = Params[i];
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>常用扩展参数键。</summary>
    public static class BtParamKeys
    {
        /// <summary>时长。</summary>
        public const string Duration = "duration";
    }
}
