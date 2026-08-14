using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    public class VariableInspector : ScriptableObject
    {
        private class SelectedPropertyMapping
        {
            private string mProperty;

            private GameObject mGameObject;

            public string Property => mProperty;

            public GameObject GameObject => mGameObject;

            public SelectedPropertyMapping(string property, GameObject gameObject)
            {
                mProperty = property;
                mGameObject = gameObject;
            }
        }

        private static string[] sharedVariableStrings;

        private static List<Type> sharedVariableTypes;

        private static Dictionary<string, int> sharedVariableTypesDict;

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

        private static SharedVariable mPropertyMappingVariable;

        private static BehaviorSource mPropertyMappingBehaviorSource;

        private static GenericMenu mPropertyMappingMenu;

        public void ResetSelectedVariableIndex()
        {
            mSelectedVariableIndex = -1;
            mVariableStartPosition = -1f;
            if (mVariablePosition != null)
            {
                mVariablePosition.Clear();
            }
        }

        public void OnEnable()
        {
            base.hideFlags = HideFlags.HideAndDontSave;
        }

        public static List<Type> FindAllSharedVariableTypes(bool removeShared)
        {
            if (sharedVariableTypes != null)
            {
                return sharedVariableTypes;
            }
            sharedVariableTypes = new List<Type>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type[] types = assemblies[i].GetTypes();
                    for (int j = 0; j < types.Length; j++)
                    {
                        if (types[j].IsSubclassOf(typeof(SharedVariable)) && !types[j].IsAbstract)
                        {
                            sharedVariableTypes.Add(types[j]);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
            sharedVariableTypes.Sort(new AlphanumComparator<Type>());
            sharedVariableStrings = new string[sharedVariableTypes.Count];
            sharedVariableTypesDict = new Dictionary<string, int>();
            for (int k = 0; k < sharedVariableTypes.Count; k++)
            {
                string text = sharedVariableTypes[k].Name;
                sharedVariableTypesDict.Add(text, k);
                if (removeShared && text.Length > 6 && text.Substring(0, 6).Equals("Shared"))
                {
                    text = text.Substring(6, text.Length - 6);
                }
                sharedVariableStrings[k] = text;
            }
            return sharedVariableTypes;
        }

        public bool ClearFocus(bool addVariable, BehaviorSource behaviorSource)
        {
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(string.Empty);
            bool result = false;
            if (addVariable && !string.IsNullOrEmpty(mVariableName) && VariableNameValid(behaviorSource, mVariableName))
            {
                result = AddVariable(behaviorSource, mVariableName, mVariableTypeIndex, fromGlobalVariablesWindow: false);
                mVariableName = string.Empty;
            }
            return result;
        }

        public bool HasFocus()
        {
            return GUIUtility.keyboardControl != 0 && !string.IsNullOrEmpty(mVariableName);
        }

        public void FocusNameField()
        {
            mFocusNameField = true;
        }

        public bool LeftMouseDown(IVariableSource variableSource, BehaviorSource behaviorSource, Vector2 mousePosition)
        {
            return LeftMouseDown(variableSource, behaviorSource, mousePosition, mVariablePosition, mVariableStartPosition, mScrollPosition, ref mSelectedVariableIndex, ref mSelectedVariableName, ref mSelectedVariableTypeIndex);
        }

        public static bool LeftMouseDown(IVariableSource variableSource, BehaviorSource behaviorSource, Vector2 mousePosition, List<float> variablePosition, float variableStartPosition, Vector2 scrollPosition, ref int selectedVariableIndex, ref string selectedVariableName, ref int selectedVariableTypeIndex)
        {
            if (variablePosition != null && mousePosition.y > variableStartPosition && variableSource != null)
            {
                List<SharedVariable> list = null;
                if (!Application.isPlaying && behaviorSource != null && behaviorSource.Owner is Behavior)
                {
                    Behavior behavior = behaviorSource.Owner as Behavior;
                    if (behavior.ExternalBehavior != null)
                    {
                        BehaviorSource behaviorSource2 = behavior.GetBehaviorSource();
                        behaviorSource2.CheckForSerialization(force: true);
                        list = behaviorSource2.GetAllVariables();
                        ExternalBehavior externalBehavior = behavior.ExternalBehavior;
                        externalBehavior.BehaviorSource.Owner = externalBehavior;
                        externalBehavior.BehaviorSource.CheckForSerialization(force: true, behaviorSource);
                    }
                    else
                    {
                        list = variableSource.GetAllVariables();
                    }
                }
                else
                {
                    list = variableSource.GetAllVariables();
                }
                if (list == null || list.Count != variablePosition.Count)
                {
                    return false;
                }
                for (int i = 0; i < variablePosition.Count; i++)
                {
                    if (mousePosition.y < variablePosition[i] - scrollPosition.y)
                    {
                        if (i == selectedVariableIndex)
                        {
                            return false;
                        }
                        selectedVariableIndex = i;
                        selectedVariableName = list[i].Name;
                        selectedVariableTypeIndex = sharedVariableTypesDict[list[i].GetType().Name];
                        return true;
                    }
                }
            }
            if (selectedVariableIndex != -1)
            {
                selectedVariableIndex = -1;
                return true;
            }
            return false;
        }

        public bool DrawVariables(BehaviorSource behaviorSource)
        {
            return DrawVariables(behaviorSource, behaviorSource, ref mVariableName, ref mFocusNameField, ref mVariableTypeIndex, ref mScrollPosition, ref mVariablePosition, ref mVariableStartPosition, ref mSelectedVariableIndex, ref mSelectedVariableName, ref mSelectedVariableTypeIndex);
        }

        public static bool DrawVariables(IVariableSource variableSource, BehaviorSource behaviorSource, ref string variableName, ref bool focusNameField, ref int variableTypeIndex, ref Vector2 scrollPosition, ref List<float> variablePosition, ref float variableStartPosition, ref int selectedVariableIndex, ref string selectedVariableName, ref int selectedVariableTypeIndex)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            bool flag = false;
            bool flag2 = false;
            if (DrawHeader(variableSource, behaviorSource == null, ref variableStartPosition, ref variableName, ref focusNameField, ref variableTypeIndex, ref selectedVariableIndex, ref selectedVariableName, ref selectedVariableTypeIndex))
            {
                flag = true;
            }
            List<SharedVariable> variables = variableSource?.GetAllVariables();
            if (variables != null && variables.Count > 0)
            {
                GUI.enabled = !flag2;
                if (DrawAllVariables(showFooter: true, variableSource, ref variables, canSelect: true, ref variablePosition, ref selectedVariableIndex, ref selectedVariableName, ref selectedVariableTypeIndex, drawRemoveButton: true, drawLastSeparator: true))
                {
                    flag = true;
                }
            }
            if (flag)
            {
                variableSource?.SetAllVariables(variables);
            }
            GUI.enabled = true;
            GUILayout.EndScrollView();
            if (flag && !EditorApplication.isPlayingOrWillChangePlaymode && behaviorSource != null && behaviorSource.Owner is Behavior)
            {
                Behavior behavior = behaviorSource.Owner as Behavior;
                if (behavior.ExternalBehavior != null)
                {
                    if (BehaviorDesignerPreferences.GetBool(BDPreferences.BinarySerialization))
                    {
                        BinarySerialization.Save(behaviorSource);
                    }
                    else
                    {
                        JSONSerialization.Save(behaviorSource);
                    }
                    BehaviorSource behaviorSource2 = behavior.ExternalBehavior.GetBehaviorSource();
                    behaviorSource2.CheckForSerialization(force: true);
                    SyncVariables(behaviorSource2, variables);
                }
            }
            return flag;
        }

        public static bool SyncVariables(BehaviorSource localBehaviorSource, List<SharedVariable> variables)
        {
            List<SharedVariable> list = localBehaviorSource.GetAllVariables();
            if (variables == null)
            {
                if (list != null && list.Count > 0)
                {
                    list.Clear();
                    return true;
                }
                return false;
            }
            bool result = false;
            if (list == null)
            {
                list = new List<SharedVariable>();
                localBehaviorSource.SetAllVariables(list);
                result = true;
            }
            for (int i = 0; i < variables.Count; i++)
            {
                if (variables[i] != null)
                {
                    if (list.Count - 1 < i)
                    {
                        SharedVariable sharedVariable = Activator.CreateInstance(variables[i].GetType()) as SharedVariable;
                        sharedVariable.Name = variables[i].Name;
                        sharedVariable.IsShared = true;
                        sharedVariable.SetValue(variables[i].GetValue());
                        list.Add(sharedVariable);
                        result = true;
                    }
                    else if (list[i].Name != variables[i].Name || list[i].GetType() != variables[i].GetType())
                    {
                        SharedVariable sharedVariable2 = Activator.CreateInstance(variables[i].GetType()) as SharedVariable;
                        sharedVariable2.Name = variables[i].Name;
                        sharedVariable2.IsShared = true;
                        sharedVariable2.SetValue(variables[i].GetValue());
                        list[i] = sharedVariable2;
                        result = true;
                    }
                }
            }
            for (int num = list.Count - 1; num > variables.Count - 1; num--)
            {
                list.RemoveAt(num);
                result = true;
            }
            return result;
        }

        private static bool DrawHeader(IVariableSource variableSource, bool fromGlobalVariablesWindow, ref float variableStartPosition, ref string variableName, ref bool focusNameField, ref int variableTypeIndex, ref int selectedVariableIndex, ref string selectedVariableName, ref int selectedVariableTypeIndex)
        {
            if (sharedVariableTypes == null)
            {
                FindAllSharedVariableTypes(removeShared: true);
            }
            GUILayout.Space(6f);
            EditorGUIUtility.labelWidth = 150f;
            GUILayout.BeginHorizontal();
            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Name", GUILayout.Width(70f));
            GUI.SetNextControlName("Name");
            variableName = EditorGUILayout.TextField(variableName, GUILayout.Width(212f));
            if (focusNameField)
            {
                GUI.FocusControl("Name");
                focusNameField = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label("Type", GUILayout.Width(70f));
            variableTypeIndex = EditorGUILayout.Popup(variableTypeIndex, sharedVariableStrings, EditorStyles.popup, GUILayout.Width(163f));
            GUILayout.Space(4f);
            bool flag = false;
            bool flag2 = VariableNameValid(variableSource, variableName);
            bool enabled = GUI.enabled;
            GUI.enabled = flag2 && enabled;
            GUI.SetNextControlName("Add");
            if (GUILayout.Button("Add", EditorStyles.miniButton, GUILayout.Width(40f)) && flag2)
            {
                if (fromGlobalVariablesWindow && variableSource == null)
                {
                    GlobalVariables globalVariables = ScriptableObject.CreateInstance(typeof(GlobalVariables)) as GlobalVariables;
                    string text = "";
                    string text2 = "/Resources/BehaviorDesignerGlobalVariables.asset";
                    //if (!Directory.Exists(Application.dataPath + text + "/Resources"))
                    //{
                    //    Directory.CreateDirectory(Application.dataPath + text + "/Resources");
                    //}
                    if (!File.Exists(Application.dataPath + text2))
                    {
                        AssetDatabase.CreateAsset(globalVariables, "Assets" + text2);
                        EditorUtility.DisplayDialog("Created Global Variables", "Behavior Designer Global Variables asset created:\n\nAssets" + text + "/Resources/BehaviorDesignerGlobalVariables.asset\n\nNote: Copy this file to transfer global variables between projects.", "OK");
                    }
                    variableSource = globalVariables;
                }
                flag = AddVariable(variableSource, variableName, variableTypeIndex, fromGlobalVariablesWindow);
                if (flag)
                {
                    selectedVariableIndex = variableSource.GetAllVariables().Count - 1;
                    selectedVariableName = variableName;
                    selectedVariableTypeIndex = variableTypeIndex;
                    variableName = string.Empty;
                    GUI.FocusControl(string.Empty);
                }
            }
            GUILayout.Space(6f);
            GUILayout.EndHorizontal();
            if (!fromGlobalVariablesWindow)
            {
                GUI.enabled = true;
                GUILayout.Space(3f);
                GUILayout.BeginHorizontal();
                GUILayout.Space(5f);
                if (GUILayout.Button("Global Variables", EditorStyles.miniButton, GUILayout.Width(284f)))
                {
                    GlobalVariablesWindow.ShowWindow();
                }
                GUILayout.EndHorizontal();
            }
            BehaviorDesignerUtility.DrawContentSeperator(2);
            GUILayout.Space(4f);
            if (variableStartPosition == -1f && Event.current.type == EventType.Repaint)
            {
                variableStartPosition = GUILayoutUtility.GetLastRect().yMax;
            }
            GUI.enabled = enabled;
            return flag;
        }

        private static bool AddVariable(IVariableSource variableSource, string variableName, int variableTypeIndex, bool fromGlobalVariablesWindow)
        {
            SharedVariable item = CreateVariable(variableTypeIndex, variableName, fromGlobalVariablesWindow);
            List<SharedVariable> list = variableSource?.GetAllVariables();
            if (list == null)
            {
                list = new List<SharedVariable>();
            }
            list.Add(item);
            variableSource.SetAllVariables(list);
            return true;
        }

        public static bool DrawAllVariables(bool showFooter, IVariableSource variableSource, ref List<SharedVariable> variables, bool canSelect, ref List<float> variablePosition, ref int selectedVariableIndex, ref string selectedVariableName, ref int selectedVariableTypeIndex, bool drawRemoveButton, bool drawLastSeparator)
        {
            if (variables == null)
            {
                return false;
            }
            bool result = false;
            if (canSelect && variablePosition == null)
            {
                variablePosition = new List<float>();
            }
            for (int i = 0; i < variables.Count; i++)
            {
                SharedVariable sharedVariable = variables[i];
                if (sharedVariable == null || sharedVariable.IsDynamic)
                {
                    continue;
                }
                if (canSelect && selectedVariableIndex == i)
                {
                    if (i == 0)
                    {
                        GUILayout.Space(2f);
                    }
                    bool deleted = false;
                    if (DrawSelectedVariable(variableSource, ref variables, sharedVariable, ref selectedVariableIndex, ref selectedVariableName, ref selectedVariableTypeIndex, ref deleted))
                    {
                        result = true;
                    }
                    if (deleted)
                    {
                        if (BehaviorDesignerWindow.instance != null)
                        {
                            BehaviorDesignerWindow.instance.RemoveSharedVariableReferences(sharedVariable);
                        }
                        variables.RemoveAt(i);
                        if (selectedVariableIndex == i)
                        {
                            selectedVariableIndex = -1;
                        }
                        else if (selectedVariableIndex > i)
                        {
                            selectedVariableIndex--;
                        }
                        result = true;
                        break;
                    }
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    if (DrawSharedVariable(variableSource, sharedVariable, selected: false))
                    {
                        result = true;
                    }
                    if (drawRemoveButton && GUILayout.Button(BehaviorDesignerUtility.VariableDeleteButtonTexture, BehaviorDesignerUtility.PlainButtonGUIStyle, GUILayout.Width(19f)) && EditorUtility.DisplayDialog("Delete Variable", "Are you sure you want to delete this variable?", "Yes", "No"))
                    {
                        if (BehaviorDesignerWindow.instance != null)
                        {
                            if (BehaviorDesignerWindow.instance.ActiveBehaviorSource != null)
                            {
                                BehaviorUndo.RegisterUndo("Delete Variable", BehaviorDesignerWindow.instance.ActiveBehaviorSource.Owner.GetObject());
                            }
                            BehaviorDesignerWindow.instance.RemoveSharedVariableReferences(sharedVariable);
                        }
                        variables.RemoveAt(i);
                        if (canSelect)
                        {
                            if (selectedVariableIndex == i)
                            {
                                selectedVariableIndex = -1;
                            }
                            else if (selectedVariableIndex > i)
                            {
                                selectedVariableIndex--;
                            }
                        }
                        result = true;
                        break;
                    }
                    if (BehaviorDesignerWindow.instance != null && BehaviorDesignerWindow.instance.ContainsError(null, variables[i].Name))
                    {
                        GUILayout.Box(BehaviorDesignerUtility.ErrorIconTexture, BehaviorDesignerUtility.PlainTextureGUIStyle, GUILayout.Width(20f));
                    }
                    GUILayout.Space(10f);
                    GUILayout.EndHorizontal();
                    if (i != variables.Count - 1 || drawLastSeparator)
                    {
                        BehaviorDesignerUtility.DrawContentSeperator(2, 7);
                    }
                }
                GUILayout.Space(4f);
                if (canSelect && Event.current.type == EventType.Repaint)
                {
                    if (variablePosition.Count <= i)
                    {
                        variablePosition.Add(GUILayoutUtility.GetLastRect().yMax);
                    }
                    else
                    {
                        variablePosition[i] = GUILayoutUtility.GetLastRect().yMax;
                    }
                }
            }
            if (canSelect && variables.Count < variablePosition.Count)
            {
                for (int num = variablePosition.Count - 1; num >= variables.Count; num--)
                {
                    variablePosition.RemoveAt(num);
                }
            }
            if (showFooter && variables.Count > 0)
            {
                GUI.enabled = true;
                GUILayout.Label("Select a variable to change its properties.", BehaviorDesignerUtility.LabelWrapGUIStyle);
            }
            return result;
        }

        private static bool DrawSharedVariable(IVariableSource variableSource, SharedVariable sharedVariable, bool selected)
        {
            if (sharedVariable == null || sharedVariable.GetType().GetProperty("Value") == null)
            {
                return false;
            }
            GUILayout.BeginHorizontal();
            bool result = false;
            if (!string.IsNullOrEmpty(sharedVariable.PropertyMapping))
            {
                if (selected)
                {
                    GUILayout.Label("Property");
                }
                else
                {
                    GUILayout.Label(new GUIContent(sharedVariable.Name, sharedVariable.Tooltip));
                }
                string[] array = sharedVariable.PropertyMapping.Split('.');
                string text = array[array.Length - 1].Replace('/', '.');
                GUILayout.Label(new GUIContent(text, text));
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                FieldInspector.DrawFields(null, sharedVariable, new GUIContent(sharedVariable.Name, sharedVariable.Tooltip));
                result = EditorGUI.EndChangeCheck();
            }
            if (!sharedVariable.IsGlobal && GUILayout.Button(BehaviorDesignerUtility.VariableMapButtonTexture, BehaviorDesignerUtility.PlainButtonGUIStyle, GUILayout.Width(19f)))
            {
                ShowPropertyMappingMenu(variableSource as BehaviorSource, sharedVariable);
            }
            GUILayout.EndHorizontal();
            return result;
        }

        private static bool DrawSelectedVariable(IVariableSource variableSource, ref List<SharedVariable> variables, SharedVariable sharedVariable, ref int selectedVariableIndex, ref string selectedVariableName, ref int selectedVariableTypeIndex, ref bool deleted)
        {
            bool result = false;
            GUILayout.BeginVertical(BehaviorDesignerUtility.SelectedBackgroundGUIStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(70f));
            EditorGUI.BeginChangeCheck();
            if (string.IsNullOrEmpty(selectedVariableName))
            {
                selectedVariableName = sharedVariable.Name;
            }
            selectedVariableName = EditorGUILayout.TextField(selectedVariableName, GUILayout.Width(140f));
            if (EditorGUI.EndChangeCheck())
            {
                if (VariableNameValid(variableSource, selectedVariableName))
                {
                    variableSource.UpdateVariableName(sharedVariable, selectedVariableName);
                }
                result = true;
            }
            GUILayout.Space(10f);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && selectedVariableIndex < variables.Count - 1;
            if (GUILayout.Button(BehaviorDesignerUtility.DownArrowButtonTexture, BehaviorDesignerUtility.PlainButtonGUIStyle, GUILayout.Width(19f)))
            {
                SharedVariable value = variables[selectedVariableIndex + 1];
                variables[selectedVariableIndex + 1] = variables[selectedVariableIndex];
                variables[selectedVariableIndex] = value;
                selectedVariableIndex++;
                result = true;
            }
            GUI.enabled = enabled && (selectedVariableIndex < variables.Count - 1 || selectedVariableIndex != 0);
            GUI.enabled = enabled && selectedVariableIndex != 0;
            if (GUILayout.Button(BehaviorDesignerUtility.UpArrowButtonTexture, BehaviorDesignerUtility.PlainButtonGUIStyle, GUILayout.Width(20f)))
            {
                SharedVariable value2 = variables[selectedVariableIndex - 1];
                variables[selectedVariableIndex - 1] = variables[selectedVariableIndex];
                variables[selectedVariableIndex] = value2;
                selectedVariableIndex--;
                result = true;
            }
            GUI.enabled = enabled;
            if (GUILayout.Button(BehaviorDesignerUtility.VariableDeleteButtonTexture, BehaviorDesignerUtility.PlainButtonGUIStyle, GUILayout.Width(19f)) && EditorUtility.DisplayDialog("Delete Variable", "Are you sure you want to delete this variable?", "Yes", "No"))
            {
                deleted = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Type", GUILayout.Width(70f));
            EditorGUI.BeginChangeCheck();
            selectedVariableTypeIndex = EditorGUILayout.Popup(selectedVariableTypeIndex, sharedVariableStrings, EditorStyles.toolbarPopup, GUILayout.Width(200f));
            if (EditorGUI.EndChangeCheck() && sharedVariableTypesDict[sharedVariable.GetType().Name] != selectedVariableTypeIndex)
            {
                if (BehaviorDesignerWindow.instance != null)
                {
                    BehaviorDesignerWindow.instance.RemoveSharedVariableReferences(sharedVariable);
                }
                sharedVariable = CreateVariable(selectedVariableTypeIndex, sharedVariable.Name, sharedVariable.IsGlobal);
                variables[selectedVariableIndex] = sharedVariable;
                result = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tooltip", GUILayout.Width(70f));
            EditorGUI.BeginChangeCheck();
            sharedVariable.Tooltip = EditorGUILayout.TextField(sharedVariable.Tooltip, GUILayout.Width(200f));
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                result = true;
            }
            EditorGUI.BeginChangeCheck();
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUI.enabled = CanNetworkSync(sharedVariable.GetType().GetProperty("Value").PropertyType);
            EditorGUI.BeginChangeCheck();
            if (EditorGUI.EndChangeCheck())
            {
                result = true;
            }
            GUILayout.EndHorizontal();
            GUI.enabled = enabled;
            GUILayout.BeginHorizontal();
            if (DrawSharedVariable(variableSource, sharedVariable, selected: true))
            {
                result = true;
            }
            if (BehaviorDesignerWindow.instance != null && BehaviorDesignerWindow.instance.ContainsError(null, variables[selectedVariableIndex].Name))
            {
                GUILayout.Box(BehaviorDesignerUtility.ErrorIconTexture, BehaviorDesignerUtility.PlainTextureGUIStyle, GUILayout.Width(20f));
            }
            GUILayout.EndHorizontal();
            BehaviorDesignerUtility.DrawContentSeperator(4, 7);
            GUILayout.EndVertical();
            GUILayout.Space(3f);
            return result;
        }

        private static bool VariableNameValid(IVariableSource variableSource, string variableName)
        {
            return !variableName.Equals(string.Empty) && (variableSource == null || variableSource.GetVariable(variableName) == null);
        }

        private static SharedVariable CreateVariable(int index, string name, bool global)
        {
            SharedVariable sharedVariable = Activator.CreateInstance(sharedVariableTypes[index]) as SharedVariable;
            sharedVariable.Name = name;
            sharedVariable.IsShared = true;
            sharedVariable.IsGlobal = global;
            return sharedVariable;
        }

        private static bool CanNetworkSync(Type type)
        {
            if (type == typeof(bool) || type == typeof(Color) || type == typeof(float) || type == typeof(GameObject) || type == typeof(int) || type == typeof(Quaternion) || type == typeof(Rect) || type == typeof(string) || type == typeof(Transform) || type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4))
            {
                return true;
            }
            return false;
        }

        private static void ShowPropertyMappingMenu(BehaviorSource behaviorSource, SharedVariable sharedVariable)
        {
            mPropertyMappingVariable = sharedVariable;
            mPropertyMappingBehaviorSource = behaviorSource;
            mPropertyMappingMenu = new GenericMenu();
            List<string> propertyNames = new List<string>();
            List<GameObject> propertyGameObjects = new List<GameObject>();
            propertyNames.Add("None");
            propertyGameObjects.Add(null);
            int num = 0;
            if (behaviorSource.Owner.GetObject() is Behavior)
            {
                GameObject gameObject = (behaviorSource.Owner.GetObject() as Behavior).gameObject;
                int num2;
                if ((num2 = AddPropertyName(sharedVariable, gameObject, ref propertyNames, ref propertyGameObjects, behaviorGameObject: true)) != -1)
                {
                    num = num2;
                }
                GameObject[] array;
                if (AssetDatabase.GetAssetPath(gameObject).Length == 0)
                {
                    array = UnityEngine.Object.FindObjectsOfType<GameObject>();
                }
                else
                {
                    Transform[] componentsInChildren = gameObject.GetComponentsInChildren<Transform>();
                    array = new GameObject[componentsInChildren.Length];
                    for (int i = 0; i < componentsInChildren.Length; i++)
                    {
                        array[i] = componentsInChildren[i].gameObject;
                    }
                }
                for (int j = 0; j < array.Length; j++)
                {
                    if (!array[j].Equals(gameObject) && (num2 = AddPropertyName(sharedVariable, array[j], ref propertyNames, ref propertyGameObjects, behaviorGameObject: false)) != -1)
                    {
                        num = num2;
                    }
                }
            }
            for (int k = 0; k < propertyNames.Count; k++)
            {
                string[] array2 = propertyNames[k].Split('.');
                if (propertyGameObjects[k] != null)
                {
                    array2[array2.Length - 1] = GetFullPath(propertyGameObjects[k].transform) + "/" + array2[array2.Length - 1];
                }
                mPropertyMappingMenu.AddItem(new GUIContent(array2[array2.Length - 1]), k == num, PropertySelected, new SelectedPropertyMapping(propertyNames[k], propertyGameObjects[k]));
            }
            mPropertyMappingMenu.ShowAsContext();
        }

        private static string GetFullPath(Transform transform)
        {
            if (transform.parent == null)
            {
                return transform.name;
            }
            return GetFullPath(transform.parent) + "/" + transform.name;
        }

        private static int AddPropertyName(SharedVariable sharedVariable, GameObject gameObject, ref List<string> propertyNames, ref List<GameObject> propertyGameObjects, bool behaviorGameObject)
        {
            int result = -1;
            Component[] array = null;
            if (gameObject != null)
            {
                array = gameObject.GetComponents(typeof(Component));
                Type propertyType = sharedVariable.GetType().GetProperty("Value").PropertyType;
                for (int i = 0; i < array.Length; i++)
                {
                    if (array[i] == null)
                    {
                        continue;
                    }
                    PropertyInfo[] properties = array[i].GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
                    for (int j = 0; j < properties.Length; j++)
                    {
                        if (properties[j].PropertyType.Equals(propertyType) && !properties[j].IsSpecialName)
                        {
                            string text = array[i].GetType().FullName + "/" + properties[j].Name;
                            if (text.Equals(sharedVariable.PropertyMapping) && (object.Equals(sharedVariable.PropertyMappingOwner, gameObject) || (object.Equals(sharedVariable.PropertyMappingOwner, null) && behaviorGameObject)))
                            {
                                result = propertyNames.Count;
                            }
                            propertyNames.Add(text);
                            propertyGameObjects.Add(gameObject);
                        }
                    }
                }
            }
            return result;
        }

        private static void PropertySelected(object selected)
        {
            SelectedPropertyMapping selectedPropertyMapping = selected as SelectedPropertyMapping;
            if (selectedPropertyMapping.Property.Equals("None"))
            {
                mPropertyMappingVariable.PropertyMapping = string.Empty;
                mPropertyMappingVariable.PropertyMappingOwner = null;
            }
            else
            {
                mPropertyMappingVariable.PropertyMapping = selectedPropertyMapping.Property;
                mPropertyMappingVariable.PropertyMappingOwner = selectedPropertyMapping.GameObject;
            }
            if (BehaviorDesignerPreferences.GetBool(BDPreferences.BinarySerialization))
            {
                BinarySerialization.Save(mPropertyMappingBehaviorSource);
            }
            else
            {
                JSONSerialization.Save(mPropertyMappingBehaviorSource);
            }
        }
    }
}