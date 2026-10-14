using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    [CustomEditor(typeof(ExternalBehavior))]
    public class ExternalBehaviorInspector : UnityEditor.Editor
    {
        private bool mShowVariables;

        private static List<float> variablePosition;

        private static int selectedVariableIndex = -1;

        private static string selectedVariableName;

        private static int selectedVariableTypeIndex;

        public override void OnInspectorGUI()
        {
            ExternalBehavior externalBehavior = base.target as ExternalBehavior;
            if (!(externalBehavior == null))
            {
                if (externalBehavior.BehaviorSource.Owner == null)
                {
                    externalBehavior.BehaviorSource.Owner = externalBehavior;
                }
                if (DrawInspectorGUI(externalBehavior.BehaviorSource, fromInspector: true, ref mShowVariables))
                {
                    BehaviorDesignerUtility.SetObjectDirty(externalBehavior);
                }
            }
        }

        public void Reset()
        {
            ExternalBehavior externalBehavior = base.target as ExternalBehavior;
            if (!(externalBehavior == null) && externalBehavior.BehaviorSource.Owner == null)
            {
                externalBehavior.BehaviorSource.Owner = externalBehavior;
            }
        }

        public static bool DrawInspectorGUI(BehaviorSource behaviorSource, bool fromInspector, ref bool showVariables)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Behavior Name", GUILayout.Width(120f));
            behaviorSource.behaviorName = EditorGUILayout.TextField(behaviorSource.behaviorName);
            if (fromInspector && GUILayout.Button("Open"))
            {
                BehaviorDesignerWindow.ShowWindow();
                BehaviorDesignerWindow.instance.LoadBehavior(behaviorSource, loadPrevBehavior: false, inspectorLoad: true);
            }
            GUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Behavior Description");
            behaviorSource.behaviorDescription = EditorGUILayout.TextArea(behaviorSource.behaviorDescription, GUILayout.Height(48f));
            if (fromInspector)
            {
                string key = "BehaviorDesigner.VariablesFoldout." + behaviorSource.GetHashCode();
                if (showVariables = EditorGUILayout.Foldout(EditorPrefs.GetBool(key, defaultValue: true), "Variables"))
                {
                    EditorGUI.indentLevel++;
                    List<SharedVariable> variables = behaviorSource.GetAllVariables();
                    if (variables != null && VariableInspector.DrawAllVariables(showFooter: false, behaviorSource, ref variables, canSelect: false, ref variablePosition, ref selectedVariableIndex, ref selectedVariableName, ref selectedVariableTypeIndex, drawRemoveButton: true, drawLastSeparator: false))
                    {
                        if (BehaviorDesignerPreferences.GetBool(BDPreferences.BinarySerialization))
                        {
                            BinarySerialization.Save(behaviorSource);
                        }
                        else
                        {
                            JSONSerialization.Save(behaviorSource);
                        }
                        return true;
                    }
                    EditorGUI.indentLevel--;
                }
                EditorPrefs.SetBool(key, showVariables);
            }
            if (EditorGUI.EndChangeCheck())
            {
                return true;
            }
            return false;
        }

        [OnOpenAsset(0)]
        public static bool ClickAction(int instanceID, int line)
        {
            ExternalBehavior externalBehavior = EditorUtility.InstanceIDToObject(instanceID) as ExternalBehavior;
            if (externalBehavior == null)
            {
                return false;
            }
            BehaviorDesignerWindow.ShowWindow();
            BehaviorDesignerWindow.instance.LoadBehavior(externalBehavior.BehaviorSource, loadPrevBehavior: false, inspectorLoad: true);
            return true;
        }
    }
}