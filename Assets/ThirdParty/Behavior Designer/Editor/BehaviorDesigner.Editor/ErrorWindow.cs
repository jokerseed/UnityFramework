using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    public class ErrorWindow : EditorWindow
    {
        private List<ErrorDetails> mErrorDetails;

        private Vector2 mScrollPosition;

        public static ErrorWindow instance;

        public List<ErrorDetails> ErrorDetails
        {
            set
            {
                mErrorDetails = value;
            }
        }

        [MenuItem("Tools/Behavior Designer/Error List", false, 2)]
        public static void ShowWindow()
        {
            ErrorWindow window = EditorWindow.GetWindow<ErrorWindow>(utility: false, "Error List");
            window.minSize = new Vector2(400f, 200f);
            window.wantsMouseMove = true;
        }

        public void OnFocus()
        {
            instance = this;
            if (BehaviorDesignerWindow.instance != null)
            {
                mErrorDetails = BehaviorDesignerWindow.instance.ErrorDetails;
            }
        }

        public void OnGUI()
        {
            mScrollPosition = EditorGUILayout.BeginScrollView(mScrollPosition);
            if (mErrorDetails != null && mErrorDetails.Count > 0)
            {
                for (int i = 0; i < mErrorDetails.Count; i++)
                {
                    ErrorDetails errorDetails = mErrorDetails[i];
                    if (errorDetails != null && (errorDetails.Type == BehaviorDesigner.Editor.ErrorDetails.ErrorType.InvalidVariableReference || (!(errorDetails.NodeDesigner == null) && errorDetails.NodeDesigner.Task != null)))
                    {
                        string label = string.Empty;
                        switch (errorDetails.Type)
                        {
                            case BehaviorDesigner.Editor.ErrorDetails.ErrorType.MissingChildren:
                                label = $"The {errorDetails.TaskFriendlyName} task ({errorDetails.TaskType}, index {errorDetails.NodeDesigner.Task.ID}) is a parent task which does not have any children";
                                break;
                            case BehaviorDesigner.Editor.ErrorDetails.ErrorType.RequiredField:
                                label = $"The task {errorDetails.TaskFriendlyName} ({errorDetails.TaskType}, index {errorDetails.NodeDesigner.Task.ID}) requires a value for the field {BehaviorDesignerUtility.SplitCamelCase(errorDetails.FieldName)}.";
                                break;
                            case BehaviorDesigner.Editor.ErrorDetails.ErrorType.SharedVariable:
                                label = $"The task {errorDetails.TaskFriendlyName} ({errorDetails.TaskType}, index {errorDetails.NodeDesigner.Task.ID}) has a Shared Variable field ({BehaviorDesignerUtility.SplitCamelCase(errorDetails.FieldName)}) that is marked as shared but is not referencing a Shared Variable.";
                                break;
                            case BehaviorDesigner.Editor.ErrorDetails.ErrorType.NonUniqueDynamicVariable:
                                label = $"The task {errorDetails.TaskFriendlyName} ({errorDetails.TaskType}, index {errorDetails.NodeDesigner.Task.ID}) has a dynamic Shared Variable ({BehaviorDesignerUtility.SplitCamelCase(errorDetails.FieldName)}) but the name matches an existing Shared Varaible.";
                                break;
                            case BehaviorDesigner.Editor.ErrorDetails.ErrorType.UnknownTask:
                                label = $"The task at index {errorDetails.NodeDesigner.Task.ID} is unknown. Has a task been renamed or deleted?";
                                break;
                            case BehaviorDesigner.Editor.ErrorDetails.ErrorType.InvalidTaskReference:
                                label = $"The task {errorDetails.TaskFriendlyName} ({errorDetails.TaskType}, index {errorDetails.NodeDesigner.Task.ID}) has a field ({BehaviorDesignerUtility.SplitCamelCase(errorDetails.FieldName)}) which is referencing an object within the scene. Behavior tree variables at the project level cannot reference objects within a scene.";
                                break;
                            case BehaviorDesigner.Editor.ErrorDetails.ErrorType.InvalidVariableReference:
                                label = $"The variable {errorDetails.FieldName} is referencing an object within the scene. Behavior tree variables at the project level cannot reference objects within a scene.";
                                break;
                        }
                        EditorGUILayout.LabelField(label, (i % 2 != 0) ? BehaviorDesignerUtility.ErrorListDarkBackground : BehaviorDesignerUtility.ErrorListLightBackground, GUILayout.Height(30f), GUILayout.Width(Screen.width - 7));
                    }
                }
            }
            else if (!BehaviorDesignerPreferences.GetBool(BDPreferences.ErrorChecking))
            {
                EditorGUILayout.LabelField("Enable realtime error checking from the preferences to view the errors.", BehaviorDesignerUtility.ErrorListLightBackground);
            }
            else
            {
                EditorGUILayout.LabelField("The behavior tree has no errors.", BehaviorDesignerUtility.ErrorListLightBackground);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}