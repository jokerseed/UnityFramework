using System;
using Framework.FixedMath;

namespace Framework.BehaviourTree
{
    /// <summary>内置节点类型的配置 → 运行时编译。</summary>
    public sealed class BtBuiltinNodeRegistry : IBtNodeRegistry
    {
        /// <inheritdoc />
        public bool TryCreate(BtConfigNode config, out BtNode node)
        {
            node = null;
            if (config == null)
            {
                return false;
            }

            switch (config.Kind)
            {
                case BtNodeKind.Sequence:
                    node = new BtSequence();
                    return true;
                case BtNodeKind.Selector:
                    node = new BtSelector();
                    return true;
                case BtNodeKind.Parallel:
                    node = new BtParallel(config.ParallelPolicy);
                    return true;
                case BtNodeKind.WaitFrames:
                    node = new BtWaitFrames(config.IntParam);
                    return true;
                case BtNodeKind.WaitTime:
                    node = new BtWaitTime(FP.FromFloat(config.FloatParam));
                    return true;
                case BtNodeKind.BlackboardBool:
                    node = CreateBlackboardBool(config.StringParam);
                    return true;
                case BtNodeKind.CustomAction:
                case BtNodeKind.CustomCondition:
                    return false;
                default:
                    return false;
            }
        }

        static BtNode CreateBlackboardBool(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("BlackboardBool requires a non-empty StringParam key.", nameof(key));
            }

            return new BtCondition(ctx => ctx.Blackboard.Get(key, false));
        }
    }
}
