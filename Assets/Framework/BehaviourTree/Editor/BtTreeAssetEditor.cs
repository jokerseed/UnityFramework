#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Framework.BehaviourTree.Editor
{
    /// <summary><see cref="BtTreeAsset"/> 自定义 Inspector，含常驻 lint。</summary>
    [CustomEditor(typeof(BtTreeAsset))]
    public sealed class BtTreeAssetEditor : UnityEditor.Editor
    {
        readonly List<BtLintMessage> _lint = new List<BtLintMessage>();
        readonly BtEditorSubtreeResolver _subtrees = new BtEditorSubtreeResolver();

        public override void OnInspectorGUI()
        {
            var asset = (BtTreeAsset)target;
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Behaviour Tree", EditorStyles.boldLabel);

            if (GUILayout.Button("Open Graph Editor"))
            {
                BtEditorWindow.Open(asset);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export JSON"))
            {
                var path = BtEditorUtility.ExportJsonNextToAsset(asset);
                if (!string.IsNullOrEmpty(path))
                {
                    EditorUtility.DisplayDialog("Export", path, "OK");
                }
            }

            if (GUILayout.Button("Compile Test"))
            {
                BtEditorUtility.TryCompilePreview(asset);
            }
            EditorGUILayout.EndHorizontal();

            var def = asset.Definition;
            EditorGUILayout.LabelField("Nodes", def.Nodes.Count.ToString());
            EditorGUILayout.LabelField("Root", def.RootNodeId);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Lint", EditorStyles.boldLabel);
            BtTreeValidator.Validate(def, null, _subtrees, _lint);
            if (_lint.Count == 0)
            {
                EditorGUILayout.HelpBox("No issues.", MessageType.Info);
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
            }
        }
    }
}
#endif
