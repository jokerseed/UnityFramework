using System.Collections.Generic;
using UnityEngine;

namespace Framework.BehaviourTree.Editor
{
    /// <summary>编辑器节点类型元数据。</summary>
    internal readonly struct BtNodeCatalogEntry
    {
        public readonly BtNodeKind Kind;
        public readonly string Label;
        public readonly Color Color;
        public readonly bool IsComposite;
        public readonly bool IsDecorator;

        public BtNodeCatalogEntry(BtNodeKind kind, string label, Color color, bool isComposite, bool isDecorator)
        {
            Kind = kind;
            Label = label;
            Color = color;
            IsComposite = isComposite;
            IsDecorator = isDecorator;
        }
    }

    /// <summary>编辑器可用节点清单（参考 Behavior Designer 分类）。</summary>
    internal static class BtNodeCatalog
    {
        public static readonly IReadOnlyList<BtNodeCatalogEntry> Entries = new List<BtNodeCatalogEntry>
        {
            new BtNodeCatalogEntry(BtNodeKind.Sequence, "Sequence", new Color(0.2f, 0.55f, 0.95f), true, false),
            new BtNodeCatalogEntry(BtNodeKind.Selector, "Selector", new Color(0.2f, 0.75f, 0.45f), true, false),
            new BtNodeCatalogEntry(BtNodeKind.Parallel, "Parallel", new Color(0.15f, 0.65f, 0.75f), true, false),
            new BtNodeCatalogEntry(BtNodeKind.Inverter, "Inverter", new Color(0.85f, 0.55f, 0.15f), false, true),
            new BtNodeCatalogEntry(BtNodeKind.Repeater, "Repeater", new Color(0.85f, 0.55f, 0.15f), false, true),
            new BtNodeCatalogEntry(BtNodeKind.ForceSuccess, "Force Success", new Color(0.85f, 0.55f, 0.15f), false, true),
            new BtNodeCatalogEntry(BtNodeKind.WaitFrames, "Wait Frames", new Color(0.75f, 0.35f, 0.35f), false, false),
            new BtNodeCatalogEntry(BtNodeKind.WaitTime, "Wait Time", new Color(0.75f, 0.35f, 0.35f), false, false),
            new BtNodeCatalogEntry(BtNodeKind.BlackboardBool, "Blackboard Bool", new Color(0.55f, 0.45f, 0.85f), false, false),
            new BtNodeCatalogEntry(BtNodeKind.CustomAction, "Custom Action", new Color(0.9f, 0.4f, 0.2f), false, false),
            new BtNodeCatalogEntry(BtNodeKind.CustomCondition, "Custom Condition", new Color(0.9f, 0.4f, 0.2f), false, false),
        };

        public static bool TryGet(BtNodeKind kind, out BtNodeCatalogEntry entry)
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Kind == kind)
                {
                    entry = Entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
