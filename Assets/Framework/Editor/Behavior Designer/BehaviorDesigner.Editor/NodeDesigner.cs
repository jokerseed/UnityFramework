using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    [Serializable]
    public class NodeDesigner : ScriptableObject
    {
        [SerializeField]
        private Task mTask;

        [SerializeField]
        private bool mSelected;

        private int mIdentifyUpdateCount = -1;

        private bool mFoundTask;

        [SerializeField]
        private bool mConnectionIsDirty;

        private bool mRectIsDirty = true;

        private bool mIncomingRectIsDirty = true;

        private bool mOutgoingRectIsDirty = true;

        [SerializeField]
        private bool isParent;

        [SerializeField]
        private bool isEntryDisplay;

        [SerializeField]
        private bool showReferenceIcon;

        private bool showHoverBar;

        private bool hasError;

        [SerializeField]
        private string taskName = string.Empty;

        private Rect mRectangle;

        private Rect mOutgoingRectangle;

        private Rect mIncomingRectangle;

        private bool prevRunningState;

        private int prevCommentLength = -1;

        private List<int> prevWatchedFieldsLength = new List<int>();

        private int prevFriendlyNameLength = -1;

        [SerializeField]
        private NodeDesigner parentNodeDesigner;

        [SerializeField]
        private List<NodeConnection> outgoingNodeConnections;

        private bool mCacheIsDirty = true;

        private readonly Color grayColor = new Color(0.7f, 0.7f, 0.7f);

        private Rect nodeCollapsedTextureRect;

        private Rect iconTextureRect;

        private Rect titleRect;

        private Rect breakpointTextureRect;

        private Rect errorTextureRect;

        private Rect referenceTextureRect;

        private Rect conditionalAbortTextureRect;

        private Rect conditionalAbortLowerPriorityTextureRect;

        private Rect disabledButtonTextureRect;

        private Rect collapseButtonTextureRect;

        private Rect incomingConnectionTextureRect;

        private Rect outgoingConnectionTextureRect;

        private Rect successReevaluatingExecutionStatusTextureRect;

        private Rect successExecutionStatusTextureRect;

        private Rect failureExecutionStatusTextureRect;

        private Rect iconBorderTextureRect;

        private Rect watchedFieldRect;

        private Rect watchedFieldNamesRect;

        private Rect watchedFieldValuesRect;

        private Rect commentRect;

        private Rect commentLabelRect;

        public Task Task
        {
            get
            {
                return mTask;
            }
            set
            {
                mTask = value;
                Init();
            }
        }

        public bool IsParent => isParent;

        public bool IsEntryDisplay => isEntryDisplay;

        public bool ShowReferenceIcon
        {
            set
            {
                showReferenceIcon = value;
            }
        }

        public bool ShowHoverBar
        {
            get
            {
                return showHoverBar;
            }
            set
            {
                showHoverBar = value;
            }
        }

        public bool HasError
        {
            set
            {
                hasError = value;
            }
        }

        public NodeDesigner ParentNodeDesigner
        {
            get
            {
                return parentNodeDesigner;
            }
            set
            {
                parentNodeDesigner = value;
            }
        }

        public List<NodeConnection> OutgoingNodeConnections => outgoingNodeConnections;

        public void Select()
        {
            if (!isEntryDisplay)
            {
                mSelected = true;
            }
        }

        public void Deselect()
        {
            mSelected = false;
        }

        public void MarkDirty()
        {
            mConnectionIsDirty = true;
            mRectIsDirty = true;
            mIncomingRectIsDirty = true;
            mOutgoingRectIsDirty = true;
        }

        public Rect IncomingConnectionRect(Vector2 offset)
        {
            if (!mIncomingRectIsDirty)
            {
                return mIncomingRectangle;
            }
            Rect rect = Rectangle(offset, includeConnections: false, includeComments: false);
            mIncomingRectangle = new Rect(rect.x + (rect.width - 42f) / 2f, rect.y - 14f, 42f, 14f);
            mIncomingRectIsDirty = false;
            return mIncomingRectangle;
        }

        public Rect OutgoingConnectionRect(Vector2 offset)
        {
            if (!mOutgoingRectIsDirty)
            {
                return mOutgoingRectangle;
            }
            Rect rect = Rectangle(offset, includeConnections: false, includeComments: false);
            mOutgoingRectangle = new Rect(rect.x + (rect.width - 42f) / 2f, rect.yMax, 42f, 16f);
            mOutgoingRectIsDirty = false;
            return mOutgoingRectangle;
        }

        public void OnEnable()
        {
            base.hideFlags = HideFlags.HideAndDontSave;
        }

        public void LoadTask(Task task, Behavior owner, ref int id)
        {
            if (task == null)
            {
                return;
            }
            mTask = task;
            if (owner != null)
            {
                mTask.Owner = owner;
            }
            mTask.ID = id++;
            mTask.NodeData.NodeDesigner = this;
            mTask.NodeData.InitWatchedFields(mTask);
            if (!mTask.NodeData.FriendlyName.Equals(string.Empty))
            {
                mTask.FriendlyName = mTask.NodeData.FriendlyName;
                mTask.NodeData.FriendlyName = string.Empty;
            }
            LoadTaskIcon();
            Init();
            RequiredComponentAttribute[] array;
            if (mTask.Owner != null && (array = mTask.GetType().GetCustomAttributes(typeof(RequiredComponentAttribute), inherit: true) as RequiredComponentAttribute[]).Length > 0)
            {
                Type componentType = array[0].ComponentType;
                if (typeof(Component).IsAssignableFrom(componentType) && mTask.Owner.gameObject.GetComponent(componentType) == null)
                {
                    mTask.Owner.gameObject.AddComponent(componentType);
                }
            }
            List<Type> baseClasses = FieldInspector.GetBaseClasses(mTask.GetType());
            BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (int num = baseClasses.Count - 1; num > -1; num--)
            {
                FieldInfo[] fields = baseClasses[num].GetFields(bindingAttr);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (typeof(SharedVariable).IsAssignableFrom(fields[i].FieldType) && !fields[i].FieldType.IsAbstract)
                    {
                        SharedVariable sharedVariable = fields[i].GetValue(mTask) as SharedVariable;
                        if (sharedVariable == null)
                        {
                            sharedVariable = Activator.CreateInstance(fields[i].FieldType) as SharedVariable;
                        }
                        if (TaskUtility.HasAttribute(fields[i], typeof(RequiredFieldAttribute)) || TaskUtility.HasAttribute(fields[i], typeof(SharedRequiredAttribute)))
                        {
                            sharedVariable.IsShared = true;
                        }
                        fields[i].SetValue(mTask, sharedVariable);
                    }
                }
            }
            if (!isParent)
            {
                return;
            }
            ParentTask parentTask = mTask as ParentTask;
            if (parentTask.Children != null)
            {
                for (int j = 0; j < parentTask.Children.Count; j++)
                {
                    NodeDesigner nodeDesigner = ScriptableObject.CreateInstance<NodeDesigner>();
                    nodeDesigner.LoadTask(parentTask.Children[j], owner, ref id);
                    NodeConnection nodeConnection = ScriptableObject.CreateInstance<NodeConnection>();
                    nodeConnection.LoadConnection(this, NodeConnectionType.Fixed);
                    AddChildNode(nodeDesigner, nodeConnection, adjustOffset: true, replaceNode: true, j);
                }
            }
            mConnectionIsDirty = true;
        }

        public void LoadNode(Task task, BehaviorSource behaviorSource, Vector2 offset, ref int id)
        {
            mTask = task;
            mTask.Owner = behaviorSource.Owner as Behavior;
            mTask.ID = id++;
            mTask.NodeData = new NodeData();
            mTask.NodeData.Offset = offset;
            mTask.NodeData.NodeDesigner = this;
            LoadTaskIcon();
            Init();
            mTask.FriendlyName = taskName;
            RequiredComponentAttribute[] array;
            if (mTask.Owner != null && (array = mTask.GetType().GetCustomAttributes(typeof(RequiredComponentAttribute), inherit: true) as RequiredComponentAttribute[]).Length > 0)
            {
                Type componentType = array[0].ComponentType;
                if (typeof(Component).IsAssignableFrom(componentType) && mTask.Owner.gameObject.GetComponent(componentType) == null)
                {
                    mTask.Owner.gameObject.AddComponent(componentType);
                }
            }
            List<Type> baseClasses = FieldInspector.GetBaseClasses(mTask.GetType());
            BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (int num = baseClasses.Count - 1; num > -1; num--)
            {
                FieldInfo[] fields = baseClasses[num].GetFields(bindingAttr);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (typeof(SharedVariable).IsAssignableFrom(fields[i].FieldType) && !fields[i].FieldType.IsAbstract)
                    {
                        SharedVariable sharedVariable = fields[i].GetValue(mTask) as SharedVariable;
                        if (sharedVariable == null)
                        {
                            sharedVariable = Activator.CreateInstance(fields[i].FieldType) as SharedVariable;
                        }
                        if (TaskUtility.HasAttribute(fields[i], typeof(RequiredFieldAttribute)) || TaskUtility.HasAttribute(fields[i], typeof(SharedRequiredAttribute)))
                        {
                            sharedVariable.IsShared = true;
                        }
                        fields[i].SetValue(mTask, sharedVariable);
                    }
                }
            }
        }

        private void LoadTaskIcon()
        {
            TaskIconAttribute[] array = null;
            mTask.NodeData.Icon = null;
            if ((array = mTask.GetType().GetCustomAttributes(typeof(TaskIconAttribute), inherit: true) as TaskIconAttribute[]).Length > 0)
            {
                mTask.NodeData.Icon = BehaviorDesignerUtility.LoadIcon(array[0].IconPath);
            }
            if (mTask.NodeData.Icon == null)
            {
                string empty = string.Empty;
                empty = (mTask.GetType().IsSubclassOf(typeof(BehaviorDesigner.Runtime.Tasks.Action)) ? "{SkinColor}ActionIcon.png" : (mTask.GetType().IsSubclassOf(typeof(Conditional)) ? "{SkinColor}ConditionalIcon.png" : (mTask.GetType().IsSubclassOf(typeof(Composite)) ? "{SkinColor}CompositeIcon.png" : ((!mTask.GetType().IsSubclassOf(typeof(Decorator))) ? "{SkinColor}EntryIcon.png" : "{SkinColor}DecoratorIcon.png"))));
                mTask.NodeData.Icon = BehaviorDesignerUtility.LoadIcon(empty);
            }
        }

        private void Init()
        {
            taskName = BehaviorDesignerUtility.SplitCamelCase(mTask.GetType().Name.ToString());
            isParent = mTask.GetType().IsSubclassOf(typeof(ParentTask));
            if (isParent)
            {
                outgoingNodeConnections = new List<NodeConnection>();
            }
            mRectIsDirty = (mCacheIsDirty = true);
            mIncomingRectIsDirty = true;
            mOutgoingRectIsDirty = true;
        }

        public void MakeEntryDisplay()
        {
            isEntryDisplay = (isParent = true);
            mTask.FriendlyName = (taskName = "Entry");
            outgoingNodeConnections = new List<NodeConnection>();
        }

        public Vector2 GetAbsolutePosition()
        {
            Vector2 offset = mTask.NodeData.Offset;
            if (parentNodeDesigner != null)
            {
                offset += parentNodeDesigner.GetAbsolutePosition();
            }
            if (BehaviorDesignerPreferences.GetBool(BDPreferences.SnapToGrid))
            {
                offset.Set(BehaviorDesignerUtility.RoundToNearest(offset.x, 10f), BehaviorDesignerUtility.RoundToNearest(offset.y, 10f));
            }
            return offset;
        }

        public Rect Rectangle(Vector2 offset, bool includeConnections, bool includeComments)
        {
            Rect result = Rectangle(offset);
            if (includeConnections)
            {
                if (!isEntryDisplay)
                {
                    result.yMin -= 14f;
                }
                if (isParent)
                {
                    result.yMax += 16f;
                }
            }
            if (includeComments && mTask != null)
            {
                if (mTask.NodeData.WatchedFields != null && mTask.NodeData.WatchedFields.Count > 0 && result.xMax < watchedFieldRect.xMax)
                {
                    result.xMax = watchedFieldRect.xMax;
                }
                if (!GetNodeComment().Equals(string.Empty))
                {
                    if (result.xMax < commentRect.xMax)
                    {
                        result.xMax = commentRect.xMax;
                    }
                    if (result.yMax < commentRect.yMax)
                    {
                        result.yMax = commentRect.yMax;
                    }
                }
            }
            return result;
        }

        private Rect Rectangle(Vector2 offset)
        {
            if (!mRectIsDirty)
            {
                return mRectangle;
            }
            mCacheIsDirty = true;
            if (mTask == null)
            {
                return default(Rect);
            }
            float num = BehaviorDesignerUtility.TaskTitleGUIStyle.CalcSize(new GUIContent(ToString())).x + 20f;
            if (!isParent)
            {
                BehaviorDesignerUtility.TaskCommentGUIStyle.CalcMinMaxWidth(new GUIContent(GetNodeComment()), out var _, out var maxWidth);
                maxWidth += 20f;
                num = ((!(num > maxWidth)) ? maxWidth : num);
            }
            num = Mathf.Min(220f, Mathf.Max(100f, num));
            Vector2 absolutePosition = GetAbsolutePosition();
            float height = 20 + ((!BehaviorDesignerPreferences.GetBool(BDPreferences.CompactMode)) ? 52 : 22);
            mRectangle = new Rect(absolutePosition.x + offset.x - num / 2f, absolutePosition.y + offset.y, num, height);
            mRectIsDirty = false;
            return mRectangle;
        }

        private void UpdateCache(Rect nodeRect)
        {
            if (mCacheIsDirty)
            {
                nodeCollapsedTextureRect = new Rect(nodeRect.x + (nodeRect.width - 26f) / 2f + 1f, nodeRect.yMax + 2f, 26f, 6f);
                iconTextureRect = new Rect(nodeRect.x + (nodeRect.width - 44f) / 2f, nodeRect.y + 4f + 2f, 44f, 44f);
                titleRect = new Rect(nodeRect.x, nodeRect.yMax - (float)((!BehaviorDesignerPreferences.GetBool(BDPreferences.CompactMode)) ? 20 : 28) - 1f, nodeRect.width, 20f);
                breakpointTextureRect = new Rect(nodeRect.xMax - 16f, nodeRect.y + 3f, 14f, 14f);
                errorTextureRect = new Rect(nodeRect.xMax - 12f, nodeRect.y - 8f, 20f, 20f);
                referenceTextureRect = new Rect(nodeRect.x + 2f, nodeRect.y + 3f, 14f, 14f);
                conditionalAbortTextureRect = new Rect(nodeRect.x + 3f, nodeRect.y + 3f, 16f, 16f);
                conditionalAbortLowerPriorityTextureRect = new Rect(nodeRect.x + 3f, nodeRect.y, 16f, 16f);
                disabledButtonTextureRect = new Rect(nodeRect.x - 1f, nodeRect.y - 17f, 14f, 14f);
                collapseButtonTextureRect = new Rect(nodeRect.x + 15f, nodeRect.y - 17f, 14f, 14f);
                incomingConnectionTextureRect = new Rect(nodeRect.x + (nodeRect.width - 42f) / 2f, nodeRect.y - 14f - 3f + 3f, 42f, 17f);
                outgoingConnectionTextureRect = new Rect(nodeRect.x + (nodeRect.width - 42f) / 2f, nodeRect.yMax - 3f, 42f, 19f);
                successReevaluatingExecutionStatusTextureRect = new Rect(nodeRect.xMax - 37f, nodeRect.yMax - 38f, 35f, 36f);
                successExecutionStatusTextureRect = new Rect(nodeRect.xMax - 37f, nodeRect.yMax - 33f, 35f, 31f);
                failureExecutionStatusTextureRect = new Rect(nodeRect.xMax - 37f, nodeRect.yMax - 38f, 35f, 36f);
                iconBorderTextureRect = new Rect(nodeRect.x + (nodeRect.width - 46f) / 2f, nodeRect.y + 3f + 2f, 46f, 46f);
                CalculateNodeCommentRect(nodeRect);
                mCacheIsDirty = false;
            }
        }

        private void CalculateNodeCommentRect(Rect nodeRect)
        {
            bool flag = false;
            if (mTask.NodeData.WatchedFields != null && mTask.NodeData.WatchedFields.Count > 0)
            {
                string text = string.Empty;
                string text2 = string.Empty;
                for (int i = 0; i < mTask.NodeData.WatchedFields.Count; i++)
                {
                    FieldInfo fieldInfo = mTask.NodeData.WatchedFields[i];
                    text = text + BehaviorDesignerUtility.SplitCamelCase(fieldInfo.Name) + ": \n";
                    text2 = text2 + ((fieldInfo.GetValue(mTask) == null) ? "null" : fieldInfo.GetValue(mTask).ToString()) + "\n";
                }
                BehaviorDesignerUtility.TaskCommentGUIStyle.CalcMinMaxWidth(new GUIContent(text), out var minWidth, out var maxWidth);
                BehaviorDesignerUtility.TaskCommentGUIStyle.CalcMinMaxWidth(new GUIContent(text2), out minWidth, out var maxWidth2);
                float num = maxWidth;
                float width = maxWidth2;
                float num2 = Mathf.Min(220f, maxWidth + maxWidth2 + 20f);
                if (num2 == 220f)
                {
                    num = maxWidth / (maxWidth + maxWidth2) * 220f;
                    width = maxWidth2 / (maxWidth + maxWidth2) * 220f;
                }
                watchedFieldRect = new Rect(nodeRect.xMax + 4f, nodeRect.y, num2 + 8f, nodeRect.height);
                watchedFieldNamesRect = new Rect(nodeRect.xMax + 6f, nodeRect.y + 4f, num, nodeRect.height - 8f);
                watchedFieldValuesRect = new Rect(nodeRect.xMax + 6f + num, nodeRect.y + 4f, width, nodeRect.height - 8f);
                flag = true;
            }
            string nodeComment = GetNodeComment();
            if (nodeComment.Equals(string.Empty))
            {
                return;
            }
            if (isParent)
            {
                BehaviorDesignerUtility.TaskCommentGUIStyle.CalcMinMaxWidth(new GUIContent(nodeComment), out var _, out var maxWidth3);
                float num3 = Mathf.Min(220f, maxWidth3 + 20f);
                if (flag)
                {
                    commentRect = new Rect(nodeRect.xMin - 12f - num3, nodeRect.y, num3 + 8f, nodeRect.height);
                    commentLabelRect = new Rect(nodeRect.xMin - 6f - num3, nodeRect.y + 4f, num3, nodeRect.height - 8f);
                }
                else
                {
                    commentRect = new Rect(nodeRect.xMax + 4f, nodeRect.y, num3 + 8f, nodeRect.height);
                    commentLabelRect = new Rect(nodeRect.xMax + 6f, nodeRect.y + 4f, num3, nodeRect.height - 8f);
                }
            }
            else
            {
                float num4 = Mathf.Min(100f, BehaviorDesignerUtility.TaskCommentGUIStyle.CalcHeight(new GUIContent(nodeComment), nodeRect.width - 4f));
                commentRect = new Rect(nodeRect.x, nodeRect.yMax + 4f, nodeRect.width, num4 + 4f);
                commentLabelRect = new Rect(nodeRect.x, nodeRect.yMax + 4f, nodeRect.width - 4f, num4);
            }
        }

        private string GetNodeComment()
        {
            string text = string.Empty;
            if (!mTask.OnDrawNodeText().Equals(string.Empty))
            {
                text = mTask.OnDrawNodeText();
            }
            if (!mTask.NodeData.Comment.Equals(string.Empty))
            {
                if (!text.Equals(string.Empty))
                {
                    text += "\n";
                }
                text += mTask.NodeData.Comment;
            }
            return text;
        }

        public bool DrawNode(Vector2 offset, bool drawSelected, bool disabled)
        {
            if (drawSelected != mSelected)
            {
                return false;
            }
            if (ToString().Length != prevFriendlyNameLength)
            {
                prevFriendlyNameLength = ToString().Length;
                mRectIsDirty = true;
            }
            Rect rect = Rectangle(offset, includeConnections: false, includeComments: false);
            UpdateCache(rect);
            bool flag = (mTask.NodeData.PushTime != -1f && mTask.NodeData.PushTime >= mTask.NodeData.PopTime) || (isEntryDisplay && outgoingNodeConnections.Count > 0 && outgoingNodeConnections[0].DestinationNodeDesigner.Task.NodeData.PushTime != -1f);
            bool flag2 = mIdentifyUpdateCount != -1 || mFoundTask;
            bool result = prevRunningState != flag;
            float num = ((!BehaviorDesignerPreferences.GetBool(BDPreferences.FadeNodes)) ? 0.01f : 0.5f);
            float num2 = 0f;
            if (flag2)
            {
                num2 = ((2000 - mIdentifyUpdateCount >= 500) ? 1f : ((float)(2000 - mIdentifyUpdateCount) / 500f));
                if (mIdentifyUpdateCount != -1)
                {
                    mIdentifyUpdateCount++;
                    if (mIdentifyUpdateCount > 2000)
                    {
                        mIdentifyUpdateCount = -1;
                    }
                }
                result = true;
            }
            else if (flag)
            {
                num2 = 1f;
            }
            else if ((mTask.NodeData.PopTime != -1f && num != 0f && mTask.NodeData.PopTime <= Time.realtimeSinceStartup && Time.realtimeSinceStartup - mTask.NodeData.PopTime < num) || (isEntryDisplay && outgoingNodeConnections.Count > 0 && outgoingNodeConnections[0].DestinationNodeDesigner.Task.NodeData.PopTime != -1f && outgoingNodeConnections[0].DestinationNodeDesigner.Task.NodeData.PopTime <= Time.realtimeSinceStartup && Time.realtimeSinceStartup - outgoingNodeConnections[0].DestinationNodeDesigner.Task.NodeData.PopTime < num))
            {
                num2 = ((!isEntryDisplay) ? (1f - (Time.realtimeSinceStartup - mTask.NodeData.PopTime) / num) : (1f - (Time.realtimeSinceStartup - outgoingNodeConnections[0].DestinationNodeDesigner.Task.NodeData.PopTime) / num));
                result = true;
            }
            if (!isEntryDisplay && !prevRunningState && parentNodeDesigner != null)
            {
                parentNodeDesigner.BringConnectionToFront(this);
            }
            prevRunningState = flag;
            if (num2 != 1f)
            {
                GUI.color = ((!disabled && !mTask.Disabled) ? Color.white : grayColor);
                GUIStyle gUIStyle = null;
                DrawNodeTexture(backgroundGUIStyle: (!BehaviorDesignerPreferences.GetBool(BDPreferences.CompactMode)) ? ((!mSelected) ? BehaviorDesignerUtility.GetTaskGUIStyle(mTask.NodeData.ColorIndex) : BehaviorDesignerUtility.GetTaskSelectedGUIStyle(mTask.NodeData.ColorIndex)) : ((!mSelected) ? BehaviorDesignerUtility.GetTaskCompactGUIStyle(mTask.NodeData.ColorIndex) : BehaviorDesignerUtility.GetTaskSelectedCompactGUIStyle(mTask.NodeData.ColorIndex)), nodeRect: rect, connectionTopTexture: BehaviorDesignerUtility.GetTaskConnectionTopTexture(mTask.NodeData.ColorIndex), connectionBottomTexture: BehaviorDesignerUtility.GetTaskConnectionBottomTexture(mTask.NodeData.ColorIndex), iconBorderTexture: BehaviorDesignerUtility.GetTaskBorderTexture(mTask.NodeData.ColorIndex));
            }
            if (num2 > 0f)
            {
                GUIStyle gUIStyle2 = null;
                Texture2D texture2D = null;
                if (flag2)
                {
                    gUIStyle2 = (BehaviorDesignerPreferences.GetBool(BDPreferences.CompactMode) ? ((!mSelected) ? BehaviorDesignerUtility.TaskIdentifyCompactGUIStyle : BehaviorDesignerUtility.TaskIdentifySelectedCompactGUIStyle) : ((!mSelected) ? BehaviorDesignerUtility.TaskIdentifyGUIStyle : BehaviorDesignerUtility.TaskIdentifySelectedGUIStyle));
                    texture2D = BehaviorDesignerUtility.TaskBorderIdentifyTexture;
                }
                else
                {
                    gUIStyle2 = (BehaviorDesignerPreferences.GetBool(BDPreferences.CompactMode) ? ((!mSelected) ? BehaviorDesignerUtility.TaskRunningCompactGUIStyle : BehaviorDesignerUtility.TaskRunningSelectedCompactGUIStyle) : ((!mSelected) ? BehaviorDesignerUtility.TaskRunningGUIStyle : BehaviorDesignerUtility.TaskRunningSelectedGUIStyle));
                    texture2D = BehaviorDesignerUtility.TaskBorderRunningTexture;
                }
                Color color = ((!disabled && !mTask.Disabled) ? Color.white : grayColor);
                color.a = num2;
                GUI.color = color;
                Texture2D connectionTopTexture = null;
                Texture2D connectionBottomTexture = null;
                if (!isEntryDisplay)
                {
                    connectionTopTexture = ((!flag2) ? BehaviorDesignerUtility.TaskConnectionRunningTopTexture : BehaviorDesignerUtility.TaskConnectionIdentifyTopTexture);
                }
                if (isParent)
                {
                    connectionBottomTexture = ((!flag2) ? BehaviorDesignerUtility.TaskConnectionRunningBottomTexture : BehaviorDesignerUtility.TaskConnectionIdentifyBottomTexture);
                }
                DrawNodeTexture(rect, connectionTopTexture, connectionBottomTexture, gUIStyle2, texture2D);
                GUI.color = Color.white;
            }
            if (mTask.NodeData.Collapsed)
            {
                GUI.DrawTexture(nodeCollapsedTextureRect, BehaviorDesignerUtility.TaskConnectionCollapsedTexture);
            }
            if (!BehaviorDesignerPreferences.GetBool(BDPreferences.CompactMode))
            {
                GUI.DrawTexture(iconTextureRect, mTask.NodeData.Icon);
            }
            if (mTask.NodeData.InterruptTime != -1f && Time.realtimeSinceStartup - mTask.NodeData.InterruptTime < 0.75f + num)
            {
                float a = ((!(Time.realtimeSinceStartup - mTask.NodeData.InterruptTime < 0.75f)) ? (1f - (Time.realtimeSinceStartup - (mTask.NodeData.InterruptTime + 0.75f)) / num) : 1f);
                Color white = Color.white;
                white.a = a;
                GUI.color = white;
                GUI.Label(rect, string.Empty, BehaviorDesignerUtility.TaskHighlightGUIStyle);
                GUI.color = Color.white;
            }
            GUI.Label(titleRect, ToString(), BehaviorDesignerUtility.TaskTitleGUIStyle);
            if (mTask.NodeData.IsBreakpoint)
            {
                GUI.DrawTexture(breakpointTextureRect, BehaviorDesignerUtility.BreakpointTexture);
            }
            if (showReferenceIcon)
            {
                GUI.DrawTexture(referenceTextureRect, BehaviorDesignerUtility.ReferencedTexture);
            }
            if (hasError)
            {
                GUI.DrawTexture(errorTextureRect, BehaviorDesignerUtility.ErrorIconTexture);
            }
            if (mTask is Composite && (mTask as Composite).AbortType != 0)
            {
                switch ((mTask as Composite).AbortType)
                {
                    case AbortType.Self:
                        GUI.DrawTexture(conditionalAbortTextureRect, BehaviorDesignerUtility.ConditionalAbortSelfTexture);
                        break;
                    case AbortType.LowerPriority:
                        GUI.DrawTexture(conditionalAbortLowerPriorityTextureRect, BehaviorDesignerUtility.ConditionalAbortLowerPriorityTexture);
                        break;
                    case AbortType.Both:
                        GUI.DrawTexture(conditionalAbortTextureRect, BehaviorDesignerUtility.ConditionalAbortBothTexture);
                        break;
                }
            }
            GUI.color = Color.white;
            if (showHoverBar)
            {
                GUI.DrawTexture(disabledButtonTextureRect, (!mTask.Disabled) ? BehaviorDesignerUtility.DisableTaskTexture : BehaviorDesignerUtility.EnableTaskTexture, ScaleMode.ScaleToFit);
                if (isParent || mTask is BehaviorReference)
                {
                    bool collapsed = mTask.NodeData.Collapsed;
                    if (mTask is BehaviorReference)
                    {
                        collapsed = (mTask as BehaviorReference).collapsed;
                    }
                    GUI.DrawTexture(collapseButtonTextureRect, (!collapsed) ? BehaviorDesignerUtility.CollapseTaskTexture : BehaviorDesignerUtility.ExpandTaskTexture, ScaleMode.ScaleToFit);
                }
            }
            return result;
        }

        private void DrawNodeTexture(Rect nodeRect, Texture2D connectionTopTexture, Texture2D connectionBottomTexture, GUIStyle backgroundGUIStyle, Texture2D iconBorderTexture)
        {
            if (!isEntryDisplay)
            {
                GUI.DrawTexture(incomingConnectionTextureRect, connectionTopTexture, ScaleMode.ScaleToFit);
            }
            if (isParent)
            {
                GUI.DrawTexture(outgoingConnectionTextureRect, connectionBottomTexture, ScaleMode.ScaleToFit);
            }
            GUI.Label(nodeRect, string.Empty, backgroundGUIStyle);
            if (mTask.NodeData.ExecutionStatus == TaskStatus.Success)
            {
                if (mTask.NodeData.IsReevaluating)
                {
                    GUI.DrawTexture(successReevaluatingExecutionStatusTextureRect, BehaviorDesignerUtility.ExecutionSuccessRepeatTexture);
                }
                else
                {
                    GUI.DrawTexture(successExecutionStatusTextureRect, BehaviorDesignerUtility.ExecutionSuccessTexture);
                }
            }
            else if (mTask.NodeData.ExecutionStatus == TaskStatus.Failure)
            {
                GUI.DrawTexture(failureExecutionStatusTextureRect, (!mTask.NodeData.IsReevaluating) ? BehaviorDesignerUtility.ExecutionFailureTexture : BehaviorDesignerUtility.ExecutionFailureRepeatTexture);
            }
            if (!BehaviorDesignerPreferences.GetBool(BDPreferences.CompactMode))
            {
                GUI.DrawTexture(iconBorderTextureRect, iconBorderTexture);
            }
        }

        public void DrawNodeConnection(Vector2 offset, bool disabled)
        {
            if (mConnectionIsDirty)
            {
                DetermineConnectionHorizontalHeight(Rectangle(offset, includeConnections: false, includeComments: false), offset);
                mConnectionIsDirty = false;
            }
            if (isParent)
            {
                for (int i = 0; i < outgoingNodeConnections.Count; i++)
                {
                    outgoingNodeConnections[i].DrawConnection(offset, disabled);
                }
            }
        }

        public void DrawNodeComment(Vector2 offset)
        {
            string nodeComment = GetNodeComment();
            if (nodeComment.Length != prevCommentLength)
            {
                prevCommentLength = nodeComment.Length;
                mRectIsDirty = true;
            }
            if (mTask.NodeData.WatchedFields != null && mTask.NodeData.WatchedFields.Count > 0)
            {
                if (mTask.NodeData.WatchedFields.Count != prevWatchedFieldsLength.Count)
                {
                    mRectIsDirty = true;
                    prevWatchedFieldsLength.Clear();
                    for (int i = 0; i < mTask.NodeData.WatchedFields.Count; i++)
                    {
                        if (!(mTask.NodeData.WatchedFields[i] == null))
                        {
                            object value = mTask.NodeData.WatchedFields[i].GetValue(mTask);
                            if (value != null)
                            {
                                prevWatchedFieldsLength.Add(value.ToString().Length);
                            }
                            else
                            {
                                prevWatchedFieldsLength.Add(0);
                            }
                        }
                    }
                }
                else
                {
                    for (int j = 0; j < mTask.NodeData.WatchedFields.Count; j++)
                    {
                        if (!(mTask.NodeData.WatchedFields[j] == null))
                        {
                            object value2 = mTask.NodeData.WatchedFields[j].GetValue(mTask);
                            int num = 0;
                            if (value2 != null)
                            {
                                num = value2.ToString().Length;
                            }
                            if (num != prevWatchedFieldsLength[j])
                            {
                                mRectIsDirty = true;
                                break;
                            }
                        }
                    }
                }
            }
            if (nodeComment.Equals(string.Empty) && (mTask.NodeData.WatchedFields == null || mTask.NodeData.WatchedFields.Count == 0))
            {
                return;
            }
            if (mTask.NodeData.WatchedFields != null && mTask.NodeData.WatchedFields.Count > 0)
            {
                string text = string.Empty;
                string text2 = string.Empty;
                for (int k = 0; k < mTask.NodeData.WatchedFields.Count; k++)
                {
                    FieldInfo fieldInfo = mTask.NodeData.WatchedFields[k];
                    text = text + BehaviorDesignerUtility.SplitCamelCase(fieldInfo.Name) + ": \n";
                    text2 = text2 + ((fieldInfo.GetValue(mTask) == null) ? "null" : fieldInfo.GetValue(mTask).ToString()) + "\n";
                }
                GUI.Box(watchedFieldRect, string.Empty, BehaviorDesignerUtility.TaskDescriptionGUIStyle);
                GUI.Label(watchedFieldNamesRect, text, BehaviorDesignerUtility.TaskCommentRightAlignGUIStyle);
                GUI.Label(watchedFieldValuesRect, text2, BehaviorDesignerUtility.TaskCommentLeftAlignGUIStyle);
            }
            if (!nodeComment.Equals(string.Empty))
            {
                GUI.Box(commentRect, string.Empty, BehaviorDesignerUtility.TaskDescriptionGUIStyle);
                GUI.Label(commentLabelRect, nodeComment, BehaviorDesignerUtility.TaskCommentGUIStyle);
            }
        }

        public bool Contains(Vector2 point, Vector2 offset, bool includeConnections)
        {
            return Rectangle(offset, includeConnections, includeComments: false).Contains(point);
        }

        public NodeConnection NodeConnectionRectContains(Vector2 point, Vector2 offset)
        {
            bool flag = false;
            if ((flag = IncomingConnectionRect(offset).Contains(point)) || (isParent && OutgoingConnectionRect(offset).Contains(point)))
            {
                return CreateNodeConnection(flag);
            }
            return null;
        }

        public NodeConnection CreateNodeConnection(bool incomingNodeConnection)
        {
            NodeConnection nodeConnection = ScriptableObject.CreateInstance<NodeConnection>();
            nodeConnection.LoadConnection(this, (!incomingNodeConnection) ? NodeConnectionType.Outgoing : NodeConnectionType.Incoming);
            return nodeConnection;
        }

        public void ConnectionContains(Vector2 point, Vector2 offset, ref List<NodeConnection> nodeConnections)
        {
            if (outgoingNodeConnections == null || isEntryDisplay)
            {
                return;
            }
            for (int i = 0; i < outgoingNodeConnections.Count; i++)
            {
                if (outgoingNodeConnections[i].Contains(point, offset))
                {
                    nodeConnections.Add(outgoingNodeConnections[i]);
                }
            }
        }

        private void DetermineConnectionHorizontalHeight(Rect nodeRect, Vector2 offset)
        {
            if (!isParent)
            {
                return;
            }
            float num = float.MaxValue;
            float num2 = num;
            for (int i = 0; i < outgoingNodeConnections.Count; i++)
            {
                Rect rect = outgoingNodeConnections[i].DestinationNodeDesigner.Rectangle(offset, includeConnections: false, includeComments: false);
                if (rect.y < num)
                {
                    num = rect.y;
                    num2 = rect.y;
                }
            }
            num = num * 0.75f + nodeRect.yMax * 0.25f;
            if (num < nodeRect.yMax + 15f)
            {
                num = nodeRect.yMax + 15f;
            }
            else if (num > num2 - 15f)
            {
                num = num2 - 15f;
            }
            for (int j = 0; j < outgoingNodeConnections.Count; j++)
            {
                outgoingNodeConnections[j].HorizontalHeight = num;
            }
        }

        public Vector2 GetConnectionPosition(Vector2 offset, NodeConnectionType connectionType)
        {
            Vector2 result;
            if (connectionType == NodeConnectionType.Incoming)
            {
                Rect rect = IncomingConnectionRect(offset);
                result = new Vector2(rect.center.x, rect.y + 7f);
            }
            else
            {
                Rect rect2 = OutgoingConnectionRect(offset);
                result = new Vector2(rect2.center.x, rect2.yMax - 8f);
            }
            return result;
        }

        public bool HoverBarAreaContains(Vector2 point, Vector2 offset)
        {
            Rect rect = Rectangle(offset, includeConnections: false, includeComments: false);
            rect.y -= 24f;
            return rect.Contains(point);
        }

        public bool HoverBarButtonClick(Vector2 point, Vector2 offset, ref bool collapsedButtonClicked)
        {
            Rect rect = Rectangle(offset, includeConnections: false, includeComments: false);
            Rect rect2 = new Rect(rect.x - 1f, rect.y - 17f, 14f, 14f);
            Rect rect3 = rect2;
            bool flag = false;
            if (rect2.Contains(point))
            {
                mTask.Disabled = !mTask.Disabled;
                flag = true;
            }
            if (!flag && (isParent || mTask is BehaviorReference))
            {
                Rect rect4 = new Rect(rect.x + 15f, rect.y - 17f, 14f, 14f);
                rect3.xMax = rect4.xMax;
                if (rect4.Contains(point))
                {
                    if (mTask is BehaviorReference)
                    {
                        (mTask as BehaviorReference).collapsed = !(mTask as BehaviorReference).collapsed;
                    }
                    else
                    {
                        mTask.NodeData.Collapsed = !mTask.NodeData.Collapsed;
                    }
                    collapsedButtonClicked = true;
                    flag = true;
                }
            }
            if (!flag && rect3.Contains(point))
            {
                flag = true;
            }
            return flag;
        }

        public bool Intersects(Rect rect, Vector2 offset)
        {
            Rect rect2 = Rectangle(offset, includeConnections: false, includeComments: false);
            return rect2.xMin < rect.xMax && rect2.xMax > rect.xMin && rect2.yMin < rect.yMax && rect2.yMax > rect.yMin;
        }

        public void ChangeOffset(Vector2 delta)
        {
            Vector2 offset = mTask.NodeData.Offset;
            offset += delta;
            mTask.NodeData.Offset = offset;
            MarkDirty();
            if (parentNodeDesigner != null)
            {
                parentNodeDesigner.MarkDirty();
            }
        }

        public void AddChildNode(NodeDesigner childNodeDesigner, NodeConnection nodeConnection, bool adjustOffset, bool replaceNode)
        {
            AddChildNode(childNodeDesigner, nodeConnection, adjustOffset, replaceNode, -1);
        }

        public void AddChildNode(NodeDesigner childNodeDesigner, NodeConnection nodeConnection, bool adjustOffset, bool replaceNode, int replaceNodeIndex)
        {
            if (replaceNode)
            {
                ParentTask parentTask = mTask as ParentTask;
                parentTask.Children[replaceNodeIndex] = childNodeDesigner.Task;
            }
            else
            {
                if (!isEntryDisplay)
                {
                    ParentTask parentTask2 = mTask as ParentTask;
                    int i = 0;
                    if (parentTask2.Children != null)
                    {
                        for (i = 0; i < parentTask2.Children.Count && !(childNodeDesigner.GetAbsolutePosition().x < (parentTask2.Children[i].NodeData.NodeDesigner as NodeDesigner).GetAbsolutePosition().x); i++)
                        {
                        }
                    }
                    parentTask2.AddChild(childNodeDesigner.Task, i);
                }
                if (adjustOffset)
                {
                    childNodeDesigner.Task.NodeData.Offset -= GetAbsolutePosition();
                }
            }
            childNodeDesigner.ParentNodeDesigner = this;
            nodeConnection.DestinationNodeDesigner = childNodeDesigner;
            nodeConnection.NodeConnectionType = NodeConnectionType.Fixed;
            if (!nodeConnection.OriginatingNodeDesigner.Equals(this))
            {
                nodeConnection.OriginatingNodeDesigner = this;
            }
            outgoingNodeConnections.Add(nodeConnection);
            mConnectionIsDirty = true;
        }

        public void RemoveChildNode(NodeDesigner childNodeDesigner)
        {
            if (!isEntryDisplay)
            {
                ParentTask parentTask = mTask as ParentTask;
                parentTask.Children.Remove(childNodeDesigner.Task);
            }
            for (int i = 0; i < outgoingNodeConnections.Count; i++)
            {
                NodeConnection nodeConnection = outgoingNodeConnections[i];
                if (nodeConnection.DestinationNodeDesigner.Equals(childNodeDesigner) || nodeConnection.OriginatingNodeDesigner.Equals(childNodeDesigner))
                {
                    outgoingNodeConnections.RemoveAt(i);
                    break;
                }
            }
            childNodeDesigner.ParentNodeDesigner = null;
            mConnectionIsDirty = true;
        }

        public void SetID(ref int id)
        {
            mTask.ID = id++;
            if (!isParent)
            {
                return;
            }
            ParentTask parentTask = mTask as ParentTask;
            if (parentTask.Children != null)
            {
                for (int i = 0; i < parentTask.Children.Count; i++)
                {
                    (parentTask.Children[i].NodeData.NodeDesigner as NodeDesigner).SetID(ref id);
                }
            }
        }

        public int ChildIndexForTask(Task childTask)
        {
            if (isParent)
            {
                ParentTask parentTask = mTask as ParentTask;
                if (parentTask.Children != null)
                {
                    for (int i = 0; i < parentTask.Children.Count; i++)
                    {
                        if (parentTask.Children[i].Equals(childTask))
                        {
                            return i;
                        }
                    }
                }
            }
            return -1;
        }

        public NodeDesigner NodeDesignerForChildIndex(int index)
        {
            if (index < 0)
            {
                return null;
            }
            if (isParent)
            {
                ParentTask parentTask = mTask as ParentTask;
                if (parentTask.Children != null)
                {
                    if (index >= parentTask.Children.Count || parentTask.Children[index] == null)
                    {
                        return null;
                    }
                    return parentTask.Children[index].NodeData.NodeDesigner as NodeDesigner;
                }
            }
            return null;
        }

        public void MoveChildNode(int index, bool decreaseIndex)
        {
            int index2 = index + ((!decreaseIndex) ? 1 : (-1));
            ParentTask parentTask = mTask as ParentTask;
            Task value = parentTask.Children[index];
            parentTask.Children[index] = parentTask.Children[index2];
            parentTask.Children[index2] = value;
        }

        private void BringConnectionToFront(NodeDesigner nodeDesigner)
        {
            for (int i = 0; i < outgoingNodeConnections.Count; i++)
            {
                if (outgoingNodeConnections[i].DestinationNodeDesigner.Equals(nodeDesigner))
                {
                    NodeConnection value = outgoingNodeConnections[i];
                    outgoingNodeConnections[i] = outgoingNodeConnections[outgoingNodeConnections.Count - 1];
                    outgoingNodeConnections[outgoingNodeConnections.Count - 1] = value;
                    break;
                }
            }
        }

        public void ToggleBreakpoint()
        {
            mTask.NodeData.IsBreakpoint = !Task.NodeData.IsBreakpoint;
        }

        public void ToggleEnableState()
        {
            mTask.Disabled = !Task.Disabled;
        }

        public bool IsDisabled()
        {
            if (mTask.Disabled)
            {
                return true;
            }
            if (parentNodeDesigner != null)
            {
                return parentNodeDesigner.IsDisabled();
            }
            return false;
        }

        public bool ToggleCollapseState()
        {
            mTask.NodeData.Collapsed = !Task.NodeData.Collapsed;
            return mTask.NodeData.Collapsed;
        }

        public void IdentifyNode()
        {
            mIdentifyUpdateCount = 0;
        }

        public void FoundTask(bool found)
        {
            mFoundTask = found;
        }

        public bool HasParent(NodeDesigner nodeDesigner)
        {
            if (parentNodeDesigner == null)
            {
                return false;
            }
            if (parentNodeDesigner.Equals(nodeDesigner))
            {
                return true;
            }
            return parentNodeDesigner.HasParent(nodeDesigner);
        }

        public void DestroyConnections()
        {
            if (outgoingNodeConnections != null)
            {
                for (int num = outgoingNodeConnections.Count - 1; num > -1; num--)
                {
                    UnityEngine.Object.DestroyImmediate(outgoingNodeConnections[num], allowDestroyingAssets: true);
                }
            }
        }

        public override bool Equals(object obj)
        {
            return object.ReferenceEquals(this, obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return (mTask == null) ? string.Empty : ((!mTask.FriendlyName.Equals(string.Empty)) ? mTask.FriendlyName : taskName);
        }
    }
}