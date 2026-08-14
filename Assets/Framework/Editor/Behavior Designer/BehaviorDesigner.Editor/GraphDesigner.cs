using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    [Serializable]
    public class GraphDesigner : ScriptableObject
    {
        private NodeDesigner mEntryNode;

        private NodeDesigner mRootNode;

        private List<NodeDesigner> mDetachedNodes = new List<NodeDesigner>();

        [SerializeField]
        private List<NodeDesigner> mSelectedNodes = new List<NodeDesigner>();

        private NodeDesigner mHoverNode;

        private NodeConnection mActiveNodeConnection;

        [SerializeField]
        private List<NodeConnection> mSelectedNodeConnections = new List<NodeConnection>();

        [SerializeField]
        private int mNextTaskID;

        private List<int> mNodeSelectedID = new List<int>();

        [SerializeField]
        private int[] mPrevNodeSelectedID;

        public NodeDesigner RootNode => mRootNode;

        public List<NodeDesigner> DetachedNodes => mDetachedNodes;

        public List<NodeDesigner> SelectedNodes => mSelectedNodes;

        public NodeDesigner HoverNode
        {
            get
            {
                return mHoverNode;
            }
            set
            {
                mHoverNode = value;
            }
        }

        public NodeConnection ActiveNodeConnection
        {
            get
            {
                return mActiveNodeConnection;
            }
            set
            {
                mActiveNodeConnection = value;
            }
        }

        public List<NodeConnection> SelectedNodeConnections => mSelectedNodeConnections;

        public void OnEnable()
        {
            base.hideFlags = HideFlags.HideAndDontSave;
        }

        public NodeDesigner AddNode(BehaviorSource behaviorSource, Type type, Vector2 position)
        {
            if (!(Activator.CreateInstance(type, nonPublic: true) is Task task))
            {
                EditorUtility.DisplayDialog("Unable to Add Task", $"Unable to create task of type {type}. Is the class name the same as the file name?", "OK");
                return null;
            }
            try
            {
                task.OnReset();
            }
            catch (Exception)
            {
            }
            return AddNode(behaviorSource, task, position);
        }

        private NodeDesigner AddNode(BehaviorSource behaviorSource, Task task, Vector2 position)
        {
            if (mEntryNode == null)
            {
                Task task2 = Activator.CreateInstance(TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.Tasks.EntryTask")) as Task;
                mEntryNode = ScriptableObject.CreateInstance<NodeDesigner>();
                mEntryNode.LoadNode(task2, behaviorSource, new Vector2(position.x, position.y - 120f), ref mNextTaskID);
                mEntryNode.MakeEntryDisplay();
            }
            NodeDesigner nodeDesigner = ScriptableObject.CreateInstance<NodeDesigner>();
            nodeDesigner.LoadNode(task, behaviorSource, position, ref mNextTaskID);
            TaskNameAttribute[] array = null;
            if ((array = task.GetType().GetCustomAttributes(typeof(TaskNameAttribute), inherit: false) as TaskNameAttribute[]).Length > 0)
            {
                task.FriendlyName = array[0].Name;
            }
            if (mEntryNode.OutgoingNodeConnections.Count == 0)
            {
                mActiveNodeConnection = ScriptableObject.CreateInstance<NodeConnection>();
                mActiveNodeConnection.LoadConnection(mEntryNode, NodeConnectionType.Outgoing);
                ConnectNodes(behaviorSource, nodeDesigner);
            }
            else
            {
                mDetachedNodes.Add(nodeDesigner);
            }
            return nodeDesigner;
        }

        public NodeDesigner NodeAt(Vector2 point, Vector2 offset)
        {
            if (mEntryNode == null)
            {
                return null;
            }
            for (int i = 0; i < mSelectedNodes.Count; i++)
            {
                if (mSelectedNodes[i].Contains(point, offset, includeConnections: false))
                {
                    return mSelectedNodes[i];
                }
            }
            NodeDesigner nodeDesigner = null;
            for (int num = mDetachedNodes.Count - 1; num > -1; num--)
            {
                if (mDetachedNodes[num] != null && (nodeDesigner = NodeChildrenAt(mDetachedNodes[num], point, offset)) != null)
                {
                    return nodeDesigner;
                }
            }
            if (mRootNode != null && (nodeDesigner = NodeChildrenAt(mRootNode, point, offset)) != null)
            {
                return nodeDesigner;
            }
            if (mEntryNode.Contains(point, offset, includeConnections: true))
            {
                return mEntryNode;
            }
            return null;
        }

        private NodeDesigner NodeChildrenAt(NodeDesigner nodeDesigner, Vector2 point, Vector2 offset)
        {
            if (nodeDesigner == null)
            {
                return null;
            }
            if (nodeDesigner.Contains(point, offset, includeConnections: true))
            {
                return nodeDesigner;
            }
            if (nodeDesigner.IsParent)
            {
                ParentTask parentTask = nodeDesigner.Task as ParentTask;
                NodeDesigner nodeDesigner2 = null;
                if (!parentTask.NodeData.Collapsed && parentTask.Children != null)
                {
                    for (int i = 0; i < parentTask.Children.Count; i++)
                    {
                        if (parentTask.Children[i] != null && (nodeDesigner2 = NodeChildrenAt(parentTask.Children[i].NodeData.NodeDesigner as NodeDesigner, point, offset)) != null)
                        {
                            return nodeDesigner2;
                        }
                    }
                }
            }
            return null;
        }

        public List<NodeDesigner> NodesAt(Rect rect, Vector2 offset)
        {
            List<NodeDesigner> nodes = new List<NodeDesigner>();
            if (mRootNode != null)
            {
                NodesChildrenAt(mRootNode, rect, offset, ref nodes);
            }
            for (int i = 0; i < mDetachedNodes.Count; i++)
            {
                NodesChildrenAt(mDetachedNodes[i], rect, offset, ref nodes);
            }
            return (nodes.Count <= 0) ? null : nodes;
        }

        private void NodesChildrenAt(NodeDesigner nodeDesigner, Rect rect, Vector2 offset, ref List<NodeDesigner> nodes)
        {
            if (nodeDesigner.Intersects(rect, offset))
            {
                nodes.Add(nodeDesigner);
            }
            if (!nodeDesigner.IsParent)
            {
                return;
            }
            ParentTask parentTask = nodeDesigner.Task as ParentTask;
            if (parentTask.NodeData.Collapsed || parentTask.Children == null)
            {
                return;
            }
            for (int i = 0; i < parentTask.Children.Count; i++)
            {
                if (parentTask.Children[i] != null)
                {
                    NodesChildrenAt(parentTask.Children[i].NodeData.NodeDesigner as NodeDesigner, rect, offset, ref nodes);
                }
            }
        }

        public bool IsSelected(NodeDesigner nodeDesigner)
        {
            return mSelectedNodes.Contains(nodeDesigner);
        }

        public bool IsParentSelected(NodeDesigner nodeDesigner)
        {
            if (nodeDesigner.ParentNodeDesigner != null)
            {
                if (IsSelected(nodeDesigner.ParentNodeDesigner))
                {
                    return true;
                }
                return IsParentSelected(nodeDesigner.ParentNodeDesigner);
            }
            return false;
        }

        public void Select(NodeDesigner nodeDesigner)
        {
            Select(nodeDesigner, addHash: true);
        }

        public void Select(NodeDesigner nodeDesigner, bool addHash)
        {
            if (!mSelectedNodes.Contains(nodeDesigner))
            {
                if (mSelectedNodes.Count == 1)
                {
                    IndicateReferencedTasks(mSelectedNodes[0].Task, indicate: false);
                }
                mSelectedNodes.Add(nodeDesigner);
                if (addHash)
                {
                    mNodeSelectedID.Add(nodeDesigner.Task.ID);
                }
                nodeDesigner.Select();
                if (mSelectedNodes.Count == 1)
                {
                    IndicateReferencedTasks(mSelectedNodes[0].Task, indicate: true);
                }
            }
        }

        public void Deselect(NodeDesigner nodeDesigner)
        {
            mSelectedNodes.Remove(nodeDesigner);
            mNodeSelectedID.Remove(nodeDesigner.Task.ID);
            nodeDesigner.Deselect();
            IndicateReferencedTasks(nodeDesigner.Task, indicate: false);
        }

        public void DeselectAll(NodeDesigner exceptionNodeDesigner)
        {
            for (int num = mSelectedNodes.Count - 1; num >= 0; num--)
            {
                if (exceptionNodeDesigner == null || !mSelectedNodes[num].Equals(exceptionNodeDesigner))
                {
                    mSelectedNodes[num].Deselect();
                    mSelectedNodes.RemoveAt(num);
                    mNodeSelectedID.RemoveAt(num);
                }
            }
            if (exceptionNodeDesigner != null)
            {
                IndicateReferencedTasks(exceptionNodeDesigner.Task, indicate: false);
            }
        }

        public void ClearNodeSelection()
        {
            if (mSelectedNodes.Count == 1)
            {
                IndicateReferencedTasks(mSelectedNodes[0].Task, indicate: false);
            }
            for (int i = 0; i < mSelectedNodes.Count; i++)
            {
                mSelectedNodes[i].Deselect();
            }
            mSelectedNodes.Clear();
            mNodeSelectedID.Clear();
        }

        public void DeselectWithParent(NodeDesigner nodeDesigner)
        {
            for (int num = mSelectedNodes.Count - 1; num >= 0; num--)
            {
                if (mSelectedNodes[num].HasParent(nodeDesigner))
                {
                    Deselect(mSelectedNodes[num]);
                }
            }
        }

        public bool ReplaceSelectedNodes(BehaviorSource behaviorSource, Type taskType)
        {
            if (SelectedNodes.Count == 0)
            {
                return false;
            }
            for (int num = SelectedNodes.Count - 1; num > -1; num--)
            {
                Vector2 absolutePosition = SelectedNodes[num].GetAbsolutePosition();
                NodeDesigner parentNodeDesigner = SelectedNodes[num].ParentNodeDesigner;
                List<Task> list = ((!SelectedNodes[num].IsParent) ? null : (SelectedNodes[num].Task as ParentTask).Children);
                UnknownTask unknownTask = SelectedNodes[num].Task as UnknownTask;
                RemoveNode(SelectedNodes[num]);
                mSelectedNodes.RemoveAt(num);
                TaskReferences.CheckReferences(behaviorSource);
                NodeDesigner nodeDesigner = null;
                if (unknownTask != null)
                {
                    Task task = null;
                    if (!string.IsNullOrEmpty(unknownTask.JSONSerialization))
                    {
                        Dictionary<int, Task> IDtoTask = new Dictionary<int, Task>();
                        Dictionary<string, object> dictionary = MiniJSON.Deserialize(unknownTask.JSONSerialization) as Dictionary<string, object>;
                        if (dictionary.ContainsKey("Type"))
                        {
                            dictionary["Type"] = taskType.ToString();
                        }
                        task = JSONDeserialization.DeserializeTask(behaviorSource, dictionary, ref IDtoTask, null);
                    }
                    else
                    {
                        TaskSerializationData taskSerializationData = new TaskSerializationData();
                        taskSerializationData.types.Add(taskType.ToString());
                        taskSerializationData.startIndex.Add(0);
                        FieldSerializationData fieldSerializationData = new FieldSerializationData();
                        fieldSerializationData.fieldNameHash = unknownTask.fieldNameHash;
                        fieldSerializationData.startIndex = unknownTask.startIndex;
                        fieldSerializationData.dataPosition = unknownTask.dataPosition;
                        fieldSerializationData.unityObjects = unknownTask.unityObjects;
                        fieldSerializationData.byteDataArray = unknownTask.byteData.ToArray();
                        List<Task> taskList = new List<Task>();
                        BinaryDeserialization.LoadTask(taskSerializationData, fieldSerializationData, ref taskList, ref behaviorSource);
                        if (taskList.Count > 0)
                        {
                            task = taskList[0];
                        }
                    }
                    if (task != null)
                    {
                        nodeDesigner = AddNode(behaviorSource, task, absolutePosition);
                    }
                }
                else
                {
                    nodeDesigner = AddNode(behaviorSource, taskType, absolutePosition);
                }
                if (!(nodeDesigner == null))
                {
                    if (parentNodeDesigner != null)
                    {
                        ActiveNodeConnection = parentNodeDesigner.CreateNodeConnection(incomingNodeConnection: false);
                        ConnectNodes(behaviorSource, nodeDesigner);
                    }
                    if (nodeDesigner.IsParent && list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            ActiveNodeConnection = nodeDesigner.CreateNodeConnection(incomingNodeConnection: false);
                            ConnectNodes(behaviorSource, list[i].NodeData.NodeDesigner as NodeDesigner);
                            if (i >= (nodeDesigner.Task as ParentTask).MaxChildren())
                            {
                                break;
                            }
                        }
                    }
                    Select(nodeDesigner);
                }
            }
            BehaviorUndo.RegisterUndo("Replace", behaviorSource.Owner.GetObject());
            return true;
        }

        public void Hover(NodeDesigner nodeDesigner)
        {
            if (!nodeDesigner.ShowHoverBar)
            {
                nodeDesigner.ShowHoverBar = true;
                HoverNode = nodeDesigner;
            }
        }

        public void ClearHover()
        {
            if ((bool)HoverNode)
            {
                HoverNode.ShowHoverBar = false;
                HoverNode = null;
            }
        }

        private void IndicateReferencedTasks(Task task, bool indicate)
        {
            List<Task> referencedTasks = TaskInspector.GetReferencedTasks(task);
            NodeDesigner nodeDesigner = null;
            if (referencedTasks == null || referencedTasks.Count <= 0)
            {
                return;
            }
            for (int i = 0; i < referencedTasks.Count; i++)
            {
                if (referencedTasks[i] != null && referencedTasks[i].NodeData != null)
                {
                    nodeDesigner = referencedTasks[i].NodeData.NodeDesigner as NodeDesigner;
                    if (nodeDesigner != null)
                    {
                        nodeDesigner.ShowReferenceIcon = indicate;
                    }
                }
            }
        }

        public bool DragSelectedNodes(Vector2 delta, bool dragChildren)
        {
            if (mSelectedNodes.Count == 0)
            {
                return false;
            }
            bool flag = mSelectedNodes.Count == 1;
            for (int i = 0; i < mSelectedNodes.Count; i++)
            {
                DragNode(mSelectedNodes[i], delta, dragChildren);
            }
            if (flag && dragChildren && mSelectedNodes[0].IsEntryDisplay && mRootNode != null)
            {
                DragNode(mRootNode, delta, dragChildren);
            }
            return true;
        }

        private void DragNode(NodeDesigner nodeDesigner, Vector2 delta, bool dragChildren)
        {
            if (IsParentSelected(nodeDesigner) && dragChildren)
            {
                return;
            }
            nodeDesigner.ChangeOffset(delta);
            if (nodeDesigner.ParentNodeDesigner != null)
            {
                int num = nodeDesigner.ParentNodeDesigner.ChildIndexForTask(nodeDesigner.Task);
                if (num != -1)
                {
                    int index = num - 1;
                    bool flag = false;
                    NodeDesigner nodeDesigner2 = nodeDesigner.ParentNodeDesigner.NodeDesignerForChildIndex(index);
                    if (nodeDesigner2 != null && nodeDesigner.Task.NodeData.Offset.x < nodeDesigner2.Task.NodeData.Offset.x)
                    {
                        nodeDesigner.ParentNodeDesigner.MoveChildNode(num, decreaseIndex: true);
                        flag = true;
                    }
                    if (!flag)
                    {
                        index = num + 1;
                        nodeDesigner2 = nodeDesigner.ParentNodeDesigner.NodeDesignerForChildIndex(index);
                        if (nodeDesigner2 != null && nodeDesigner.Task.NodeData.Offset.x > nodeDesigner2.Task.NodeData.Offset.x)
                        {
                            nodeDesigner.ParentNodeDesigner.MoveChildNode(num, decreaseIndex: false);
                        }
                    }
                }
            }
            if (nodeDesigner.IsParent && !dragChildren)
            {
                ParentTask parentTask = nodeDesigner.Task as ParentTask;
                if (parentTask.Children != null)
                {
                    for (int i = 0; i < parentTask.Children.Count; i++)
                    {
                        NodeDesigner nodeDesigner3 = parentTask.Children[i].NodeData.NodeDesigner as NodeDesigner;
                        nodeDesigner3.ChangeOffset(-delta);
                    }
                }
            }
            MarkNodeDirty(nodeDesigner);
        }

        public bool DrawNodes(Vector2 mousePosition, Vector2 offset)
        {
            if (mEntryNode == null)
            {
                return false;
            }
            mEntryNode.DrawNodeConnection(offset, disabled: false);
            if (mRootNode != null)
            {
                DrawNodeConnectionChildren(mRootNode, offset, mRootNode.Task.Disabled);
            }
            for (int i = 0; i < mDetachedNodes.Count; i++)
            {
                DrawNodeConnectionChildren(mDetachedNodes[i], offset, mDetachedNodes[i].Task.Disabled);
            }
            for (int j = 0; j < mSelectedNodeConnections.Count; j++)
            {
                mSelectedNodeConnections[j].DrawConnection(offset, mSelectedNodeConnections[j].OriginatingNodeDesigner.IsDisabled());
            }
            if (mousePosition != new Vector2(-1f, -1f) && mActiveNodeConnection != null)
            {
                mActiveNodeConnection.HorizontalHeight = (mActiveNodeConnection.OriginatingNodeDesigner.GetConnectionPosition(offset, mActiveNodeConnection.NodeConnectionType).y + mousePosition.y) / 2f;
                mActiveNodeConnection.DrawConnection(mActiveNodeConnection.OriginatingNodeDesigner.GetConnectionPosition(offset, mActiveNodeConnection.NodeConnectionType), mousePosition, mActiveNodeConnection.NodeConnectionType == NodeConnectionType.Outgoing && mActiveNodeConnection.OriginatingNodeDesigner.IsDisabled());
            }
            mEntryNode.DrawNode(offset, drawSelected: false, disabled: false);
            bool result = false;
            if (mRootNode != null && DrawNodeChildren(mRootNode, offset, mRootNode.Task.Disabled))
            {
                result = true;
            }
            for (int k = 0; k < mDetachedNodes.Count; k++)
            {
                if (DrawNodeChildren(mDetachedNodes[k], offset, mDetachedNodes[k].Task.Disabled))
                {
                    result = true;
                }
            }
            for (int l = 0; l < mSelectedNodes.Count; l++)
            {
                if (mSelectedNodes[l].DrawNode(offset, drawSelected: true, mSelectedNodes[l].IsDisabled()))
                {
                    result = true;
                }
            }
            if (mRootNode != null)
            {
                DrawNodeCommentChildren(mRootNode, offset);
            }
            for (int m = 0; m < mDetachedNodes.Count; m++)
            {
                DrawNodeCommentChildren(mDetachedNodes[m], offset);
            }
            return result;
        }

        private bool DrawNodeChildren(NodeDesigner nodeDesigner, Vector2 offset, bool disabledNode)
        {
            if (nodeDesigner == null)
            {
                return false;
            }
            bool result = false;
            if (nodeDesigner.DrawNode(offset, drawSelected: false, disabledNode))
            {
                result = true;
            }
            if (nodeDesigner.IsParent)
            {
                ParentTask parentTask = nodeDesigner.Task as ParentTask;
                if (!parentTask.NodeData.Collapsed && parentTask.Children != null)
                {
                    for (int num = parentTask.Children.Count - 1; num > -1; num--)
                    {
                        if (parentTask.Children[num] != null && DrawNodeChildren(parentTask.Children[num].NodeData.NodeDesigner as NodeDesigner, offset, parentTask.Disabled || disabledNode))
                        {
                            result = true;
                        }
                    }
                }
            }
            return result;
        }

        private void DrawNodeConnectionChildren(NodeDesigner nodeDesigner, Vector2 offset, bool disabledNode)
        {
            if (nodeDesigner == null || nodeDesigner.Task.NodeData.Collapsed)
            {
                return;
            }
            nodeDesigner.DrawNodeConnection(offset, nodeDesigner.Task.Disabled || disabledNode);
            if (!nodeDesigner.IsParent)
            {
                return;
            }
            ParentTask parentTask = nodeDesigner.Task as ParentTask;
            if (parentTask.Children == null)
            {
                return;
            }
            for (int i = 0; i < parentTask.Children.Count; i++)
            {
                if (parentTask.Children[i] != null)
                {
                    DrawNodeConnectionChildren(parentTask.Children[i].NodeData.NodeDesigner as NodeDesigner, offset, parentTask.Disabled || disabledNode);
                }
            }
        }

        private void DrawNodeCommentChildren(NodeDesigner nodeDesigner, Vector2 offset)
        {
            if (nodeDesigner == null)
            {
                return;
            }
            nodeDesigner.DrawNodeComment(offset);
            if (!nodeDesigner.IsParent)
            {
                return;
            }
            ParentTask parentTask = nodeDesigner.Task as ParentTask;
            if (parentTask.NodeData.Collapsed || parentTask.Children == null)
            {
                return;
            }
            for (int i = 0; i < parentTask.Children.Count; i++)
            {
                if (parentTask.Children[i] != null)
                {
                    DrawNodeCommentChildren(parentTask.Children[i].NodeData.NodeDesigner as NodeDesigner, offset);
                }
            }
        }

        private void RemoveNode(NodeDesigner nodeDesigner)
        {
            if (nodeDesigner.IsEntryDisplay)
            {
                return;
            }
            if (nodeDesigner.IsParent)
            {
                for (int i = 0; i < nodeDesigner.OutgoingNodeConnections.Count; i++)
                {
                    NodeDesigner destinationNodeDesigner = nodeDesigner.OutgoingNodeConnections[i].DestinationNodeDesigner;
                    mDetachedNodes.Add(destinationNodeDesigner);
                    destinationNodeDesigner.Task.NodeData.Offset = destinationNodeDesigner.GetAbsolutePosition();
                    destinationNodeDesigner.ParentNodeDesigner = null;
                }
            }
            if (nodeDesigner.ParentNodeDesigner != null)
            {
                nodeDesigner.ParentNodeDesigner.RemoveChildNode(nodeDesigner);
            }
            if (mRootNode != null && mRootNode.Equals(nodeDesigner))
            {
                mEntryNode.RemoveChildNode(nodeDesigner);
                mRootNode = null;
            }
            if (mRootNode != null)
            {
                RemoveReferencedTasks(mRootNode, nodeDesigner.Task);
            }
            if (mDetachedNodes != null)
            {
                for (int j = 0; j < mDetachedNodes.Count; j++)
                {
                    RemoveReferencedTasks(mDetachedNodes[j], nodeDesigner.Task);
                }
            }
            mDetachedNodes.Remove(nodeDesigner);
            BehaviorUndo.DestroyObject(nodeDesigner, registerScene: false);
        }

        private void RemoveReferencedTasks(NodeDesigner nodeDesigner, Task task)
        {
            bool fullSync = false;
            bool doReference = false;
            FieldInfo[] serializableFields = TaskUtility.GetSerializableFields(nodeDesigner.Task.GetType());
            for (int i = 0; i < serializableFields.Length; i++)
            {
                if ((serializableFields[i].IsPrivate || serializableFields[i].IsFamily) && !BehaviorDesignerUtility.HasAttribute(serializableFields[i], typeof(SerializeField)))
                {
                    continue;
                }
                if (typeof(IList).IsAssignableFrom(serializableFields[i].FieldType))
                {
                    if ((!typeof(Task).IsAssignableFrom(serializableFields[i].FieldType.GetElementType()) && (!serializableFields[i].FieldType.IsGenericType || !typeof(Task).IsAssignableFrom(serializableFields[i].FieldType.GetGenericArguments()[0]))) || !(serializableFields[i].GetValue(nodeDesigner.Task) is Task[] array))
                    {
                        continue;
                    }
                    for (int num = array.Length - 1; num > -1; num--)
                    {
                        if (array[num] != null && (nodeDesigner.Task.Equals(task) || array[num].Equals(task)))
                        {
                            TaskInspector.ReferenceTasks(nodeDesigner.Task, task, serializableFields[i], ref fullSync, ref doReference, synchronize: false, unreferenceAll: false);
                        }
                    }
                }
                else if (typeof(Task).IsAssignableFrom(serializableFields[i].FieldType) && serializableFields[i].GetValue(nodeDesigner.Task) is Task task2 && (nodeDesigner.Task.Equals(task) || task2.Equals(task)))
                {
                    TaskInspector.ReferenceTasks(nodeDesigner.Task, task, serializableFields[i], ref fullSync, ref doReference, synchronize: false, unreferenceAll: false);
                }
            }
            if (!nodeDesigner.IsParent)
            {
                return;
            }
            ParentTask parentTask = nodeDesigner.Task as ParentTask;
            if (parentTask.Children == null)
            {
                return;
            }
            for (int j = 0; j < parentTask.Children.Count; j++)
            {
                if (parentTask.Children[j] != null)
                {
                    RemoveReferencedTasks(parentTask.Children[j].NodeData.NodeDesigner as NodeDesigner, task);
                }
            }
        }

        public bool NodeCanOriginateConnection(NodeDesigner nodeDesigner, NodeConnection connection)
        {
            return !nodeDesigner.IsEntryDisplay || (nodeDesigner.IsEntryDisplay && connection.NodeConnectionType == NodeConnectionType.Outgoing);
        }

        public bool NodeCanAcceptConnection(NodeDesigner nodeDesigner, NodeConnection connection)
        {
            if ((!nodeDesigner.IsEntryDisplay || connection.NodeConnectionType != 0) && (nodeDesigner.IsEntryDisplay || (!nodeDesigner.IsParent && (nodeDesigner.IsParent || connection.NodeConnectionType != NodeConnectionType.Outgoing))))
            {
                return false;
            }
            if (nodeDesigner.IsEntryDisplay || connection.OriginatingNodeDesigner.IsEntryDisplay)
            {
                return true;
            }
            HashSet<NodeDesigner> set = new HashSet<NodeDesigner>();
            NodeDesigner nodeDesigner2 = ((connection.NodeConnectionType != NodeConnectionType.Outgoing) ? connection.OriginatingNodeDesigner : nodeDesigner);
            NodeDesigner item = ((connection.NodeConnectionType != NodeConnectionType.Outgoing) ? nodeDesigner : connection.OriginatingNodeDesigner);
            if (CycleExists(nodeDesigner2, ref set))
            {
                return false;
            }
            if (set.Contains(item))
            {
                return false;
            }
            return true;
        }

        private bool CycleExists(NodeDesigner nodeDesigner, ref HashSet<NodeDesigner> set)
        {
            if (set.Contains(nodeDesigner))
            {
                return true;
            }
            set.Add(nodeDesigner);
            if (nodeDesigner.IsParent)
            {
                ParentTask parentTask = nodeDesigner.Task as ParentTask;
                if (parentTask.Children != null)
                {
                    for (int i = 0; i < parentTask.Children.Count; i++)
                    {
                        if (CycleExists(parentTask.Children[i].NodeData.NodeDesigner as NodeDesigner, ref set))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void ConnectNodes(BehaviorSource behaviorSource, NodeDesigner nodeDesigner)
        {
            NodeConnection nodeConnection = mActiveNodeConnection;
            mActiveNodeConnection = null;
            if (nodeConnection != null && !nodeConnection.OriginatingNodeDesigner.Equals(nodeDesigner))
            {
                NodeDesigner originatingNodeDesigner = nodeConnection.OriginatingNodeDesigner;
                if (nodeConnection.NodeConnectionType == NodeConnectionType.Outgoing)
                {
                    RemoveParentConnection(nodeDesigner);
                    CheckForLastConnectionRemoval(originatingNodeDesigner);
                    originatingNodeDesigner.AddChildNode(nodeDesigner, nodeConnection, adjustOffset: true, replaceNode: false);
                }
                else
                {
                    RemoveParentConnection(originatingNodeDesigner);
                    CheckForLastConnectionRemoval(nodeDesigner);
                    nodeDesigner.AddChildNode(originatingNodeDesigner, nodeConnection, adjustOffset: true, replaceNode: false);
                }
                if (nodeConnection.OriginatingNodeDesigner.IsEntryDisplay)
                {
                    mRootNode = nodeConnection.DestinationNodeDesigner;
                }
                mDetachedNodes.Remove(nodeConnection.DestinationNodeDesigner);
            }
        }

        private void RemoveParentConnection(NodeDesigner nodeDesigner)
        {
            if (!(nodeDesigner.ParentNodeDesigner != null))
            {
                return;
            }
            NodeDesigner parentNodeDesigner = nodeDesigner.ParentNodeDesigner;
            NodeConnection nodeConnection = null;
            for (int i = 0; i < parentNodeDesigner.OutgoingNodeConnections.Count; i++)
            {
                if (parentNodeDesigner.OutgoingNodeConnections[i].DestinationNodeDesigner.Equals(nodeDesigner))
                {
                    nodeConnection = parentNodeDesigner.OutgoingNodeConnections[i];
                    break;
                }
            }
            if (nodeConnection != null)
            {
                RemoveConnection(nodeConnection);
            }
        }

        private void CheckForLastConnectionRemoval(NodeDesigner nodeDesigner)
        {
            if (nodeDesigner.IsEntryDisplay)
            {
                if (nodeDesigner.OutgoingNodeConnections.Count == 1)
                {
                    RemoveConnection(nodeDesigner.OutgoingNodeConnections[0]);
                }
                return;
            }
            ParentTask parentTask = nodeDesigner.Task as ParentTask;
            if (parentTask.Children == null || parentTask.Children.Count + 1 <= parentTask.MaxChildren())
            {
                return;
            }
            NodeConnection nodeConnection = null;
            for (int i = 0; i < nodeDesigner.OutgoingNodeConnections.Count; i++)
            {
                if (nodeDesigner.OutgoingNodeConnections[i].DestinationNodeDesigner.Equals(parentTask.Children[parentTask.Children.Count - 1].NodeData.NodeDesigner as NodeDesigner))
                {
                    nodeConnection = nodeDesigner.OutgoingNodeConnections[i];
                    break;
                }
            }
            if (nodeConnection != null)
            {
                RemoveConnection(nodeConnection);
            }
        }

        public void NodeConnectionsAt(Vector2 point, Vector2 offset, ref List<NodeConnection> nodeConnections)
        {
            if (!(mEntryNode == null))
            {
                NodeChildrenConnectionsAt(mEntryNode, point, offset, ref nodeConnections);
                if (mRootNode != null)
                {
                    NodeChildrenConnectionsAt(mRootNode, point, offset, ref nodeConnections);
                }
                for (int i = 0; i < mDetachedNodes.Count; i++)
                {
                    NodeChildrenConnectionsAt(mDetachedNodes[i], point, offset, ref nodeConnections);
                }
            }
        }

        private void NodeChildrenConnectionsAt(NodeDesigner nodeDesigner, Vector2 point, Vector2 offset, ref List<NodeConnection> nodeConnections)
        {
            if (nodeDesigner.Task.NodeData.Collapsed)
            {
                return;
            }
            nodeDesigner.ConnectionContains(point, offset, ref nodeConnections);
            if (!nodeDesigner.IsParent || !(nodeDesigner.Task is ParentTask parentTask) || parentTask.Children == null)
            {
                return;
            }
            for (int i = 0; i < parentTask.Children.Count; i++)
            {
                if (parentTask.Children[i] != null)
                {
                    NodeChildrenConnectionsAt(parentTask.Children[i].NodeData.NodeDesigner as NodeDesigner, point, offset, ref nodeConnections);
                }
            }
        }

        public void RemoveConnection(NodeConnection nodeConnection)
        {
            nodeConnection.DestinationNodeDesigner.Task.NodeData.Offset = nodeConnection.DestinationNodeDesigner.GetAbsolutePosition();
            mDetachedNodes.Add(nodeConnection.DestinationNodeDesigner);
            nodeConnection.OriginatingNodeDesigner.RemoveChildNode(nodeConnection.DestinationNodeDesigner);
            if (nodeConnection.OriginatingNodeDesigner.IsEntryDisplay)
            {
                mRootNode = null;
            }
        }

        public bool IsSelected(NodeConnection nodeConnection)
        {
            for (int i = 0; i < mSelectedNodeConnections.Count; i++)
            {
                if (mSelectedNodeConnections[i].Equals(nodeConnection))
                {
                    return true;
                }
            }
            return false;
        }

        public void Select(NodeConnection nodeConnection)
        {
            mSelectedNodeConnections.Add(nodeConnection);
            nodeConnection.select();
        }

        public void Deselect(NodeConnection nodeConnection)
        {
            mSelectedNodeConnections.Remove(nodeConnection);
            nodeConnection.deselect();
        }

        public void ClearConnectionSelection()
        {
            for (int i = 0; i < mSelectedNodeConnections.Count; i++)
            {
                mSelectedNodeConnections[i].deselect();
            }
            mSelectedNodeConnections.Clear();
        }

        public void GraphDirty()
        {
            if (!(mEntryNode == null))
            {
                mEntryNode.MarkDirty();
                if (mRootNode != null)
                {
                    MarkNodeDirty(mRootNode);
                }
                for (int num = mDetachedNodes.Count - 1; num > -1; num--)
                {
                    MarkNodeDirty(mDetachedNodes[num]);
                }
            }
        }

        private void MarkNodeDirty(NodeDesigner nodeDesigner)
        {
            nodeDesigner.MarkDirty();
            if (nodeDesigner.IsEntryDisplay)
            {
                if (nodeDesigner.OutgoingNodeConnections.Count > 0 && nodeDesigner.OutgoingNodeConnections[0].DestinationNodeDesigner != null)
                {
                    MarkNodeDirty(nodeDesigner.OutgoingNodeConnections[0].DestinationNodeDesigner);
                }
            }
            else
            {
                if (!nodeDesigner.IsParent)
                {
                    return;
                }
                ParentTask parentTask = nodeDesigner.Task as ParentTask;
                if (parentTask.Children == null)
                {
                    return;
                }
                for (int i = 0; i < parentTask.Children.Count; i++)
                {
                    if (parentTask.Children[i] != null)
                    {
                        MarkNodeDirty(parentTask.Children[i].NodeData.NodeDesigner as NodeDesigner);
                    }
                }
            }
        }

        public void Find(string findTaskValue, SharedVariable findSharedVariable)
        {
            if (findTaskValue != null)
            {
                findTaskValue = findTaskValue.ToLower();
            }
            if (mRootNode != null)
            {
                Find(mRootNode, findTaskValue, findSharedVariable);
            }
            for (int i = 0; i < mDetachedNodes.Count; i++)
            {
                Find(mDetachedNodes[i], findTaskValue, findSharedVariable);
            }
        }

        private void Find(NodeDesigner nodeDesigner, string findTaskValue, SharedVariable findSharedVariable)
        {
            if (nodeDesigner == null || nodeDesigner.Task == null)
            {
                return;
            }
            bool flag = false;
            if (!string.IsNullOrEmpty(findTaskValue) && findTaskValue.Length > 2)
            {
                if (nodeDesigner.Task.FriendlyName.ToLower().Contains(findTaskValue))
                {
                    flag = true;
                }
                else if (nodeDesigner.Task.GetType().FullName.ToLower().Contains(findTaskValue))
                {
                    flag = true;
                }
            }
            if (!flag)
            {
                FieldInfo[] serializableFields = TaskUtility.GetSerializableFields(nodeDesigner.Task.GetType());
                for (int i = 0; i < serializableFields.Length; i++)
                {
                    Type fieldType = serializableFields[i].FieldType;
                    if (findSharedVariable != null && typeof(SharedVariable).IsAssignableFrom(fieldType))
                    {
                        if (serializableFields[i].GetValue(nodeDesigner.Task) is SharedVariable sharedVariable && sharedVariable.Name == findSharedVariable.Name && sharedVariable.IsGlobal == findSharedVariable.IsGlobal)
                        {
                            flag = true;
                            break;
                        }
                    }
                    else
                    {
                        if (!BehaviorDesignerUtility.HasAttribute(serializableFields[i], typeof(InspectTaskAttribute)))
                        {
                            continue;
                        }
                        if (typeof(IList).IsAssignableFrom(serializableFields[i].FieldType))
                        {
                            if (!(serializableFields[i].GetValue(nodeDesigner.Task) is IList list))
                            {
                                continue;
                            }
                            for (int j = 0; j < list.Count; j++)
                            {
                                if (FindInspectedTask(list[j] as Task, findTaskValue, findSharedVariable))
                                {
                                    flag = true;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            flag = FindInspectedTask(serializableFields[i].GetValue(nodeDesigner.Task) as Task, findTaskValue, findSharedVariable);
                        }
                    }
                }
            }
            nodeDesigner.FoundTask(flag);
            if (!nodeDesigner.IsParent)
            {
                return;
            }
            ParentTask parentTask = nodeDesigner.Task as ParentTask;
            if (parentTask.Children == null)
            {
                return;
            }
            for (int k = 0; k < parentTask.Children.Count; k++)
            {
                if (parentTask.Children[k] != null)
                {
                    Find(parentTask.Children[k].NodeData.NodeDesigner as NodeDesigner, findTaskValue, findSharedVariable);
                }
            }
        }

        private bool FindInspectedTask(Task inspectedTask, string findTaskValue, SharedVariable findSharedVariable)
        {
            if (inspectedTask == null)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(findTaskValue) && findTaskValue.Length > 2 && (inspectedTask.FriendlyName.ToLower().Contains(findTaskValue) || inspectedTask.GetType().FullName.ToLower().Contains(findTaskValue)))
            {
                return true;
            }
            FieldInfo[] publicFields = TaskUtility.GetPublicFields(inspectedTask.GetType());
            for (int i = 0; i < publicFields.Length; i++)
            {
                if (findSharedVariable != null && typeof(SharedVariable).IsAssignableFrom(publicFields[i].FieldType) && publicFields[i].GetValue(inspectedTask) is SharedVariable sharedVariable && sharedVariable.Name == findSharedVariable.Name && sharedVariable.IsGlobal == findSharedVariable.IsGlobal)
                {
                    return true;
                }
            }
            return false;
        }

        public List<BehaviorSource> FindReferencedBehaviors()
        {
            List<BehaviorSource> behaviors = new List<BehaviorSource>();
            if (mRootNode != null)
            {
                FindReferencedBehaviors(mRootNode, ref behaviors);
            }
            for (int i = 0; i < mDetachedNodes.Count; i++)
            {
                FindReferencedBehaviors(mDetachedNodes[i], ref behaviors);
            }
            return behaviors;
        }

        public void FindReferencedBehaviors(NodeDesigner nodeDesigner, ref List<BehaviorSource> behaviors)
        {
            FieldInfo[] publicFields = TaskUtility.GetPublicFields(nodeDesigner.Task.GetType());
            for (int i = 0; i < publicFields.Length; i++)
            {
                Type fieldType = publicFields[i].FieldType;
                if (typeof(IList).IsAssignableFrom(fieldType))
                {
                    Type type = fieldType;
                    if (fieldType.IsGenericType)
                    {
                        while (!type.IsGenericType)
                        {
                            type = type.BaseType;
                        }
                        type = fieldType.GetGenericArguments()[0];
                    }
                    else
                    {
                        type = fieldType.GetElementType();
                    }
                    if (type == null)
                    {
                        continue;
                    }
                    if (typeof(ExternalBehavior).IsAssignableFrom(type) || typeof(Behavior).IsAssignableFrom(type))
                    {
                        if (!(publicFields[i].GetValue(nodeDesigner.Task) is IList list))
                        {
                            continue;
                        }
                        for (int j = 0; j < list.Count; j++)
                        {
                            if (list[j] == null)
                            {
                                continue;
                            }
                            BehaviorSource behaviorSource = null;
                            if (list[j] is ExternalBehavior)
                            {
                                behaviorSource = (list[j] as ExternalBehavior).BehaviorSource;
                                if (behaviorSource.Owner == null)
                                {
                                    behaviorSource.Owner = list[j] as ExternalBehavior;
                                }
                            }
                            else
                            {
                                behaviorSource = (list[j] as Behavior).GetBehaviorSource();
                                if (behaviorSource.Owner == null)
                                {
                                    behaviorSource.Owner = list[j] as Behavior;
                                }
                            }
                            behaviors.Add(behaviorSource);
                        }
                    }
                    else if (!typeof(Behavior).IsAssignableFrom(type))
                    {
                    }
                }
                else
                {
                    if (!typeof(ExternalBehavior).IsAssignableFrom(fieldType) && !typeof(Behavior).IsAssignableFrom(fieldType))
                    {
                        continue;
                    }
                    object value = publicFields[i].GetValue(nodeDesigner.Task);
                    if (value == null)
                    {
                        continue;
                    }
                    BehaviorSource behaviorSource2 = null;
                    if (value is ExternalBehavior)
                    {
                        behaviorSource2 = (value as ExternalBehavior).BehaviorSource;
                        if (behaviorSource2.Owner == null)
                        {
                            behaviorSource2.Owner = value as ExternalBehavior;
                        }
                        behaviors.Add(behaviorSource2);
                    }
                    else
                    {
                        behaviorSource2 = (value as Behavior).GetBehaviorSource();
                        if (behaviorSource2.Owner == null)
                        {
                            behaviorSource2.Owner = value as Behavior;
                        }
                    }
                    behaviors.Add(behaviorSource2);
                }
            }
            if (!nodeDesigner.IsParent)
            {
                return;
            }
            ParentTask parentTask = nodeDesigner.Task as ParentTask;
            if (parentTask.Children == null)
            {
                return;
            }
            for (int k = 0; k < parentTask.Children.Count; k++)
            {
                if (parentTask.Children[k] != null)
                {
                    FindReferencedBehaviors(parentTask.Children[k].NodeData.NodeDesigner as NodeDesigner, ref behaviors);
                }
            }
        }

        public void SelectAll()
        {
            for (int num = mSelectedNodes.Count - 1; num > -1; num--)
            {
                Deselect(mSelectedNodes[num]);
            }
            if (mRootNode != null)
            {
                SelectAll(mRootNode);
            }
            for (int num2 = mDetachedNodes.Count - 1; num2 > -1; num2--)
            {
                SelectAll(mDetachedNodes[num2]);
            }
        }

        private void SelectAll(NodeDesigner nodeDesigner)
        {
            Select(nodeDesigner);
            if (!nodeDesigner.Task.GetType().IsSubclassOf(typeof(ParentTask)))
            {
                return;
            }
            ParentTask parentTask = nodeDesigner.Task as ParentTask;
            if (parentTask.Children != null)
            {
                for (int i = 0; i < parentTask.Children.Count; i++)
                {
                    SelectAll(parentTask.Children[i].NodeData.NodeDesigner as NodeDesigner);
                }
            }
        }

        public int GetTaskCount()
        {
            int num = mDetachedNodes.Count;
            if (mRootNode != null)
            {
                num += GetTaskCount(mRootNode);
            }
            return num;
        }

        private int GetTaskCount(NodeDesigner nodeDesigner)
        {
            int num = 1;
            if (nodeDesigner.Task.GetType().IsSubclassOf(typeof(ParentTask)))
            {
                ParentTask parentTask = nodeDesigner.Task as ParentTask;
                if (parentTask.Children != null)
                {
                    for (int i = 0; i < parentTask.Children.Count; i++)
                    {
                        num += GetTaskCount(parentTask.Children[i].NodeData.NodeDesigner as NodeDesigner);
                    }
                }
            }
            return num;
        }

        public void IdentifyNode(NodeDesigner nodeDesigner)
        {
            nodeDesigner.IdentifyNode();
        }

        public List<TaskSerializer> Copy(Vector2 graphOffset, float graphZoom)
        {
            List<TaskSerializer> list = new List<TaskSerializer>();
            for (int i = 0; i < mSelectedNodes.Count; i++)
            {
                TaskSerializer taskSerializer;
                if ((taskSerializer = TaskCopier.CopySerialized(mSelectedNodes[i].Task)) == null)
                {
                    continue;
                }
                if (mSelectedNodes[i].IsParent)
                {
                    ParentTask parentTask = mSelectedNodes[i].Task as ParentTask;
                    if (parentTask.Children != null)
                    {
                        List<int> list2 = new List<int>();
                        int num = -1;
                        for (int j = 0; j < parentTask.Children.Count; j++)
                        {
                            if ((num = mSelectedNodes.IndexOf(parentTask.Children[j].NodeData.NodeDesigner as NodeDesigner)) != -1)
                            {
                                list2.Add(num);
                            }
                        }
                        taskSerializer.childrenIndex = list2;
                    }
                }
                taskSerializer.offset = (taskSerializer.offset + graphOffset) * graphZoom;
                list.Add(taskSerializer);
            }
            return (list.Count <= 0) ? null : list;
        }

        public bool Paste(BehaviorSource behaviorSource, Vector3 position, List<TaskSerializer> copiedTasks, Vector2 graphOffset, float graphZoom)
        {
            if (copiedTasks == null || copiedTasks.Count == 0)
            {
                return false;
            }
            ClearNodeSelection();
            ClearConnectionSelection();
            RemapIDs();
            List<NodeDesigner> list = new List<NodeDesigner>();
            for (int i = 0; i < copiedTasks.Count; i++)
            {
                TaskSerializer taskSerializer = copiedTasks[i];
                Task task = TaskCopier.PasteTask(behaviorSource, taskSerializer);
                NodeDesigner nodeDesigner = ScriptableObject.CreateInstance<NodeDesigner>();
                nodeDesigner.LoadTask(task, (behaviorSource.Owner == null) ? null : (behaviorSource.Owner.GetObject() as Behavior), ref mNextTaskID);
                nodeDesigner.Task.NodeData.Offset = taskSerializer.offset / graphZoom - graphOffset;
                list.Add(nodeDesigner);
                mDetachedNodes.Add(nodeDesigner);
                Select(nodeDesigner);
            }
            for (int j = 0; j < copiedTasks.Count; j++)
            {
                TaskSerializer taskSerializer2 = copiedTasks[j];
                if (taskSerializer2.childrenIndex != null)
                {
                    for (int k = 0; k < taskSerializer2.childrenIndex.Count; k++)
                    {
                        NodeDesigner nodeDesigner2 = list[j];
                        NodeConnection nodeConnection = ScriptableObject.CreateInstance<NodeConnection>();
                        nodeConnection.LoadConnection(nodeDesigner2, NodeConnectionType.Outgoing);
                        nodeDesigner2.AddChildNode(list[taskSerializer2.childrenIndex[k]], nodeConnection, adjustOffset: true, replaceNode: false);
                        mDetachedNodes.Remove(list[taskSerializer2.childrenIndex[k]]);
                    }
                }
            }
            if (mEntryNode == null)
            {
                Task task2 = Activator.CreateInstance(TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.Tasks.EntryTask")) as Task;
                mEntryNode = ScriptableObject.CreateInstance<NodeDesigner>();
                mEntryNode.LoadNode(task2, behaviorSource, new Vector2(position.x, position.y - 120f), ref mNextTaskID);
                mEntryNode.MakeEntryDisplay();
                if (mDetachedNodes.Count > 0)
                {
                    mActiveNodeConnection = ScriptableObject.CreateInstance<NodeConnection>();
                    mActiveNodeConnection.LoadConnection(mEntryNode, NodeConnectionType.Outgoing);
                    ConnectNodes(behaviorSource, mDetachedNodes[0]);
                }
            }
            Save(behaviorSource);
            return true;
        }

        public bool Delete(BehaviorSource behaviorSource, BehaviorDesignerWindow.TaskCallbackHandler callback)
        {
            bool flag = false;
            if (mSelectedNodeConnections != null)
            {
                for (int i = 0; i < mSelectedNodeConnections.Count; i++)
                {
                    RemoveConnection(mSelectedNodeConnections[i]);
                }
                mSelectedNodeConnections.Clear();
                flag = true;
            }
            if (mSelectedNodes != null)
            {
                for (int j = 0; j < mSelectedNodes.Count; j++)
                {
                    callback?.Invoke(behaviorSource, mSelectedNodes[j].Task);
                    RemoveNode(mSelectedNodes[j]);
                }
                mSelectedNodes.Clear();
                flag = true;
            }
            if (flag)
            {
                BehaviorUndo.RegisterUndo("Delete", behaviorSource.Owner.GetObject());
                TaskReferences.CheckReferences(behaviorSource);
                Save(behaviorSource);
            }
            return flag;
        }

        public bool RemoveSharedVariableReferences(SharedVariable sharedVariable)
        {
            if (mEntryNode == null)
            {
                return false;
            }
            bool result = false;
            if (mRootNode != null && RemoveSharedVariableReference(mRootNode, sharedVariable))
            {
                result = true;
            }
            if (mDetachedNodes != null)
            {
                for (int i = 0; i < mDetachedNodes.Count; i++)
                {
                    if (RemoveSharedVariableReference(mDetachedNodes[i], sharedVariable))
                    {
                        result = true;
                    }
                }
            }
            return result;
        }

        private bool RemoveSharedVariableReference(NodeDesigner nodeDesigner, SharedVariable sharedVariable)
        {
            bool result = false;
            FieldInfo[] serializableFields = TaskUtility.GetSerializableFields(nodeDesigner.Task.GetType());
            for (int i = 0; i < serializableFields.Length; i++)
            {
                if (typeof(SharedVariable).IsAssignableFrom(serializableFields[i].FieldType) && serializableFields[i].GetValue(nodeDesigner.Task) is SharedVariable sharedVariable2 && !string.IsNullOrEmpty(sharedVariable2.Name) && sharedVariable2.IsGlobal == sharedVariable.IsGlobal && sharedVariable2.Name.Equals(sharedVariable.Name))
                {
                    if (!serializableFields[i].FieldType.IsAbstract)
                    {
                        SharedVariable sharedVariable3 = Activator.CreateInstance(serializableFields[i].FieldType) as SharedVariable;
                        sharedVariable3.IsShared = true;
                        serializableFields[i].SetValue(nodeDesigner.Task, sharedVariable3);
                    }
                    result = true;
                }
            }
            if (nodeDesigner.IsParent)
            {
                ParentTask parentTask = nodeDesigner.Task as ParentTask;
                if (parentTask.Children != null)
                {
                    for (int j = 0; j < parentTask.Children.Count; j++)
                    {
                        if (parentTask.Children[j] != null && RemoveSharedVariableReference(parentTask.Children[j].NodeData.NodeDesigner as NodeDesigner, sharedVariable))
                        {
                            result = true;
                        }
                    }
                }
            }
            return result;
        }

        private void RemapIDs()
        {
            if (!(mEntryNode == null))
            {
                mNextTaskID = 0;
                mEntryNode.SetID(ref mNextTaskID);
                if (mRootNode != null)
                {
                    mRootNode.SetID(ref mNextTaskID);
                }
                for (int i = 0; i < mDetachedNodes.Count; i++)
                {
                    mDetachedNodes[i].SetID(ref mNextTaskID);
                }
                mNodeSelectedID.Clear();
                for (int j = 0; j < mSelectedNodes.Count; j++)
                {
                    mNodeSelectedID.Add(mSelectedNodes[j].Task.ID);
                }
            }
        }

        public Rect GraphSize(Vector3 offset)
        {
            if (mEntryNode == null)
            {
                return default(Rect);
            }
            Rect minMaxRect = default(Rect);
            minMaxRect.xMin = float.MaxValue;
            minMaxRect.xMax = float.MinValue;
            minMaxRect.yMin = float.MaxValue;
            minMaxRect.yMax = float.MinValue;
            GetNodeMinMax(offset, mEntryNode, ref minMaxRect);
            if (mRootNode != null)
            {
                GetNodeMinMax(offset, mRootNode, ref minMaxRect);
            }
            for (int i = 0; i < mDetachedNodes.Count; i++)
            {
                GetNodeMinMax(offset, mDetachedNodes[i], ref minMaxRect);
            }
            return minMaxRect;
        }

        private void GetNodeMinMax(Vector2 offset, NodeDesigner nodeDesigner, ref Rect minMaxRect)
        {
            Rect rect = nodeDesigner.Rectangle(offset, includeConnections: true, includeComments: true);
            if (rect.xMin < minMaxRect.xMin)
            {
                minMaxRect.xMin = rect.xMin;
            }
            if (rect.yMin < minMaxRect.yMin)
            {
                minMaxRect.yMin = rect.yMin;
            }
            if (rect.xMax > minMaxRect.xMax)
            {
                minMaxRect.xMax = rect.xMax;
            }
            if (rect.yMax > minMaxRect.yMax)
            {
                minMaxRect.yMax = rect.yMax;
            }
            if (!nodeDesigner.IsParent)
            {
                return;
            }
            ParentTask parentTask = nodeDesigner.Task as ParentTask;
            if (parentTask.Children != null)
            {
                for (int i = 0; i < parentTask.Children.Count; i++)
                {
                    GetNodeMinMax(offset, parentTask.Children[i].NodeData.NodeDesigner as NodeDesigner, ref minMaxRect);
                }
            }
        }

        public void Save(BehaviorSource behaviorSource)
        {
            if (!object.ReferenceEquals(behaviorSource.Owner.GetObject(), null))
            {
                RemapIDs();
                List<Task> list = new List<Task>();
                for (int i = 0; i < mDetachedNodes.Count; i++)
                {
                    list.Add(mDetachedNodes[i].Task);
                }
                behaviorSource.Save((!(mEntryNode != null)) ? null : mEntryNode.Task, (!(mRootNode != null)) ? null : mRootNode.Task, list);
                if (BehaviorDesignerPreferences.GetBool(BDPreferences.BinarySerialization))
                {
                    BinarySerialization.Save(behaviorSource);
                }
                else
                {
                    JSONSerialization.Save(behaviorSource);
                }
            }
        }

        public bool Load(BehaviorSource behaviorSource, bool loadPrevBehavior, Vector2 nodePosition)
        {
            if (behaviorSource == null)
            {
                Clear(saveSelectedNodes: false);
                return false;
            }
            DestroyNodeDesigners();
            if (behaviorSource.Owner != null && behaviorSource.Owner is Behavior && (behaviorSource.Owner as Behavior).ExternalBehavior != null)
            {
                List<SharedVariable> list = null;
                ExternalBehavior externalBehavior = (behaviorSource.Owner as Behavior).ExternalBehavior;
                externalBehavior.BehaviorSource.Owner = externalBehavior;
                externalBehavior.BehaviorSource.CheckForSerialization(!Application.isPlaying, behaviorSource);
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        behaviorSource.SetVariable(list[i].Name, list[i]);
                    }
                }
            }
            else
            {
                behaviorSource.CheckForSerialization(!Application.isPlaying);
            }
            if (behaviorSource.EntryTask == null && behaviorSource.RootTask == null && behaviorSource.DetachedTasks == null)
            {
                Clear(saveSelectedNodes: false);
                return false;
            }
            if (loadPrevBehavior)
            {
                mSelectedNodes.Clear();
                mSelectedNodeConnections.Clear();
                if (mPrevNodeSelectedID != null)
                {
                    for (int j = 0; j < mPrevNodeSelectedID.Length; j++)
                    {
                        mNodeSelectedID.Add(mPrevNodeSelectedID[j]);
                    }
                    mPrevNodeSelectedID = null;
                }
            }
            else
            {
                Clear(saveSelectedNodes: false);
            }
            mNextTaskID = 0;
            mEntryNode = null;
            mRootNode = null;
            mDetachedNodes.Clear();
            behaviorSource.Load(out var entryTask, out var rootTask, out var detachedTasks);
            if (BehaviorDesignerUtility.AnyNullTasks(behaviorSource) || (behaviorSource.TaskData != null && BehaviorDesignerUtility.HasRootTask(behaviorSource.TaskData.JSONSerialization) && behaviorSource.RootTask == null))
            {
                behaviorSource.CheckForSerialization(force: true);
                behaviorSource.Load(out entryTask, out rootTask, out detachedTasks);
            }
            if (entryTask == null)
            {
                if (rootTask != null || (detachedTasks != null && detachedTasks.Count > 0))
                {
                    entryTask = (behaviorSource.EntryTask = Activator.CreateInstance(TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.Tasks.EntryTask"), nonPublic: true) as Task);
                    mEntryNode = ScriptableObject.CreateInstance<NodeDesigner>();
                    if (rootTask != null)
                    {
                        mEntryNode.LoadNode(entryTask, behaviorSource, new Vector2(rootTask.NodeData.Offset.x, rootTask.NodeData.Offset.y - 120f), ref mNextTaskID);
                    }
                    else
                    {
                        mEntryNode.LoadNode(entryTask, behaviorSource, new Vector2(nodePosition.x, nodePosition.y - 120f), ref mNextTaskID);
                    }
                    mEntryNode.MakeEntryDisplay();
                }
            }
            else
            {
                mEntryNode = ScriptableObject.CreateInstance<NodeDesigner>();
                mEntryNode.LoadTask(entryTask, (behaviorSource.Owner == null) ? null : (behaviorSource.Owner.GetObject() as Behavior), ref mNextTaskID);
                mEntryNode.MakeEntryDisplay();
            }
            if (rootTask != null)
            {
                mRootNode = ScriptableObject.CreateInstance<NodeDesigner>();
                mRootNode.LoadTask(rootTask, (behaviorSource.Owner == null) ? null : (behaviorSource.Owner.GetObject() as Behavior), ref mNextTaskID);
                NodeConnection nodeConnection = ScriptableObject.CreateInstance<NodeConnection>();
                nodeConnection.LoadConnection(mEntryNode, NodeConnectionType.Fixed);
                mEntryNode.AddChildNode(mRootNode, nodeConnection, adjustOffset: false, replaceNode: false);
                LoadNodeSelection(mRootNode);
                if (mEntryNode.OutgoingNodeConnections.Count == 0)
                {
                    mActiveNodeConnection = ScriptableObject.CreateInstance<NodeConnection>();
                    mActiveNodeConnection.LoadConnection(mEntryNode, NodeConnectionType.Outgoing);
                    ConnectNodes(behaviorSource, mRootNode);
                }
            }
            if (detachedTasks != null)
            {
                for (int k = 0; k < detachedTasks.Count; k++)
                {
                    if (detachedTasks[k] != null)
                    {
                        NodeDesigner nodeDesigner = ScriptableObject.CreateInstance<NodeDesigner>();
                        nodeDesigner.LoadTask(detachedTasks[k], (behaviorSource.Owner == null) ? null : (behaviorSource.Owner.GetObject() as Behavior), ref mNextTaskID);
                        mDetachedNodes.Add(nodeDesigner);
                        LoadNodeSelection(nodeDesigner);
                    }
                }
            }
            return true;
        }

        public bool HasEntryNode()
        {
            return mEntryNode != null && mEntryNode.Task != null;
        }

        public Vector2 EntryNodeOffset()
        {
            return mEntryNode.Task.NodeData.Offset;
        }

        public void SetStartOffset(Vector2 offset)
        {
            Vector2 vector = offset - mEntryNode.Task.NodeData.Offset;
            mEntryNode.Task.NodeData.Offset = offset;
            for (int i = 0; i < mDetachedNodes.Count; i++)
            {
                mDetachedNodes[i].Task.NodeData.Offset += vector;
            }
        }

        private void LoadNodeSelection(NodeDesigner nodeDesigner)
        {
            if (nodeDesigner == null)
            {
                return;
            }
            if (mNodeSelectedID != null && mNodeSelectedID.Contains(nodeDesigner.Task.ID))
            {
                Select(nodeDesigner, addHash: false);
            }
            if (!nodeDesigner.IsParent)
            {
                return;
            }
            ParentTask parentTask = nodeDesigner.Task as ParentTask;
            if (parentTask.Children == null)
            {
                return;
            }
            for (int i = 0; i < parentTask.Children.Count; i++)
            {
                if (parentTask.Children[i] != null && parentTask.Children[i].NodeData != null)
                {
                    LoadNodeSelection(parentTask.Children[i].NodeData.NodeDesigner as NodeDesigner);
                }
            }
        }

        public void Clear(bool saveSelectedNodes)
        {
            if (saveSelectedNodes)
            {
                if (mNodeSelectedID.Count > 0)
                {
                    mPrevNodeSelectedID = mNodeSelectedID.ToArray();
                }
            }
            else
            {
                mPrevNodeSelectedID = null;
            }
            mNodeSelectedID.Clear();
            mSelectedNodes.Clear();
            mSelectedNodeConnections.Clear();
            DestroyNodeDesigners();
        }

        public void DestroyNodeDesigners()
        {
            if (mEntryNode != null)
            {
                Clear(mEntryNode);
            }
            if (mRootNode != null)
            {
                Clear(mRootNode);
            }
            for (int num = mDetachedNodes.Count - 1; num > -1; num--)
            {
                Clear(mDetachedNodes[num]);
            }
            mEntryNode = null;
            mRootNode = null;
            mDetachedNodes = new List<NodeDesigner>();
        }

        private void Clear(NodeDesigner nodeDesigner)
        {
            if (nodeDesigner == null)
            {
                return;
            }
            if (nodeDesigner.IsParent && nodeDesigner.Task is ParentTask parentTask && parentTask.Children != null)
            {
                for (int num = parentTask.Children.Count - 1; num > -1; num--)
                {
                    if (parentTask.Children[num] != null)
                    {
                        Clear(parentTask.Children[num].NodeData.NodeDesigner as NodeDesigner);
                    }
                }
            }
            nodeDesigner.DestroyConnections();
            UnityEngine.Object.DestroyImmediate(nodeDesigner, allowDestroyingAssets: true);
        }
    }
}