using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    public class GlobalVariablesWindow : EditorWindow
    {
        private string mVariableName = string.Empty;

        private int mVariableTypeIndex;

        private Vector2 mScrollPosition = Vector2.zero;

        private bool mFocusNameField;

        [SerializeField]
        private float mVariableStartPosition = -1f;

        [SerializeField]
        private List<float> mVariablePosition;

        [SerializeField]
        private int mSelectedVariableIndex = -1;

        [SerializeField]
        private string mSelectedVariableName;

        [SerializeField]
        private int mSelectedVariableTypeIndex;

        private GlobalVariables mVariableSource;

        public static GlobalVariablesWindow instance;

        [MenuItem("Tools/Behavior Designer/Global Variables", false, 1)]
        public static void ShowWindow()
        {
            GlobalVariablesWindow window = EditorWindow.GetWindow<GlobalVariablesWindow>(utility: false, "Global Variables");
            window.minSize = new Vector2(300f, 410f);
            window.maxSize = new Vector2(300f, float.MaxValue);
            window.wantsMouseMove = true;
        }

        public void OnFocus()
        {
            instance = this;
            mVariableSource = GlobalVariables.Instance;
            if (mVariableSource != null)
            {
                mVariableSource.CheckForSerialization(!Application.isPlaying);
            }
            FieldInspector.Init();
        }

        public void OnGUI()
        {
            if (mVariableSource == null)
            {
                mVariableSource = GlobalVariables.Instance;
            }
            if (VariableInspector.DrawVariables(mVariableSource, null, ref mVariableName, ref mFocusNameField, ref mVariableTypeIndex, ref mScrollPosition, ref mVariablePosition, ref mVariableStartPosition, ref mSelectedVariableIndex, ref mSelectedVariableName, ref mSelectedVariableTypeIndex))
            {
                SerializeVariables();
            }
            if (Event.current.type == EventType.MouseDown && VariableInspector.LeftMouseDown(mVariableSource, null, Event.current.mousePosition, mVariablePosition, mVariableStartPosition, mScrollPosition, ref mSelectedVariableIndex, ref mSelectedVariableName, ref mSelectedVariableTypeIndex))
            {
                Event.current.Use();
                Repaint();
            }
        }

        private void SerializeVariables()
        {
            if (mVariableSource == null)
            {
                mVariableSource = GlobalVariables.Instance;
            }
            if (BehaviorDesignerPreferences.GetBool(BDPreferences.BinarySerialization))
            {
                BinarySerialization.Save(mVariableSource);
            }
            else
            {
                JSONSerialization.Save(mVariableSource);
            }
        }
    }
}