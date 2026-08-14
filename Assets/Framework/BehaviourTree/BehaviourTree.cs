using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 行为树运行时宿主。一棵实例对应一个 Agent；模板可共享。
    /// </summary>
    public sealed class BehaviourTree
    {
        /// <summary>从根节点创建（同时生成仅供本实例使用的模板）。</summary>
        /// <param name="root">根节点；不可为 null。</param>
        /// <param name="name">调试名；可为 null。</param>
        public BehaviourTree(BtNode root, string name = null)
            : this(CreateTemplate(root, name))
        {
        }

        /// <summary>从共享模板实例化，分配独立运行时槽。</summary>
        /// <param name="template">模板；不可为 null。</param>
        public BehaviourTree(BtTreeTemplate template)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Runtime = new BtRuntime(template.NodeCount);
            Name = template.Name;
        }

        /// <summary>共享拓扑。</summary>
        public BtTreeTemplate Template { get; }

        /// <summary>本 Agent 运行时槽。</summary>
        public BtRuntime Runtime { get; }

        /// <summary>根节点。</summary>
        public BtNode Root => Template.Root;

        /// <summary>调试名。</summary>
        public string Name { get; }

        /// <summary>上一帧 Tick 结果。</summary>
        public BtStatus LastStatus { get; private set; } = BtStatus.Failure;

        /// <summary>最近一次调试帧；未采集则为 null。</summary>
        public BtDebugFrame LastDebugFrame { get; private set; }

        /// <summary>从同一模板再开一个 Agent 实例。</summary>
        /// <returns>新实例。</returns>
        public BehaviourTree Instantiate() => new BehaviourTree(Template);

        /// <summary>从已绑定的根创建模板。</summary>
        /// <param name="root">根；不可为 null。</param>
        /// <param name="name">名。</param>
        /// <returns>模板。</returns>
        public static BtTreeTemplate CreateTemplate(BtNode root, string name = null)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            return new BtTreeTemplate(root, name, BtNode.Flatten(root));
        }

        /// <summary>推进一逻辑帧。</summary>
        /// <param name="context">上下文；不可为 null。</param>
        /// <returns>本帧根节点状态。</returns>
        public BtStatus Tick(BtContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Runtime = Runtime;
            var collect = context.CollectDebug;
            var startTicks = collect ? Stopwatch.GetTimestamp() : 0L;
            LastStatus = Root.Tick(context);
            var elapsed = collect ? Stopwatch.GetTimestamp() - startTicks : 0L;

#if UNITY_EDITOR
            LastDebugFrame = BuildDebugFrame(elapsed, context);
            BtDebugHub.Publish(this, context, LastDebugFrame);
#else
            if (collect || context.BreakpointHit)
            {
                LastDebugFrame = BuildDebugFrame(elapsed, context);
                BtDebugHub.Publish(this, context, LastDebugFrame);
            }
#endif

            return LastStatus;
        }

        /// <summary>重置整棵树运行时状态。</summary>
        /// <param name="context">上下文；不可为 null。</param>
        public void Reset(BtContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Runtime = Runtime;
            Root.Reset(context);
            LastStatus = BtStatus.Failure;
        }

        /// <summary>中止当前执行并重置。</summary>
        /// <param name="context">上下文；不可为 null。</param>
        public void Abort(BtContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Runtime = Runtime;
            Root.Abort(context);
            LastStatus = BtStatus.Failure;
        }

        /// <summary>捕获可还原快照。</summary>
        /// <param name="context">上下文；不可为 null。</param>
        /// <returns>快照。</returns>
        public BtSnapshot CaptureSnapshot(BtContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new BtSnapshot(context.FrameIndex, LastStatus, Runtime.Clone(), context.Blackboard.Clone());
        }

        /// <summary>从快照恢复运行时槽、黑板与帧号。</summary>
        /// <param name="snapshot">快照；不可为 null。</param>
        /// <param name="context">上下文；不可为 null。</param>
        public void RestoreSnapshot(BtSnapshot snapshot, BtContext context)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Runtime.CopyFrom(snapshot.Runtime);
            context.Blackboard.CopyAuthoritativeFrom(snapshot.Blackboard);
            context.FrameIndex = snapshot.FrameIndex;
            LastStatus = snapshot.LastStatus;
        }

        BtDebugFrame BuildDebugFrame(long elapsedTicks, BtContext context)
        {
            var statuses = new BtStatus[Runtime.NodeCount];
            for (var i = 0; i < statuses.Length; i++)
            {
                statuses[i] = Runtime.GetStatus(i);
            }

            var path = new List<int>(8);
            CollectRunningPath(Root, path);
            return new BtDebugFrame
            {
                Statuses = statuses,
                RunningPath = path.ToArray(),
                ElapsedTicks = elapsedTicks,
                BreakpointNodeIndex = context != null ? context.BreakpointNodeIndex : -1
            };
        }

        void CollectRunningPath(BtNode node, List<int> path)
        {
            if (node == null || Runtime.GetStatus(node.Index) != BtStatus.Running)
            {
                return;
            }

            while (node != null)
            {
                path.Add(node.Index);
                BtNode runningChild = null;
                for (var i = 0; i < node.ChildCount; i++)
                {
                    var child = node.GetChild(i);
                    if (child != null && Runtime.GetStatus(child.Index) == BtStatus.Running)
                    {
                        runningChild = child;
                        break;
                    }
                }

                node = runningChild;
            }
        }
    }
}
