#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Framework.BehaviourTree.Editor
{
    /// <summary>
    /// 行为树可视化编辑器（IMGUI 节点图）。
    /// 菜单：<c>Tools/Behaviour Tree/Editor</c>。
    /// </summary>
    public sealed class BtEditorWindow : EditorWindow
    {
        const float NodeWidth = 160f;
        const float NodeHeight = 44f;
        const float CanvasSize = 8000f;

        BtTreeAsset _asset;
        string _selectedNodeId;
        readonly HashSet<string> _selectedIds = new HashSet<string>();
        Vector2 _graphScroll;
        Vector2 _graphOffset = new Vector2(40f, 40f);
        bool _draggingNode;
        bool _marquee;
        Vector2 _marqueeStart;
        Vector2 _marqueeEnd;
        Vector2 _dragStartMouse;
        readonly Dictionary<string, Vector2> _dragStartPositions = new Dictionary<string, Vector2>();
        readonly List<BtLintMessage> _lint = new List<BtLintMessage>();
        readonly List<string> _bbKeys = new List<string>();
        readonly BtEditorSubtreeResolver _subtrees = new BtEditorSubtreeResolver();
        Vector2 _inspectorScroll;

        /// <summary>打开行为树图编辑器。</summary>
        [MenuItem("Tools/Behaviour Tree/Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<BtEditorWindow>("Behaviour Tree");
            window.minSize = new Vector2(900f, 520f);
        }

        /// <summary>创建新的行为树资产。</summary>
        [MenuItem("Tools/Behaviour Tree/Create Tree Asset")]
        public static void CreateTreeAssetMenu()
        {
            BtEditorUtility.CreateTreeAsset();
        }

        /// <summary>批量导出全部 BtTreeAsset 旁路 .bt.json（供 YooAsset 热更）。</summary>
        [MenuItem("Tools/Behaviour Tree/Export All Runtime JSON")]
        public static void ExportAllRuntimeJsonMenu()
        {
            var count = BtEditorUtility.ExportAllRuntimeJson();
            EditorUtility.DisplayDialog(
                "Export All Runtime JSON",
                count > 0
                    ? $"Exported {count} behaviour tree JSON file(s)."
                    : "No BtTreeAsset found in the project.",
                "OK");
        }

        /// <summary>打开指定资产。</summary>
        /// <param name="asset">行为树资产；可为 null。</param>
        public static void Open(BtTreeAsset asset)
        {
            var window = GetWindow<BtEditorWindow>("Behaviour Tree");
            window._asset = asset;
            window._selectedNodeId = asset != null ? asset.Definition.RootNodeId : null;
        }

        void OnEnable()
        {
            EditorApplication.update += RepaintIfPlaying;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            EditorApplication.update -= RepaintIfPlaying;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                BtDebugHub.Clear();
            }
        }

        void RepaintIfPlaying()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            var frame = BtDebugHub.LastFrame;
            if (frame != null && frame.BreakpointNodeIndex >= 0 && !EditorApplication.isPaused)
            {
                EditorApplication.isPaused = true;
            }

            Repaint();
        }

        void OnGUI()
        {
            DrawToolbar();

            if (_asset == null)
            {
                EditorGUILayout.HelpBox("Assign a BtTreeAsset or create one via Tools/Behaviour Tree/Create Tree Asset.", MessageType.Info);
                _asset = (BtTreeAsset)EditorGUILayout.ObjectField("Tree Asset", _asset, typeof(BtTreeAsset), false);
                return;
            }

            RefreshLint();
            EditorGUILayout.BeginHorizontal();
            DrawGraphArea();
            DrawInspectorPanel();
            EditorGUILayout.EndHorizontal();
        }

        void Record(string undoName)
        {
            if (_asset != null)
            {
                Undo.RecordObject(_asset, undoName);
            }
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var newAsset = (BtTreeAsset)EditorGUILayout.ObjectField(_asset, typeof(BtTreeAsset), false, GUILayout.Width(220f));
            if (newAsset != _asset)
            {
                _asset = newAsset;
                _selectedNodeId = _asset != null ? _asset.Definition.RootNodeId : null;
                _selectedIds.Clear();
            }

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                _asset = BtEditorUtility.CreateTreeAsset();
                _selectedNodeId = _asset.Definition.RootNodeId;
            }

            GUI.enabled = _asset != null;
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                EditorUtility.SetDirty(_asset);
                AssetDatabase.SaveAssets();
            }

            if (GUILayout.Button("Export JSON", EditorStyles.toolbarButton, GUILayout.Width(88f)))
            {
                var path = BtEditorUtility.ExportJsonNextToAsset(_asset);
                if (!string.IsNullOrEmpty(path))
                {
                    EditorUtility.DisplayDialog("Export JSON", "Exported to:\n" + path, "OK");
                }
            }

            if (GUILayout.Button("Import JSON", EditorStyles.toolbarButton, GUILayout.Width(88f)))
            {
                var picked = EditorUtility.OpenFilePanel("Import Behaviour Tree JSON", "Assets", "json");
                if (!string.IsNullOrEmpty(picked))
                {
                    Record("Import BT JSON");
                    BtEditorUtility.ImportJsonToAsset(_asset, picked);
                    _selectedNodeId = _asset.Definition.RootNodeId;
                }
            }

            if (GUILayout.Button("Compile Test", EditorStyles.toolbarButton, GUILayout.Width(88f)))
            {
                if (BtEditorUtility.TryCompilePreview(_asset))
                {
                    ShowNotification(new GUIContent("Compile OK"));
                }
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        void DrawGraphArea()
        {
            var graphRect = GUILayoutUtility.GetRect(position.width * 0.68f, position.height - 40f, GUILayout.ExpandHeight(true));
            GUI.Box(graphRect, GUIContent.none);

            var evt = Event.current;
            if (graphRect.Contains(evt.mousePosition) && evt.type == EventType.ContextClick)
            {
                ShowAddNodeMenu(WorldToCanvas(evt.mousePosition, graphRect));
                evt.Use();
            }

            _graphScroll = GUI.BeginScrollView(graphRect, _graphScroll, new Rect(0f, 0f, CanvasSize, CanvasSize));
            var contentOrigin = _graphOffset;

            DrawConnections(contentOrigin);
            DrawNodes(contentOrigin);
            HandleMarquee(evt, contentOrigin);
            HandleNodeDrag(evt);

            GUI.EndScrollView();
        }

        void DrawInspectorPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.3f), GUILayout.ExpandHeight(true));
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

            if (_asset == null)
            {
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            var def = _asset.Definition;
            EditorGUI.BeginChangeCheck();
            var treeName = EditorGUILayout.TextField("Tree Name", def.TreeName);
            if (EditorGUI.EndChangeCheck())
            {
                Record("Rename Tree");
                def.TreeName = treeName;
            }

            DrawLintPanel();
            DrawBlackboardWatch();

            var selected = FindNode(_selectedNodeId);
            if (selected == null)
            {
                EditorGUILayout.HelpBox("Select a node in the graph.", MessageType.None);
                EditorGUILayout.LabelField("Root", def.RootNodeId);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.BeginChangeCheck();
            var display = EditorGUILayout.TextField("Display Name", selected.DisplayName);
            if (EditorGUI.EndChangeCheck())
            {
                Record("Rename Node");
                selected.DisplayName = display;
            }

            EditorGUILayout.LabelField("Id", selected.Id);
            EditorGUILayout.LabelField("Kind", selected.Kind.ToString());

            EditorGUI.BeginChangeCheck();
            var abort = (BtAbortType)EditorGUILayout.EnumPopup("Abort Type", selected.AbortType);
            var bp = EditorGUILayout.Toggle("Breakpoint", selected.Breakpoint);
            if (EditorGUI.EndChangeCheck())
            {
                Record("Edit Node Flags");
                selected.AbortType = abort;
                selected.Breakpoint = bp;
            }

            DrawKindParams(selected);

            EditorGUILayout.Space(8f);
            var isRoot = def.RootNodeId == selected.Id;
            EditorGUILayout.LabelField("Root Node", isRoot ? "Yes" : "No");
            if (!isRoot && GUILayout.Button("Set As Root"))
            {
                Record("Set Root");
                def.RootNodeId = selected.Id;
                EditorUtility.SetDirty(_asset);
            }

            if (BtNodeCatalog.TryGet(selected.Kind, out var meta) && meta.IsComposite)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Children", EditorStyles.boldLabel);
                for (var i = 0; i < selected.ChildIds.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    var child = FindNode(selected.ChildIds[i]);
                    EditorGUILayout.LabelField(child != null ? child.DisplayName : selected.ChildIds[i]);
                    if (GUILayout.Button("↑", GUILayout.Width(24f)) && i > 0)
                    {
                        Record("Reorder Child");
                        SwapChildren(selected, i, i - 1);
                    }

                    if (GUILayout.Button("↓", GUILayout.Width(24f)) && i < selected.ChildIds.Count - 1)
                    {
                        Record("Reorder Child");
                        SwapChildren(selected, i, i + 1);
                    }

                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        Record("Remove Child");
                        selected.ChildIds.RemoveAt(i);
                        EditorUtility.SetDirty(_asset);
                        break;
                    }

                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Add Child Node..."))
                {
                    ShowAddChildMenu(selected);
                }
            }
            else if (BtNodeCatalog.TryGet(selected.Kind, out meta) && meta.IsDecorator)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Child", EditorStyles.boldLabel);
                var childId = selected.ChildIds.Count > 0 ? selected.ChildIds[0] : string.Empty;
                var child = FindNode(childId);
                EditorGUILayout.LabelField(child != null ? child.DisplayName : "(none)");
                if (GUILayout.Button("Assign Child..."))
                {
                    ShowAssignDecoratorChildMenu(selected);
                }
            }

            EditorGUILayout.Space(8f);
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Delete Node"))
            {
                if (EditorUtility.DisplayDialog("Delete Node", "Delete " + selected.DisplayName + "?", "Delete", "Cancel"))
                {
                    Record("Delete Node");
                    DeleteNode(selected.Id);
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawLintPanel()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Lint", EditorStyles.boldLabel);
            if (_lint.Count == 0)
            {
                EditorGUILayout.LabelField("No issues.");
                return;
            }

            for (var i = 0; i < _lint.Count; i++)
            {
                var msg = _lint[i];
                var type = msg.Severity == BtLintSeverity.Error
                    ? MessageType.Error
                    : msg.Severity == BtLintSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;
                EditorGUILayout.HelpBox(msg.Message, type);
                if (!string.IsNullOrEmpty(msg.NodeId) && GUILayout.Button("Select", GUILayout.Width(60f)))
                {
                    SelectOnly(msg.NodeId);
                }
            }
        }

        void DrawBlackboardWatch()
        {
            if (!EditorApplication.isPlaying || BtDebugHub.LastContext == null)
            {
                return;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Blackboard (debug)", EditorStyles.boldLabel);
            BtDebugHub.LastContext.Blackboard.CopyDebugKeys(_bbKeys);
            if (_bbKeys.Count == 0)
            {
                EditorGUILayout.LabelField("(empty)");
                return;
            }

            for (var i = 0; i < _bbKeys.Count; i++)
            {
                EditorGUILayout.LabelField(_bbKeys[i]);
            }
        }

        void DrawKindParams(BtConfigNode node)
        {
            var durationKind = node.Kind == BtNodeKind.WaitTime
                || node.Kind == BtNodeKind.Cooldown
                || node.Kind == BtNodeKind.Timeout
                || node.Kind == BtNodeKind.TimeLimit;
            var seconds = node.FloatParam;
            var typeId = string.IsNullOrEmpty(node.TypeId) ? node.StringParam : node.TypeId;
            var subtreeId = string.IsNullOrEmpty(node.SubtreeId) ? node.StringParam : node.SubtreeId;

            EditorGUI.BeginChangeCheck();
            switch (node.Kind)
            {
                case BtNodeKind.WaitFrames:
                    node.IntParam = EditorGUILayout.IntField("Frames", node.IntParam);
                    break;
                case BtNodeKind.Repeater:
                    node.IntParam = EditorGUILayout.IntField("Times (<=0 infinite)", node.IntParam);
                    node.RepeatOnFailure = EditorGUILayout.Toggle("Repeat On Failure", node.RepeatOnFailure);
                    break;
                case BtNodeKind.WaitTime:
                case BtNodeKind.Cooldown:
                case BtNodeKind.Timeout:
                case BtNodeKind.TimeLimit:
                    seconds = EditorGUILayout.FloatField("Duration (seconds)", node.FloatParam);
                    break;
                case BtNodeKind.Parallel:
                    node.ParallelPolicy = (BtParallelPolicy)EditorGUILayout.EnumPopup("Policy", node.ParallelPolicy);
                    node.FailFast = EditorGUILayout.Toggle("Fail Fast", node.FailFast);
                    node.SucceedFast = EditorGUILayout.Toggle("Succeed Fast", node.SucceedFast);
                    break;
                case BtNodeKind.BlackboardBool:
                    node.StringParam = EditorGUILayout.TextField("Blackboard Key", node.StringParam);
                    break;
                case BtNodeKind.CustomAction:
                case BtNodeKind.CustomCondition:
                    typeId = EditorGUILayout.TextField("Type Id", typeId);
                    break;
                case BtNodeKind.Subtree:
                    subtreeId = EditorGUILayout.TextField("Subtree Id", subtreeId);
                    break;
                case BtNodeKind.WeightedSelector:
                    EditorGUILayout.LabelField("Weights (one per child)");
                    EnsureWeightCount(node);
                    for (var i = 0; i < node.Weights.Count; i++)
                    {
                        node.Weights[i] = EditorGUILayout.IntField("W" + i, node.Weights[i]);
                    }

                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                Record("Edit Node Param");
                if (durationKind)
                {
                    node.SetDurationSeconds(seconds);
                }
                else if (node.Kind == BtNodeKind.CustomAction || node.Kind == BtNodeKind.CustomCondition)
                {
                    node.TypeId = typeId;
                    node.StringParam = typeId;
                }
                else if (node.Kind == BtNodeKind.Subtree)
                {
                    node.SubtreeId = subtreeId;
                    node.StringParam = subtreeId;
                }

                EditorUtility.SetDirty(_asset);
            }
        }

        static void EnsureWeightCount(BtConfigNode node)
        {
            if (node.Weights == null)
            {
                node.Weights = new List<int>();
            }

            while (node.Weights.Count < node.ChildIds.Count)
            {
                node.Weights.Add(1);
            }
        }

        void DrawConnections(Vector2 origin)
        {
            var def = _asset.Definition;
            for (var i = 0; i < def.Nodes.Count; i++)
            {
                var node = def.Nodes[i];
                if (node.ChildIds == null)
                {
                    continue;
                }

                var from = origin + node.EditorPosition + new Vector2(NodeWidth * 0.5f, NodeHeight);
                var catalogOk = BtNodeCatalog.TryGet(node.Kind, out var meta);
                var maxChildren = catalogOk && meta.IsDecorator ? 1 : int.MaxValue;
                for (var c = 0; c < node.ChildIds.Count; c++)
                {
                    var childId = node.ChildIds[c];
                    var child = FindNode(childId);
                    var invalid = child == null || child.Id == node.Id || c >= maxChildren;
                    var color = invalid ? Color.red : new Color(1f, 1f, 1f, 0.35f);
                    Vector2 to;
                    if (child != null)
                    {
                        to = origin + child.EditorPosition + new Vector2(NodeWidth * 0.5f, 0f);
                    }
                    else
                    {
                        to = from + new Vector2(40f + c * 12f, 60f);
                    }

                    Handles.DrawBezier(from, to, from + Vector2.down * 40f, to + Vector2.up * 40f, color, null, invalid ? 3f : 2f);
                }
            }
        }

        void DrawNodes(Vector2 origin)
        {
            var def = _asset.Definition;
            BtDebugFrame debug = null;
            BehaviourTree tree = null;
            if (EditorApplication.isPlaying)
            {
                tree = BtDebugHub.LastTree;
                if (tree != null
                    && (string.IsNullOrEmpty(_asset.Definition.TreeName) || tree.Name == _asset.Definition.TreeName))
                {
                    debug = BtDebugHub.LastFrame;
                }
                else
                {
                    tree = null;
                }
            }
            for (var i = 0; i < def.Nodes.Count; i++)
            {
                var node = def.Nodes[i];
                var rect = new Rect(origin.x + node.EditorPosition.x, origin.y + node.EditorPosition.y, NodeWidth, NodeHeight);
                var isSelected = _selectedIds.Contains(node.Id) || node.Id == _selectedNodeId;
                var isRoot = node.Id == def.RootNodeId;

                if (!BtNodeCatalog.TryGet(node.Kind, out var meta))
                {
                    meta = new BtNodeCatalogEntry(node.Kind, node.Kind.ToString(), Color.gray, false, false);
                }

                var bg = meta.Color;
                if (debug != null && tree != null)
                {
                    var idx = tree.Template.FindIndexByConfigId(node.Id);
                    if (idx >= 0 && idx < debug.Statuses.Length)
                    {
                        switch (debug.Statuses[idx])
                        {
                            case BtStatus.Success:
                                bg = new Color(0.2f, 0.75f, 0.3f);
                                break;
                            case BtStatus.Failure:
                                bg = new Color(0.85f, 0.25f, 0.25f);
                                break;
                            case BtStatus.Running:
                                bg = new Color(0.95f, 0.8f, 0.2f);
                                break;
                        }
                    }
                }

                if (isSelected)
                {
                    bg = Color.Lerp(bg, Color.white, 0.35f);
                }

                GUI.color = bg;
                GUI.Box(rect, GUIContent.none);
                GUI.color = Color.white;

                if (isRoot)
                {
                    GUI.Label(new Rect(rect.x, rect.y - 16f, rect.width, 14f), "ROOT", EditorStyles.miniBoldLabel);
                }

                if (node.Breakpoint)
                {
                    GUI.Label(new Rect(rect.xMax - 18f, rect.y - 14f, 18f, 14f), "BP", EditorStyles.miniBoldLabel);
                }

                var label = string.IsNullOrEmpty(node.DisplayName) ? meta.Label : node.DisplayName;
                GUI.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f), label, EditorStyles.whiteBoldLabel);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
                {
                    var additive = Event.current.control || Event.current.shift;
                    if (additive)
                    {
                        if (!_selectedIds.Add(node.Id))
                        {
                            _selectedIds.Remove(node.Id);
                        }

                        _selectedNodeId = node.Id;
                    }
                    else
                    {
                        SelectOnly(node.Id);
                    }

                    BeginDrag(node);
                    Event.current.Use();
                    Repaint();
                }
            }
        }

        void BeginDrag(BtConfigNode node)
        {
            Record("Move Nodes");
            _draggingNode = true;
            _dragStartMouse = Event.current.mousePosition;
            _dragStartPositions.Clear();
            if (_selectedIds.Count == 0)
            {
                _selectedIds.Add(node.Id);
            }

            foreach (var id in _selectedIds)
            {
                var n = FindNode(id);
                if (n != null)
                {
                    _dragStartPositions[id] = n.EditorPosition;
                }
            }
        }

        void HandleMarquee(Event evt, Vector2 origin)
        {
            var mouse = evt.mousePosition;
            if (evt.type == EventType.MouseDown && evt.button == 0 && !HitAnyNode(mouse, origin))
            {
                _marquee = true;
                _marqueeStart = mouse;
                _marqueeEnd = mouse;
                if (!evt.control && !evt.shift)
                {
                    _selectedIds.Clear();
                    _selectedNodeId = null;
                }

                evt.Use();
            }

            if (_marquee && evt.type == EventType.MouseDrag)
            {
                _marqueeEnd = mouse;
                evt.Use();
                Repaint();
            }

            if (_marquee && evt.type == EventType.MouseUp)
            {
                var r = RectFrom(_marqueeStart, _marqueeEnd);
                var def = _asset.Definition;
                for (var i = 0; i < def.Nodes.Count; i++)
                {
                    var node = def.Nodes[i];
                    var nr = new Rect(origin.x + node.EditorPosition.x, origin.y + node.EditorPosition.y, NodeWidth, NodeHeight);
                    if (nr.Overlaps(r))
                    {
                        _selectedIds.Add(node.Id);
                        _selectedNodeId = node.Id;
                    }
                }

                _marquee = false;
                evt.Use();
                Repaint();
            }

            if (_marquee)
            {
                var r = RectFrom(_marqueeStart, _marqueeEnd);
                Handles.DrawSolidRectangleWithOutline(
                    r,
                    new Color(0.3f, 0.6f, 1f, 0.12f),
                    new Color(0.3f, 0.6f, 1f, 0.8f));
            }
        }

        bool HitAnyNode(Vector2 mouse, Vector2 origin)
        {
            var def = _asset.Definition;
            for (var i = 0; i < def.Nodes.Count; i++)
            {
                var node = def.Nodes[i];
                var rect = new Rect(origin.x + node.EditorPosition.x, origin.y + node.EditorPosition.y, NodeWidth, NodeHeight);
                if (rect.Contains(mouse))
                {
                    return true;
                }
            }

            return false;
        }

        static Rect RectFrom(Vector2 a, Vector2 b)
        {
            var x = Mathf.Min(a.x, b.x);
            var y = Mathf.Min(a.y, b.y);
            return new Rect(x, y, Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        void HandleNodeDrag(Event evt)
        {
            if (!_draggingNode)
            {
                return;
            }

            if (evt.type == EventType.MouseUp)
            {
                _draggingNode = false;
                return;
            }

            if (evt.type != EventType.MouseDrag || evt.button != 0)
            {
                return;
            }

            var delta = evt.mousePosition - _dragStartMouse;
            foreach (var pair in _dragStartPositions)
            {
                var node = FindNode(pair.Key);
                if (node != null)
                {
                    node.EditorPosition = pair.Value + delta;
                }
            }

            EditorUtility.SetDirty(_asset);
            evt.Use();
            Repaint();
        }

        void RefreshLint()
        {
            BtTreeValidator.Validate(_asset.Definition, null, _subtrees, _lint);
        }

        void SelectOnly(string id)
        {
            _selectedIds.Clear();
            _selectedIds.Add(id);
            _selectedNodeId = id;
        }

        void ShowAddNodeMenu(Vector2 canvasPos)
        {
            var menu = new GenericMenu();
            for (var i = 0; i < BtNodeCatalog.Entries.Count; i++)
            {
                var entry = BtNodeCatalog.Entries[i];
                menu.AddItem(new GUIContent(entry.Label), false, () => AddNode(entry.Kind, canvasPos));
            }

            menu.ShowAsContext();
        }

        void ShowAddChildMenu(BtConfigNode parent)
        {
            var menu = new GenericMenu();
            for (var i = 0; i < BtNodeCatalog.Entries.Count; i++)
            {
                var entry = BtNodeCatalog.Entries[i];
                menu.AddItem(new GUIContent(entry.Label), false, () =>
                {
                    var childPos = parent.EditorPosition + new Vector2(0f, NodeHeight + 80f + parent.ChildIds.Count * (NodeHeight + 24f));
                    Record("Add Child");
                    var child = AddNode(entry.Kind, childPos);
                    parent.ChildIds.Add(child.Id);
                    EditorUtility.SetDirty(_asset);
                });
            }

            menu.ShowAsContext();
        }

        void ShowAssignDecoratorChildMenu(BtConfigNode decorator)
        {
            var menu = new GenericMenu();
            var def = _asset.Definition;
            for (var i = 0; i < def.Nodes.Count; i++)
            {
                var candidate = def.Nodes[i];
                if (candidate.Id == decorator.Id)
                {
                    continue;
                }

                menu.AddItem(new GUIContent(candidate.DisplayName + " (" + candidate.Kind + ")"), false, () =>
                {
                    Record("Assign Child");
                    decorator.ChildIds.Clear();
                    decorator.ChildIds.Add(candidate.Id);
                    EditorUtility.SetDirty(_asset);
                });
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Create New Child Below..."), false, () =>
            {
                var childPos = decorator.EditorPosition + new Vector2(0f, NodeHeight + 80f);
                Record("Create Decorator Child");
                var child = AddNode(BtNodeKind.Sequence, childPos);
                decorator.ChildIds.Clear();
                decorator.ChildIds.Add(child.Id);
                EditorUtility.SetDirty(_asset);
            });

            menu.ShowAsContext();
        }

        BtConfigNode AddNode(BtNodeKind kind, Vector2 canvasPos)
        {
            BtNodeCatalog.TryGet(kind, out var meta);
            Record("Add Node");
            var node = new BtConfigNode
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = kind,
                DisplayName = meta.Label,
                EditorPosition = canvasPos,
            };

            if (kind == BtNodeKind.Repeater)
            {
                node.IntParam = 1;
            }

            _asset.Definition.Nodes.Add(node);
            SelectOnly(node.Id);
            EditorUtility.SetDirty(_asset);
            Repaint();
            return node;
        }

        void DeleteNode(string nodeId)
        {
            var def = _asset.Definition;
            if (def.RootNodeId == nodeId)
            {
                EditorUtility.DisplayDialog("Delete Node", "Cannot delete root. Set another root first.", "OK");
                return;
            }

            for (var i = 0; i < def.Nodes.Count; i++)
            {
                def.Nodes[i].ChildIds?.RemoveAll(id => id == nodeId);
            }

            def.Nodes.RemoveAll(n => n.Id == nodeId);
            _selectedIds.Remove(nodeId);
            if (_selectedNodeId == nodeId)
            {
                _selectedNodeId = def.RootNodeId;
            }

            EditorUtility.SetDirty(_asset);
            Repaint();
        }

        BtConfigNode FindNode(string id)
        {
            if (_asset == null || string.IsNullOrEmpty(id))
            {
                return null;
            }

            var nodes = _asset.Definition.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Id == id)
                {
                    return nodes[i];
                }
            }

            return null;
        }

        static void SwapChildren(BtConfigNode parent, int a, int b)
        {
            var tmp = parent.ChildIds[a];
            parent.ChildIds[a] = parent.ChildIds[b];
            parent.ChildIds[b] = tmp;
        }

        Vector2 WorldToCanvas(Vector2 mouse, Rect graphRect)
        {
            return mouse - graphRect.position + _graphScroll - _graphOffset;
        }
    }
}
#endif
