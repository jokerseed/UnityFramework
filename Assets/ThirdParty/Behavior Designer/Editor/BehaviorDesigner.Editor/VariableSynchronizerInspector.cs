using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    [CustomEditor(typeof(VariableSynchronizer))]
    public class VariableSynchronizerInspector : UnityEditor.Editor
    {
        public enum ComponentListType
        {
            Instant,
            Popup,
            BehaviorDesignerGroup,
            None
        }

        [Serializable]
        public class Synchronizer
        {
            public GameObject gameObject;

            public Component component;

            public string targetName;

            public bool global;

            public int componentGroup;

            public string componentName;
        }

        [SerializeField]
        private Synchronizer sharedVariableSynchronizer = new Synchronizer();

        [SerializeField]
        private string sharedVariableValueTypeName;

        private Type sharedVariableValueType;

        [SerializeField]
        private VariableSynchronizer.SynchronizationType synchronizationType;

        [SerializeField]
        private bool setVariable;

        [SerializeField]
        private Synchronizer targetSynchronizer;

        private Action<Synchronizer, Type> thirdPartySynchronizer;

        private Type playMakerSynchronizationType;

        private Type uFrameSynchronizationType;

        public override void OnInspectorGUI()
        {
            VariableSynchronizer variableSynchronizer = base.target as VariableSynchronizer;
            if (variableSynchronizer == null)
            {
                return;
            }
            GUILayout.Space(5f);
            variableSynchronizer.UpdateInterval = (UpdateIntervalType)(object)EditorGUILayout.EnumPopup("Update Interval", variableSynchronizer.UpdateInterval);
            if (variableSynchronizer.UpdateInterval == UpdateIntervalType.SpecifySeconds)
            {
                variableSynchronizer.UpdateIntervalSeconds = EditorGUILayout.FloatField("Seconds", variableSynchronizer.UpdateIntervalSeconds);
            }
            GUILayout.Space(5f);
            GUI.enabled = !Application.isPlaying;
            DrawSharedVariableSynchronizer(sharedVariableSynchronizer, null);
            if (string.IsNullOrEmpty(sharedVariableSynchronizer.targetName))
            {
                DrawSynchronizedVariables(variableSynchronizer);
                return;
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Direction", GUILayout.MaxWidth(146f));
            if (GUILayout.Button(BehaviorDesignerUtility.LoadTexture((!setVariable) ? "RightArrowButton.png" : "LeftArrowButton.png", useSkinColor: true, this), BehaviorDesignerUtility.ButtonGUIStyle, GUILayout.Width(22f)))
            {
                setVariable = !setVariable;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            synchronizationType = (VariableSynchronizer.SynchronizationType)(object)EditorGUILayout.EnumPopup("Type", synchronizationType);
            if (EditorGUI.EndChangeCheck())
            {
                targetSynchronizer = new Synchronizer();
            }
            if (targetSynchronizer == null)
            {
                targetSynchronizer = new Synchronizer();
            }
            if (sharedVariableValueType == null && !string.IsNullOrEmpty(sharedVariableValueTypeName))
            {
                sharedVariableValueType = TaskUtility.GetTypeWithinAssembly(sharedVariableValueTypeName);
            }
            switch (synchronizationType)
            {
                case VariableSynchronizer.SynchronizationType.BehaviorDesigner:
                    DrawSharedVariableSynchronizer(targetSynchronizer, sharedVariableValueType);
                    break;
                case VariableSynchronizer.SynchronizationType.Property:
                    DrawPropertySynchronizer(targetSynchronizer, sharedVariableValueType);
                    break;
                case VariableSynchronizer.SynchronizationType.Animator:
                    DrawAnimatorSynchronizer(targetSynchronizer);
                    break;
                case VariableSynchronizer.SynchronizationType.PlayMaker:
                    DrawPlayMakerSynchronizer(targetSynchronizer, sharedVariableValueType);
                    break;
                case VariableSynchronizer.SynchronizationType.uFrame:
                    DrawuFrameSynchronizer(targetSynchronizer, sharedVariableValueType);
                    break;
            }
            if (string.IsNullOrEmpty(targetSynchronizer.targetName))
            {
                GUI.enabled = false;
            }
            if (GUILayout.Button("Add"))
            {
                VariableSynchronizer.SynchronizedVariable item = new VariableSynchronizer.SynchronizedVariable(synchronizationType, setVariable, sharedVariableSynchronizer.component as Behavior, sharedVariableSynchronizer.targetName, sharedVariableSynchronizer.global, targetSynchronizer.component, targetSynchronizer.targetName, targetSynchronizer.global);
                variableSynchronizer.SynchronizedVariables.Add(item);
                BehaviorDesignerUtility.SetObjectDirty(variableSynchronizer);
                sharedVariableSynchronizer = new Synchronizer();
                targetSynchronizer = new Synchronizer();
            }
            GUI.enabled = true;
            DrawSynchronizedVariables(variableSynchronizer);
        }

        public static void DrawComponentSelector(Synchronizer synchronizer, Type componentType, ComponentListType listType)
        {
            bool flag = false;
            EditorGUI.BeginChangeCheck();
            synchronizer.gameObject = EditorGUILayout.ObjectField("GameObject", synchronizer.gameObject, typeof(GameObject), true) as GameObject;
            if (EditorGUI.EndChangeCheck())
            {
                flag = true;
            }
            if (synchronizer.gameObject == null)
            {
                GUI.enabled = false;
            }
            switch (listType)
            {
                case ComponentListType.Instant:
                    if (flag)
                    {
                        if (synchronizer.gameObject != null)
                        {
                            synchronizer.component = synchronizer.gameObject.GetComponent(componentType);
                        }
                        else
                        {
                            synchronizer.component = null;
                        }
                    }
                    break;
                case ComponentListType.BehaviorDesignerGroup:
                    if (synchronizer.gameObject != null)
                    {
                        Behavior[] components = synchronizer.gameObject.GetComponents<Behavior>();
                        if (components != null && components.Length > 1)
                        {
                            synchronizer.componentGroup = EditorGUILayout.IntField("Behavior Tree Group", synchronizer.componentGroup);
                        }
                        synchronizer.component = GetBehaviorWithGroup(components, synchronizer.componentGroup);
                    }
                    break;
                case ComponentListType.Popup:
                    {
                        int selectedIndex = 0;
                        List<string> list = new List<string>();
                        Component[] array = null;
                        list.Add("None");
                        if (synchronizer.gameObject != null)
                        {
                            array = synchronizer.gameObject.GetComponents(componentType);
                            for (int i = 0; i < array.Length; i++)
                            {
                                if (array[i].Equals(synchronizer.component))
                                {
                                    selectedIndex = list.Count;
                                }
                                string text = BehaviorDesignerUtility.SplitCamelCase(array[i].GetType().Name);
                                int num = 0;
                                for (int j = 0; j < list.Count; j++)
                                {
                                    if (list[i].Equals(text))
                                    {
                                        num++;
                                    }
                                }
                                if (num > 0)
                                {
                                    text = text + " " + num;
                                }
                                list.Add(text);
                            }
                        }
                        EditorGUI.BeginChangeCheck();
                        selectedIndex = EditorGUILayout.Popup("Component", selectedIndex, list.ToArray());
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (selectedIndex != 0)
                            {
                                synchronizer.component = array[selectedIndex - 1];
                            }
                            else
                            {
                                synchronizer.component = null;
                            }
                        }
                        break;
                    }
            }
        }

        private bool DrawSharedVariableSynchronizer(Synchronizer synchronizer, Type valueType)
        {
            DrawComponentSelector(synchronizer, typeof(Behavior), ComponentListType.BehaviorDesignerGroup);
            int selectedIndex = 0;
            int globalStartIndex = -1;
            string[] names = null;
            if (synchronizer.component != null)
            {
                Behavior behavior = synchronizer.component as Behavior;
                selectedIndex = FieldInspector.GetVariablesOfType(valueType, synchronizer.global, synchronizer.targetName, behavior.GetBehaviorSource(), out names, ref globalStartIndex, (valueType == null) ? true : false, addDynamic: false);
            }
            else
            {
                names = new string[1] { "None" };
            }
            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup("Shared Variable", selectedIndex, names);
            if (EditorGUI.EndChangeCheck())
            {
                if (selectedIndex != 0)
                {
                    if (globalStartIndex != -1 && selectedIndex >= globalStartIndex)
                    {
                        synchronizer.targetName = names[selectedIndex].Substring(8, names[selectedIndex].Length - 8);
                        synchronizer.global = true;
                    }
                    else
                    {
                        synchronizer.targetName = names[selectedIndex];
                        synchronizer.global = false;
                    }
                    if (valueType == null)
                    {
                        SharedVariable variable;
                        if (synchronizer.global)
                        {
                            variable = GlobalVariables.Instance.GetVariable(synchronizer.targetName);
                        }
                        else
                        {
                            Behavior behavior2 = synchronizer.component as Behavior;
                            variable = behavior2.GetVariable(names[selectedIndex]);
                        }
                        sharedVariableValueTypeName = variable.GetType().GetProperty("Value").PropertyType.FullName;
                        sharedVariableValueType = null;
                    }
                }
                else
                {
                    synchronizer.targetName = null;
                }
            }
            if (string.IsNullOrEmpty(synchronizer.targetName))
            {
                GUI.enabled = false;
            }
            return GUI.enabled;
        }

        private static Behavior GetBehaviorWithGroup(Behavior[] behaviors, int group)
        {
            if (behaviors == null || behaviors.Length == 0)
            {
                return null;
            }
            if (behaviors.Length == 1)
            {
                return behaviors[0];
            }
            for (int i = 0; i < behaviors.Length; i++)
            {
                if (behaviors[i].Group == group)
                {
                    return behaviors[i];
                }
            }
            return behaviors[0];
        }

        private void DrawPropertySynchronizer(Synchronizer synchronizer, Type valueType)
        {
            DrawComponentSelector(synchronizer, typeof(Component), ComponentListType.Popup);
            int selectedIndex = 0;
            List<string> list = new List<string>();
            PropertyInfo[] array = null;
            list.Add("None");
            if (synchronizer.component != null)
            {
                array = synchronizer.component.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
                for (int i = 0; i < array.Length; i++)
                {
                    if (array[i].PropertyType.Equals(valueType) && !array[i].IsSpecialName)
                    {
                        if (array[i].Name.Equals(synchronizer.targetName))
                        {
                            selectedIndex = list.Count;
                        }
                        list.Add(array[i].Name);
                    }
                }
            }
            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup("Property", selectedIndex, list.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                if (selectedIndex != 0)
                {
                    synchronizer.targetName = list[selectedIndex];
                }
                else
                {
                    synchronizer.targetName = string.Empty;
                }
            }
        }

        private void DrawAnimatorSynchronizer(Synchronizer synchronizer)
        {
            DrawComponentSelector(synchronizer, typeof(Animator), ComponentListType.Instant);
            synchronizer.targetName = EditorGUILayout.TextField("Parameter Name", synchronizer.targetName);
        }

        private void DrawPlayMakerSynchronizer(Synchronizer synchronizer, Type valueType)
        {
            if (playMakerSynchronizationType == null)
            {
                playMakerSynchronizationType = Type.GetType("BehaviorDesigner.Editor.VariableSynchronizerInspector_PlayMaker, Assembly-CSharp-Editor");
                if (playMakerSynchronizationType == null)
                {
                    EditorGUILayout.LabelField("Unable to find PlayMaker inspector task.");
                    return;
                }
            }
            if (thirdPartySynchronizer == null)
            {
                MethodInfo method = playMakerSynchronizationType.GetMethod("DrawPlayMakerSynchronizer");
                if (method != null)
                {
                    thirdPartySynchronizer = (Action<Synchronizer, Type>)Delegate.CreateDelegate(typeof(Action<Synchronizer, Type>), method);
                }
            }
            thirdPartySynchronizer(synchronizer, valueType);
        }

        private void DrawuFrameSynchronizer(Synchronizer synchronizer, Type valueType)
        {
            if (uFrameSynchronizationType == null)
            {
                uFrameSynchronizationType = Type.GetType("BehaviorDesigner.Editor.VariableSynchronizerInspector_uFrame, Assembly-CSharp-Editor");
                if (uFrameSynchronizationType == null)
                {
                    EditorGUILayout.LabelField("Unable to find uFrame inspector task.");
                    return;
                }
            }
            if (thirdPartySynchronizer == null)
            {
                MethodInfo method = uFrameSynchronizationType.GetMethod("DrawSynchronizer");
                if (method != null)
                {
                    thirdPartySynchronizer = (Action<Synchronizer, Type>)Delegate.CreateDelegate(typeof(Action<Synchronizer, Type>), method);
                }
            }
            thirdPartySynchronizer(synchronizer, valueType);
        }

        private void DrawSynchronizedVariables(VariableSynchronizer variableSynchronizer)
        {
            GUI.enabled = true;
            if (variableSynchronizer.SynchronizedVariables == null || variableSynchronizer.SynchronizedVariables.Count == 0)
            {
                return;
            }
            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.x = -5f;
            lastRect.y += lastRect.height + 1f;
            lastRect.height = 2f;
            lastRect.width += 20f;
            GUI.DrawTexture(lastRect, BehaviorDesignerUtility.LoadTexture("ContentSeparator.png", useSkinColor: true, this));
            GUILayout.Space(6f);
            for (int i = 0; i < variableSynchronizer.SynchronizedVariables.Count; i++)
            {
                VariableSynchronizer.SynchronizedVariable synchronizedVariable = variableSynchronizer.SynchronizedVariables[i];
                if (synchronizedVariable.global)
                {
                    if (GlobalVariables.Instance.GetVariable(synchronizedVariable.variableName) == null)
                    {
                        variableSynchronizer.SynchronizedVariables.RemoveAt(i);
                        break;
                    }
                }
                else if (synchronizedVariable.behavior.GetVariable(synchronizedVariable.variableName) == null)
                {
                    variableSynchronizer.SynchronizedVariables.RemoveAt(i);
                    break;
                }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(synchronizedVariable.variableName, GUILayout.MaxWidth(120f));
                if (GUILayout.Button(BehaviorDesignerUtility.LoadTexture((!synchronizedVariable.setVariable) ? "RightArrowButton.png" : "LeftArrowButton.png", useSkinColor: true, this), BehaviorDesignerUtility.ButtonGUIStyle, GUILayout.Width(22f)) && !Application.isPlaying)
                {
                    synchronizedVariable.setVariable = !synchronizedVariable.setVariable;
                }
                EditorGUILayout.LabelField($"{synchronizedVariable.targetName} ({synchronizedVariable.synchronizationType.ToString()})", GUILayout.MinWidth(120f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(BehaviorDesignerUtility.LoadTexture("DeleteButton.png", useSkinColor: true, this), BehaviorDesignerUtility.ButtonGUIStyle, GUILayout.Width(22f)))
                {
                    variableSynchronizer.SynchronizedVariables.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                GUILayout.Space(2f);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2f);
            }
            GUILayout.Space(4f);
        }
    }
}