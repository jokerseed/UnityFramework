#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Framework.BehaviourTree.Editor
{
    /// <summary><see cref="BtTreeAsset"/> 自定义 Inspector。</summary>
    [CustomEditor(typeof(BtTreeAsset))]
    public sealed class BtTreeAssetEditor : UnityEditor.Editor
    {
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
        }
    }
}
#endif
