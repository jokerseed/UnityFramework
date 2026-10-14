using System.Collections.Generic;
using UnityEditor;

namespace Framework.Editor
{
    /// <summary>Project Settings → Framework 页面。</summary>
    static class FrameworkSettingsProvider
    {
        /// <summary>设置页路径。</summary>
        public const string SettingsPath = "Project/Framework";

        [SettingsProvider]
        static SettingsProvider Create()
        {
            SerializedObject serialized = null;
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Framework",
                keywords = new HashSet<string> { "Luban", "YooAsset", "Collector", "Framework" },
                activateHandler = (_, __) => serialized = new SerializedObject(FrameworkSettings.instance),
                guiHandler = _ =>
                {
                    serialized.Update();
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(serialized.FindProperty(nameof(FrameworkSettings.Luban)), true);
                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(serialized.FindProperty(nameof(FrameworkSettings.Collector)), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        FrameworkSettings.instance.SaveSettings();
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox("路径相对工程根目录。保存在 ProjectSettings/FrameworkSettings.asset，请随仓库提交。", MessageType.None);
                },
            };
        }

        [MenuItem("Tools/Framework/Settings", priority = 0)]
        static void Open()
        {
            SettingsService.OpenProjectSettings(SettingsPath);
        }
    }
}
