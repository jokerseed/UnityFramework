using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    [CustomEditor(typeof(GlobalVariables))]
    public class GlobalVariablesInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Global Variabes"))
            {
                GlobalVariablesWindow.ShowWindow();
            }
        }
    }
}