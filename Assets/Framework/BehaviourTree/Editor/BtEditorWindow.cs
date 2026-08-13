#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Framework.BehaviourTree.Editor
{
    /// <summary>
    /// 行为树可视化编辑器（IMGUI 节点图，参考 Behavior Designer 工作流）。
    /// 菜单：<c>Tools/Behaviour Tree/Editor</c>。
    /// </summary>
    public sealed class BtEditorWindow : EditorWindow
    {
        const float NodeWidth = 160f;
        const float NodeHeight = 44f;
        const float CanvasSize = 8000f;

        BtTreeAsset _asset;
        string _selectedNodeId;
        Vector2 _graphScroll;
        Vector2 _graphOffset = new Vector2(40f, 40f);
        bool _draggingNode;
        string _dragNodeId;
        Vector2 _dragStartMouse;
        Vector2 _dragStartPos;

        [MenuItem("Tools/Behaviour Tree/Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<BtEditorWindow>("Behaviour Tree");
            window.minSize = new Vector2(900f, 520f);
        }

        [MenuItem("Tools/Behaviour Tree/Create Tree Asset")]
        public static void CreateTreeAssetMenu()
        {
            BtEditorUtility.CreateTreeAsset();
        }

        public static void Open(BtTreeAsset asset)
        {
            var window = GetWindow<BtEditorWindow>("Behaviour Tree");
            window._asset = asset;
            window._selectedNodeId = asset != null ? asset.Definition.RootNodeId : null;
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

            EditorGUILayout.BeginHorizontal();
            DrawGraphArea();
            DrawInspectorPanel();
            EditorGUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var newAsset = (BtTreeAsset)EditorGUILayout.ObjectField(_asset, typeof(BtTreeAsset), false, GUILayout.Width(220f));
            if (newAsset != _asset)
            {
                _asset = newAsset;
                _selectedNodeId = _asset != null ? _asset.Definition.RootNodeId : null;
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
                    EditorUtility.DisplayDialog("Export JSON", $"Exported to:\n{path}", "OK");
                }
            }

            if (GUILayout.Button("Import JSON", EditorStyles.toolbarButton, GUILayout.Width(88f)))
            {
                var picked = EditorUtility.OpenFilePanel("Import Behaviour Tree JSON", "Assets", "json");
                if (!string.IsNullOrEmpty(picked))
                {
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
            if (graphRect.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.ContextClick)
                {
                    ShowAddNodeMenu(WorldToCanvas(evt.mousePosition, graphRect));
                    evt.Use();
                }
            }

            _graphScroll = GUI.BeginScrollView(graphRect, _graphScroll, new Rect(0f, 0f, CanvasSize, CanvasSize));
            var contentOrigin = _graphOffset;

            DrawConnections(contentOrigin);
            DrawNodes(contentOrigin, graphRect);
            HandleNodeDrag(evt, graphRect, contentOrigin);

            GUI.EndScrollView();
        }

        void DrawInspectorPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.3f), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

            if (_asset == null)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            var def = _asset.Definition;
            def.TreeName = EditorGUILayout.TextField("Tree Name", def.TreeName);

            var selected = FindNode(_selectedNodeId);
            if (selected == null)
            {
                EditorGUILayout.HelpBox("Select a node in the graph.", MessageType.None);
                EditorGUILayout.LabelField("Root", def.RootNodeId);
                EditorGUILayout.EndVertical();
                return;
            }

            selected.DisplayName = EditorGUILayout.TextField("Display Name", selected.DisplayName);
            EditorGUILayout.LabelField("Id", selected.Id);
            EditorGUILayout.LabelField("Kind", selected.Kind.ToString());

            DrawKindParams(selected);

            EditorGUILayout.Space(8f);
            var isRoot = def.RootNodeId == selected.Id;
            EditorGUILayout.LabelField("Root Node", isRoot ? "Yes" : "No");
            if (!isRoot && GUILayout.Button("Set As Root"))
            {
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
                        SwapChildren(selected, i, i - 1);
                    }

                    if (GUILayout.Button("↓", GUILayout.Width(24f)) && i < selected.ChildIds.Count - 1)
                    {
                        SwapChildren(selected, i, i + 1);
                    }

                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
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
                if (EditorUtility.DisplayDialog("Delete Node", $"Delete {selected.DisplayName}?", "Delete", "Cancel"))
                {
                    DeleteNode(selected.Id);
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
        }

        void DrawKindParams(BtConfigNode node)
        {
            switch (node.Kind)
            {
                case BtNodeKind.WaitFrames:
                case BtNodeKind.Repeater:
                    node.IntParam = EditorGUILayout.IntField(
                        node.Kind == BtNodeKind.WaitFrames ? "Frames" : "Times (<=0 infinite)",
                        node.IntParam);
                    break;
                case BtNodeKind.WaitTime:
                    node.FloatParam = EditorGUILayout.FloatField("Duration (seconds)", node.FloatParam);
                    break;
                case BtNodeKind.Parallel:
                    node.ParallelPolicy = (BtParallelPolicy)EditorGUILayout.EnumPopup("Policy", node.ParallelPolicy);
                    break;
                case BtNodeKind.BlackboardBool:
                    node.StringParam = EditorGUILayout.TextField("Blackboard Key", node.StringParam);
                    break;
                case BtNodeKind.CustomAction:
                case BtNodeKind.CustomCondition:
                    node.StringParam = EditorGUILayout.TextField("Type Id", node.StringParam);
                    break;
            }
        }

        void DrawConnections(Vector2 origin)
        {
            var def = _asset.Definition;
            Handles.color = new Color(1f, 1f, 1f, 0.35f);
            for (var i = 0; i < def.Nodes.Count; i++)
            {
                var node = def.Nodes[i];
                if (node.ChildIds == null)
                {
                    continue;
                }

                var from = origin + node.EditorPosition + new Vector2(NodeWidth * 0.5f, NodeHeight);
                for (var c = 0; c < node.ChildIds.Count; c++)
                {
                    var child = FindNode(node.ChildIds[c]);
                    if (child == null)
                    {
                        continue;
                    }

                    var to = origin + child.EditorPosition + new Vector2(NodeWidth * 0.5f, 0f);
                    Handles.DrawBezier(from, to, from + Vector2.down * 40f, to + Vector2.up * 40f, Handles.color, null, 2f);
                }
            }
        }

        void DrawNodes(Vector2 origin, Rect graphRect)
        {
            var def = _asset.Definition;
            for (var i = 0; i < def.Nodes.Count; i++)
            {
                var node = def.Nodes[i];
                var rect = new Rect(origin.x + node.EditorPosition.x, origin.y + node.EditorPosition.y, NodeWidth, NodeHeight);
                var isSelected = node.Id == _selectedNodeId;
                var isRoot = node.Id == def.RootNodeId;

                if (!BtNodeCatalog.TryGet(node.Kind, out var meta))
                {
                    meta = new BtNodeCatalogEntry(node.Kind, node.Kind.ToString(), Color.gray, false, false);
                }

                var bg = meta.Color;
                if (isSelected)
                {
                    bg = Color.Lerp(bg, Color.white, 0.35f);
                }

                GUI.color = bg;
                GUI.Box(rect, GUIContent.none);
                GUI.color = Color.white;

                if (isRoot)
                {
                    var rootRect = new Rect(rect.x, rect.y - 16f, rect.width, 14f);
                    GUI.Label(rootRect, "ROOT", EditorStyles.miniBoldLabel);
                }

                var label = string.IsNullOrEmpty(node.DisplayName) ? meta.Label : node.DisplayName;
                GUI.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f), label, EditorStyles.whiteBoldLabel);

                var mouse = MouseInGraphContent(graphRect, origin);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(mouse))
                {
                    _selectedNodeId = node.Id;
                    _draggingNode = true;
                    _dragNodeId = node.Id;
                    _dragStartMouse = Event.current.mousePosition;
                    _dragStartPos = node.EditorPosition;
                    Event.current.Use();
                    Repaint();
                }
            }
        }

        void HandleNodeDrag(Event evt, Rect graphRect, Vector2 origin)
        {
            if (!_draggingNode || evt.type != EventType.MouseDrag || evt.button != 0)
            {
                if (evt.type == EventType.MouseUp)
                {
                    _draggingNode = false;
                }

                return;
            }

            var node = FindNode(_dragNodeId);
            if (node == null)
            {
                _draggingNode = false;
                return;
            }

            var delta = evt.mousePosition - _dragStartMouse;
            node.EditorPosition = _dragStartPos + delta;
            EditorUtility.SetDirty(_asset);
            evt.Use();
            Repaint();
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
                    decorator.ChildIds.Clear();
                    decorator.ChildIds.Add(candidate.Id);
                    EditorUtility.SetDirty(_asset);
                });
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Create New Child Below..."), false, () =>
            {
                var childPos = decorator.EditorPosition + new Vector2(0f, NodeHeight + 80f);
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
            _selectedNodeId = node.Id;
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

        Vector2 MouseInGraphContent(Rect graphRect, Vector2 origin)
        {
            return Event.current.mousePosition - graphRect.position + _graphScroll - origin;
        }

        Vector2 WorldToCanvas(Vector2 mouse, Rect graphRect)
        {
            return mouse - graphRect.position + _graphScroll - _graphOffset;
        }
    }
}
#endif
