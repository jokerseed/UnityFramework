using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    [CustomEditor(typeof(Behavior))]
    public class BehaviorInspector : UnityEditor.Editor
    {
        private bool mShowOptions = true;

        private bool mShowVariables;

        private static List<float> variablePosition;

        private static int selectedVariableIndex = -1;

        private static string selectedVariableName;

        private static int selectedVariableTypeIndex;

        private void OnEnable()
        {
            Behavior behavior = base.target as Behavior;
            if (behavior == null)
            {
                return;
            }
            GizmoManager.UpdateGizmo(behavior);
            if (Application.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                BehaviorManager.IsPlaying = true;
            }
            behavior.CheckForSerialization(BehaviorDesignerWindow.instance == null && !Application.isPlaying);
            if (Application.isPlaying || !(behavior.ExternalBehavior != null) || !(BehaviorDesignerWindow.instance == null))
            {
                return;
            }
            behavior.ExternalBehavior.BehaviorSource.CheckForSerialization(force: true);
            if (VariableInspector.SyncVariables(behavior.GetBehaviorSource(), behavior.ExternalBehavior.BehaviorSource.GetAllVariables()))
            {
                if (BehaviorDesignerPreferences.GetBool(BDPreferences.BinarySerialization))
                {
                    BinarySerialization.Save(behavior.GetBehaviorSource());
                }
                else
                {
                    JSONSerialization.Save(behavior.GetBehaviorSource());
                }
            }
        }

        public override void OnInspectorGUI()
        {
            Behavior behavior = base.target as Behavior;
            if (behavior == null)
            {
                return;
            }
            bool externalModification = false;
            if (DrawInspectorGUI(behavior, base.serializedObject, fromInspector: true, ref externalModification, ref mShowOptions, ref mShowVariables))
            {
                BehaviorDesignerUtility.SetObjectDirty(behavior);
                if (externalModification && BehaviorDesignerWindow.instance != null && behavior.GetBehaviorSource().BehaviorID == BehaviorDesignerWindow.instance.ActiveBehaviorID)
                {
                    BehaviorDesignerWindow.instance.LoadBehavior(behavior.GetBehaviorSource(), loadPrevBehavior: false, inspectorLoad: false);
                }
            }
        }

        public static bool DrawInspectorGUI(Behavior behavior, SerializedObject serializedObject, bool fromInspector, ref bool externalModification, ref bool showOptions, ref bool showVariables)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.Space(3f);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Behavior Name", GUILayout.Width(120f));
            behavior.GetBehaviorSource().behaviorName = EditorGUILayout.TextField(behavior.GetBehaviorSource().behaviorName);
            if (fromInspector && GUILayout.Button("Open"))
            {
                BehaviorDesignerWindow.ShowWindow();
                BehaviorDesignerWindow.instance.LoadBehavior(behavior.GetBehaviorSource(), loadPrevBehavior: false, inspectorLoad: true);
            }
            GUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Behavior Description");
            behavior.GetBehaviorSource().behaviorDescription = EditorGUILayout.TextArea(behavior.GetBehaviorSource().behaviorDescription, BehaviorDesignerUtility.TaskInspectorCommentGUIStyle, GUILayout.Height(48f));
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            GUI.enabled = BehaviorDesignerPreferences.GetBool(BDPreferences.EditablePrefabInstances) || (PrefabUtility.GetPrefabAssetType(behavior) != PrefabAssetType.Regular && PrefabUtility.GetPrefabAssetType(behavior) != PrefabAssetType.Variant);
            SerializedProperty serializedProperty = serializedObject.FindProperty("externalBehavior");
            ExternalBehavior externalBehavior = serializedProperty.objectReferenceValue as ExternalBehavior;
            EditorGUILayout.PropertyField(serializedProperty, true);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
            if ((!object.ReferenceEquals(behavior.ExternalBehavior, null) && !behavior.ExternalBehavior.Equals(externalBehavior)) || (!object.ReferenceEquals(externalBehavior, null) && !externalBehavior.Equals(behavior.ExternalBehavior)))
            {
                if (!object.ReferenceEquals(behavior.ExternalBehavior, null))
                {
                    behavior.ExternalBehavior.BehaviorSource.Owner = behavior.ExternalBehavior;
                    behavior.ExternalBehavior.BehaviorSource.CheckForSerialization(force: true, behavior.GetBehaviorSource());
                }
                else
                {
                    behavior.GetBehaviorSource().EntryTask = null;
                    behavior.GetBehaviorSource().RootTask = null;
                    behavior.GetBehaviorSource().DetachedTasks = null;
                    behavior.GetBehaviorSource().Variables = null;
                    behavior.GetBehaviorSource().CheckForSerialization(force: true);
                    behavior.GetBehaviorSource().Variables = null;
                    if (BehaviorDesignerPreferences.GetBool(BDPreferences.BinarySerialization))
                    {
                        BinarySerialization.Save(behavior.GetBehaviorSource());
                    }
                    else
                    {
                        JSONSerialization.Save(behavior.GetBehaviorSource());
                    }
                }
                externalModification = true;
            }
            GUI.enabled = true;
            serializedProperty = serializedObject.FindProperty("group");
            EditorGUILayout.PropertyField(serializedProperty, true);
            string key;
            if (fromInspector)
            {
                key = "BehaviorDesigner.VariablesFoldout." + behavior.GetHashCode();
                if (showVariables = EditorGUILayout.Foldout(EditorPrefs.GetBool(key, defaultValue: true), "Variables"))
                {
                    EditorGUI.indentLevel++;
                    bool flag = false;
                    BehaviorSource behaviorSource = behavior.GetBehaviorSource();
                    List<SharedVariable> variables = behaviorSource.GetAllVariables();
                    if (variables != null && variables.Count > 0)
                    {
                        if (VariableInspector.DrawAllVariables(showFooter: false, behaviorSource, ref variables, canSelect: false, ref variablePosition, ref selectedVariableIndex, ref selectedVariableName, ref selectedVariableTypeIndex, drawRemoveButton: false, drawLastSeparator: true))
                        {
                            if (!EditorApplication.isPlayingOrWillChangePlaymode && behavior.ExternalBehavior != null)
                            {
                                BehaviorSource behaviorSource2 = behavior.ExternalBehavior.GetBehaviorSource();
                                behaviorSource2.CheckForSerialization(force: true);
                                if (VariableInspector.SyncVariables(behaviorSource2, variables))
                                {
                                    if (BehaviorDesignerPreferences.GetBool(BDPreferences.BinarySerialization))
                                    {
                                        BinarySerialization.Save(behaviorSource2);
                                    }
                                    else
                                    {
                                        JSONSerialization.Save(behaviorSource2);
                                    }
                                }
                            }
                            flag = true;
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("There are no variables to display");
                    }
                    if (flag)
                    {
                        if (BehaviorDesignerPreferences.GetBool(BDPreferences.BinarySerialization))
                        {
                            BinarySerialization.Save(behaviorSource);
                        }
                        else
                        {
                            JSONSerialization.Save(behaviorSource);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                EditorPrefs.SetBool(key, showVariables);
            }
            key = "BehaviorDesigner.OptionsFoldout." + behavior.GetHashCode();
            if (!fromInspector || (showOptions = EditorGUILayout.Foldout(EditorPrefs.GetBool(key, defaultValue: true), "Options")))
            {
                if (fromInspector)
                {
                    EditorGUI.indentLevel++;
                }
                serializedProperty = serializedObject.FindProperty("startWhenEnabled");
                EditorGUILayout.PropertyField(serializedProperty, true);
                serializedProperty = serializedObject.FindProperty("asynchronousLoad");
                EditorGUILayout.PropertyField(serializedProperty, true);
                serializedProperty = serializedObject.FindProperty("pauseWhenDisabled");
                EditorGUILayout.PropertyField(serializedProperty, true);
                serializedProperty = serializedObject.FindProperty("restartWhenComplete");
                EditorGUILayout.PropertyField(serializedProperty, true);
                serializedProperty = serializedObject.FindProperty("resetValuesOnRestart");
                EditorGUILayout.PropertyField(serializedProperty, true);
                serializedProperty = serializedObject.FindProperty("logTaskChanges");
                EditorGUILayout.PropertyField(serializedProperty, true);
                if (fromInspector)
                {
                    EditorGUI.indentLevel--;
                }
            }
            if (fromInspector)
            {
                EditorPrefs.SetBool(key, showOptions);
            }
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                return true;
            }
            return false;
        }
    }
}