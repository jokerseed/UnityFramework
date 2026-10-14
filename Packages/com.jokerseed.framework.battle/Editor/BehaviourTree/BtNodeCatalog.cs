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

    /// <summary>编辑器可用节点清单。</summary>
    internal static class BtNodeCatalog
    {
        static readonly Color CompositeBlue = new Color(0.2f, 0.55f, 0.95f);
        static readonly Color CompositeGreen = new Color(0.2f, 0.75f, 0.45f);
        static readonly Color CompositeTeal = new Color(0.15f, 0.65f, 0.75f);
        static readonly Color DecoratorOrange = new Color(0.85f, 0.55f, 0.15f);
        static readonly Color LeafRed = new Color(0.75f, 0.35f, 0.35f);
        static readonly Color LeafPurple = new Color(0.55f, 0.45f, 0.85f);
        static readonly Color Custom = new Color(0.9f, 0.4f, 0.2f);

        public static readonly IReadOnlyList<BtNodeCatalogEntry> Entries = new List<BtNodeCatalogEntry>
        {
            new BtNodeCatalogEntry(BtNodeKind.Sequence, "Sequence", CompositeBlue, true, false),
            new BtNodeCatalogEntry(BtNodeKind.Selector, "Selector", CompositeGreen, true, false),
            new BtNodeCatalogEntry(BtNodeKind.ActiveSelector, "Active Selector", CompositeGreen, true, false),
            new BtNodeCatalogEntry(BtNodeKind.RandomSelector, "Random Selector", CompositeTeal, true, false),
            new BtNodeCatalogEntry(BtNodeKind.WeightedSelector, "Weighted Selector", CompositeTeal, true, false),
            new BtNodeCatalogEntry(BtNodeKind.Parallel, "Parallel", CompositeTeal, true, false),
            new BtNodeCatalogEntry(BtNodeKind.Inverter, "Inverter", DecoratorOrange, false, true),
            new BtNodeCatalogEntry(BtNodeKind.Repeater, "Repeater", DecoratorOrange, false, true),
            new BtNodeCatalogEntry(BtNodeKind.ForceSuccess, "Force Success", DecoratorOrange, false, true),
            new BtNodeCatalogEntry(BtNodeKind.ForceFailure, "Force Failure", DecoratorOrange, false, true),
            new BtNodeCatalogEntry(BtNodeKind.UntilSuccess, "Until Success", DecoratorOrange, false, true),
            new BtNodeCatalogEntry(BtNodeKind.Cooldown, "Cooldown", DecoratorOrange, false, true),
            new BtNodeCatalogEntry(BtNodeKind.Timeout, "Timeout", DecoratorOrange, false, true),
            new BtNodeCatalogEntry(BtNodeKind.TimeLimit, "Time Limit", DecoratorOrange, false, true),
            new BtNodeCatalogEntry(BtNodeKind.WaitFrames, "Wait Frames", LeafRed, false, false),
            new BtNodeCatalogEntry(BtNodeKind.WaitTime, "Wait Time", LeafRed, false, false),
            new BtNodeCatalogEntry(BtNodeKind.BlackboardBool, "Blackboard Bool", LeafPurple, false, false),
            new BtNodeCatalogEntry(BtNodeKind.CustomAction, "Custom Action", Custom, false, false),
            new BtNodeCatalogEntry(BtNodeKind.CustomCondition, "Custom Condition", Custom, false, false),
            new BtNodeCatalogEntry(BtNodeKind.Subtree, "Subtree", LeafPurple, false, false),
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
