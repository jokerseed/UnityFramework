using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    public class BehaviorDesignerPreferences : UnityEditor.Editor
    {
        private static string[] prefString;

        private static string[] serializationString = new string[2] { "Binary", "JSON" };

        private static Dictionary<BDPreferences, object> prefToValue = new Dictionary<BDPreferences, object>();

        private static string[] PrefString
        {
            get
            {
                if (prefString == null)
                {
                    InitPrefString();
                }
                return prefString;
            }
        }

        public static void InitPrefernces()
        {
            if (!EditorPrefs.HasKey(PrefString[0]))
            {
                SetBool(BDPreferences.ShowWelcomeScreen, value: true);
            }
            if (!EditorPrefs.HasKey(PrefString[1]))
            {
                SetBool(BDPreferences.ShowSceneIcon, value: true);
            }
            if (!EditorPrefs.HasKey(PrefString[2]))
            {
                SetBool(BDPreferences.ShowHierarchyIcon, value: true);
            }
            if (!EditorPrefs.HasKey(PrefString[3]))
            {
                SetBool(BDPreferences.OpenInspectorOnTaskSelection, value: false);
            }
            if (!EditorPrefs.HasKey(PrefString[3]))
            {
                SetBool(BDPreferences.OpenInspectorOnTaskSelection, value: false);
            }
            if (!EditorPrefs.HasKey(PrefString[5]))
            {
                SetBool(BDPreferences.FadeNodes, value: true);
            }
            if (!EditorPrefs.HasKey(PrefString[6]))
            {
                SetBool(BDPreferences.EditablePrefabInstances, value: false);
            }
            if (!EditorPrefs.HasKey(PrefString[7]))
            {
                SetBool(BDPreferences.PropertiesPanelOnLeft, value: true);
            }
            if (!EditorPrefs.HasKey(PrefString[8]))
            {
                SetBool(BDPreferences.MouseWhellScrolls, value: false);
            }
            if (!EditorPrefs.HasKey(PrefString[9]))
            {
                SetBool(BDPreferences.FoldoutFields, value: true);
            }
            if (!EditorPrefs.HasKey(PrefString[10]))
            {
                SetBool(BDPreferences.CompactMode, value: false);
            }
            if (!EditorPrefs.HasKey(PrefString[11]))
            {
                SetBool(BDPreferences.SnapToGrid, value: true);
            }
            if (!EditorPrefs.HasKey(PrefString[12]))
            {
                SetBool(BDPreferences.ShowTaskDescription, value: true);
            }
            if (!EditorPrefs.HasKey(PrefString[13]))
            {
                SetBool(BDPreferences.BinarySerialization, value: false);
            }
            if (!EditorPrefs.HasKey(PrefString[14]))
            {
                SetBool(BDPreferences.UndoRedo, value: true);
            }
            if (!EditorPrefs.HasKey(PrefString[15]))
            {
                SetBool(BDPreferences.ErrorChecking, value: true);
            }
            if (!EditorPrefs.HasKey(PrefString[16]))
            {
                SetBool(BDPreferences.SelectOnBreakpoint, value: false);
            }
            if (!EditorPrefs.HasKey(PrefString[17]))
            {
                SetBool(BDPreferences.UpdateCheck, value: true);
            }
            if (!EditorPrefs.HasKey(PrefString[18]))
            {
                SetBool(BDPreferences.AddGameGUIComponent, value: false);
            }
            if (!EditorPrefs.HasKey(PrefString[19]))
            {
                SetInt(BDPreferences.GizmosViewMode, 2);
            }
            if (!EditorPrefs.HasKey(PrefString[20]))
            {
                SetInt(BDPreferences.QuickSearchKeyCode, 32);
            }
            if (GetBool(BDPreferences.EditablePrefabInstances) && GetBool(BDPreferences.BinarySerialization))
            {
                SetBool(BDPreferences.BinarySerialization, value: false);
            }
        }

        private static void InitPrefString()
        {
            prefString = new string[22];
            for (int i = 0; i < prefString.Length; i++)
            {
                prefString[i] = $"BehaviorDesigner{(BDPreferences)i}";
            }
        }

        public static void DrawPreferencesPane(PreferenceChangeHandler callback)
        {
            DrawBoolPref(BDPreferences.ShowWelcomeScreen, "Show welcome screen", callback);
            DrawBoolPref(BDPreferences.ShowSceneIcon, "Show Behavior Designer icon in the scene", callback);
            DrawBoolPref(BDPreferences.ShowHierarchyIcon, "Show Behavior Designer icon in the hierarchy window", callback);
            DrawBoolPref(BDPreferences.OpenInspectorOnTaskSelection, "Open inspector on single task selection", callback);
            DrawBoolPref(BDPreferences.OpenInspectorOnTaskDoubleClick, "Open inspector on task double click", callback);
            DrawBoolPref(BDPreferences.FadeNodes, "Fade tasks after they are done running", callback);
            DrawBoolPref(BDPreferences.EditablePrefabInstances, "Allow edit of prefab instances", callback);
            DrawBoolPref(BDPreferences.PropertiesPanelOnLeft, "Position properties panel on the left", callback);
            DrawBoolPref(BDPreferences.MouseWhellScrolls, "Mouse wheel scrolls graph view", callback);
            DrawBoolPref(BDPreferences.FoldoutFields, "Grouped fields start visible", callback);
            DrawBoolPref(BDPreferences.CompactMode, "Compact mode", callback);
            DrawBoolPref(BDPreferences.SnapToGrid, "Snap to grid", callback);
            DrawBoolPref(BDPreferences.ShowTaskDescription, "Show selected task description", callback);
            DrawBoolPref(BDPreferences.UndoRedo, "Record undo/redo", callback);
            DrawBoolPref(BDPreferences.ErrorChecking, "Realtime error checking", callback);
            DrawBoolPref(BDPreferences.SelectOnBreakpoint, "Select GameObject if a breakpoint is hit", callback);
            DrawBoolPref(BDPreferences.UpdateCheck, "Check for updates", callback);
            DrawBoolPref(BDPreferences.AddGameGUIComponent, "Add Game GUI Component", callback);
            bool @bool = GetBool(BDPreferences.BinarySerialization);
            if (EditorGUILayout.Popup("Serialization", (!@bool) ? 1 : 0, serializationString) != ((!@bool) ? 1 : 0))
            {
                SetBool(BDPreferences.BinarySerialization, !@bool);
                callback(BDPreferences.BinarySerialization, !@bool);
            }
            int @int = GetInt(BDPreferences.GizmosViewMode);
            int num = (int)(Behavior.GizmoViewMode)(object)EditorGUILayout.EnumPopup("Gizmos View Mode", (Behavior.GizmoViewMode)@int);
            if (num != @int)
            {
                SetInt(BDPreferences.GizmosViewMode, num);
                callback(BDPreferences.GizmosViewMode, num);
            }
            @int = GetInt(BDPreferences.QuickSearchKeyCode);
            num = (int)(KeyCode)(object)EditorGUILayout.EnumPopup("Quick Search Key Code", (KeyCode)@int);
            if (num != @int)
            {
                SetInt(BDPreferences.QuickSearchKeyCode, num);
                callback(BDPreferences.QuickSearchKeyCode, num);
            }
            float @float = GetFloat(BDPreferences.ZoomSpeedMultiplier);
            float num2 = EditorGUILayout.Slider("Zoom Speed Multiplier", @float, 0.1f, 4f);
            if (@float != num2)
            {
                SetFloat(BDPreferences.ZoomSpeedMultiplier, num2);
                callback(BDPreferences.ZoomSpeedMultiplier, num2);
            }
            if (GUILayout.Button("Restore to Defaults", EditorStyles.miniButtonMid))
            {
                ResetPrefs();
            }
        }

        private static void DrawBoolPref(BDPreferences pref, string text, PreferenceChangeHandler callback)
        {
            bool @bool = GetBool(pref);
            bool flag = GUILayout.Toggle(@bool, text);
            if (flag != @bool)
            {
                SetBool(pref, flag);
                callback(pref, flag);
            }
        }

        private static void ResetPrefs()
        {
            SetBool(BDPreferences.ShowWelcomeScreen, value: true);
            SetBool(BDPreferences.ShowSceneIcon, value: true);
            SetBool(BDPreferences.ShowHierarchyIcon, value: true);
            SetBool(BDPreferences.OpenInspectorOnTaskSelection, value: false);
            SetBool(BDPreferences.OpenInspectorOnTaskDoubleClick, value: false);
            SetBool(BDPreferences.FadeNodes, value: true);
            SetBool(BDPreferences.EditablePrefabInstances, value: false);
            SetBool(BDPreferences.PropertiesPanelOnLeft, value: true);
            SetBool(BDPreferences.MouseWhellScrolls, value: false);
            SetBool(BDPreferences.FoldoutFields, value: true);
            SetBool(BDPreferences.CompactMode, value: false);
            SetBool(BDPreferences.SnapToGrid, value: true);
            SetBool(BDPreferences.ShowTaskDescription, value: true);
            SetBool(BDPreferences.BinarySerialization, value: false);
            SetBool(BDPreferences.UndoRedo, value: true);
            SetBool(BDPreferences.ErrorChecking, value: true);
            SetBool(BDPreferences.SelectOnBreakpoint, value: false);
            SetBool(BDPreferences.UpdateCheck, value: true);
            SetBool(BDPreferences.AddGameGUIComponent, value: false);
            SetInt(BDPreferences.GizmosViewMode, 2);
            SetInt(BDPreferences.QuickSearchKeyCode, 32);
            SetInt(BDPreferences.ZoomSpeedMultiplier, 1);
        }

        public static void SetBool(BDPreferences pref, bool value)
        {
            EditorPrefs.SetBool(PrefString[(int)pref], value);
            prefToValue[pref] = value;
        }

        public static bool GetBool(BDPreferences pref)
        {
            if (prefToValue.TryGetValue(pref, out var value))
            {
                return (bool)value;
            }
            value = EditorPrefs.GetBool(PrefString[(int)pref]);
            prefToValue.Add(pref, value);
            return (bool)value;
        }

        public static void SetInt(BDPreferences pref, int value)
        {
            EditorPrefs.SetInt(PrefString[(int)pref], value);
            prefToValue[pref] = value;
        }

        public static int GetInt(BDPreferences pref)
        {
            if (prefToValue.TryGetValue(pref, out var value))
            {
                return (int)value;
            }
            value = EditorPrefs.GetInt(PrefString[(int)pref]);
            prefToValue.Add(pref, value);
            return (int)value;
        }

        public static void SetFloat(BDPreferences pref, float value)
        {
            EditorPrefs.SetFloat(PrefString[(int)pref], value);
            prefToValue[pref] = value;
        }

        public static float GetFloat(BDPreferences pref)
        {
            if (prefToValue.TryGetValue(pref, out var value))
            {
                return (float)value;
            }
            value = EditorPrefs.GetFloat(PrefString[(int)pref], 1f);
            prefToValue.Add(pref, value);
            return (float)value;
        }
    }
}