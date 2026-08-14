using BehaviorDesigner.Runtime;
using UnityEditor;

namespace BehaviorDesigner.Editor
{
    [CustomEditor(typeof(BehaviorManager))]
    public class BehaviorManagerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            BehaviorManager behaviorManager = base.target as BehaviorManager;
            behaviorManager.UpdateInterval = (UpdateIntervalType)(object)EditorGUILayout.EnumPopup("Update Interval", behaviorManager.UpdateInterval);
            if (behaviorManager.UpdateInterval == UpdateIntervalType.SpecifySeconds)
            {
                EditorGUI.indentLevel++;
                behaviorManager.UpdateIntervalSeconds = EditorGUILayout.FloatField("Seconds", behaviorManager.UpdateIntervalSeconds);
                EditorGUI.indentLevel--;
            }
            behaviorManager.ExecutionsPerTick = (BehaviorManager.ExecutionsPerTickType)(object)EditorGUILayout.EnumPopup("Task Execution Type", behaviorManager.ExecutionsPerTick);
            if (behaviorManager.ExecutionsPerTick == BehaviorManager.ExecutionsPerTickType.Count)
            {
                EditorGUI.indentLevel++;
                behaviorManager.MaxTaskExecutionsPerTick = EditorGUILayout.IntField("Max Execution Count", behaviorManager.MaxTaskExecutionsPerTick);
                EditorGUI.indentLevel--;
            }
        }
    }
}