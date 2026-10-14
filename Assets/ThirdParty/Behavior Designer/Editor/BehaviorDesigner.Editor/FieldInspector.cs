using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    public static class FieldInspector
    {
        private const string c_EditorPrefsFoldoutKey = "BehaviorDesigner.Editor.Foldout.";

        private static int currentKeyboardControl = -1;

        private static bool editingArray = false;

        private static int savedArraySize = -1;

        private static int editingFieldHash;

        public static BehaviorSource behaviorSource;

        private static HashSet<int> drawnObjects = new HashSet<int>();

        private static string[] layerNames;

        private static int[] maskValues;

        public static void Init()
        {
            InitLayers();
        }

        public static bool DrawFoldout(int hash, GUIContent guiContent)
        {
            string key = "BehaviorDesigner.Editor.Foldout.." + hash + "." + guiContent.text;
            bool @bool = EditorPrefs.GetBool(key, defaultValue: true);
            bool flag = EditorGUILayout.Foldout(@bool, guiContent);
            if (flag != @bool)
            {
                EditorPrefs.SetBool(key, flag);
            }
            return flag;
        }

        public static object DrawFields(Task task, object obj)
        {
            return DrawFields(task, obj, null);
        }

        public static object DrawFields(Task task, object obj, GUIContent guiContent)
        {
            if (obj == null)
            {
                return null;
            }
            List<Type> baseClasses = GetBaseClasses(obj.GetType());
            BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (int num = baseClasses.Count - 1; num > -1; num--)
            {
                FieldInfo[] fields = baseClasses[num].GetFields(bindingAttr);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (!BehaviorDesignerUtility.HasAttribute(fields[i], typeof(NonSerializedAttribute)) && !BehaviorDesignerUtility.HasAttribute(fields[i], typeof(HideInInspector)) && ((!fields[i].IsPrivate && !fields[i].IsFamily) || BehaviorDesignerUtility.HasAttribute(fields[i], typeof(SerializeField))) && (!(obj is ParentTask) || !fields[i].Name.Equals("children")))
                    {
                        if (guiContent == null)
                        {
                            BehaviorDesigner.Runtime.Tasks.TooltipAttribute[] array = null;
                            string name = fields[i].Name;
                            guiContent = (((array = fields[i].GetCustomAttributes(typeof(BehaviorDesigner.Runtime.Tasks.TooltipAttribute), inherit: false) as BehaviorDesigner.Runtime.Tasks.TooltipAttribute[]).Length <= 0) ? new GUIContent(BehaviorDesignerUtility.SplitCamelCase(name)) : new GUIContent(BehaviorDesignerUtility.SplitCamelCase(name), array[0].Tooltip));
                        }
                        EditorGUI.BeginChangeCheck();
                        object value = DrawField(task, guiContent, fields[i], fields[i].GetValue(obj));
                        if (EditorGUI.EndChangeCheck())
                        {
                            fields[i].SetValue(obj, value);
                            GUI.changed = true;
                        }
                        guiContent = null;
                    }
                }
            }
            return obj;
        }

        public static List<Type> GetBaseClasses(Type t)
        {
            List<Type> list = new List<Type>();
            while (t != null && !t.Equals(typeof(ParentTask)) && !t.Equals(typeof(Task)) && !t.Equals(typeof(SharedVariable)))
            {
                list.Add(t);
                t = t.BaseType;
            }
            return list;
        }

        public static object DrawField(Task task, GUIContent guiContent, FieldInfo field, object value)
        {
            ObjectDrawer objectDrawer = null;
            ObjectDrawerAttribute[] array = null;
            if ((objectDrawer = ObjectDrawerUtility.GetObjectDrawer(task, field)) != null)
            {
                if (value == null && !field.FieldType.IsAbstract)
                {
                    value = ((!typeof(ScriptableObject).IsAssignableFrom(field.FieldType)) ? Activator.CreateInstance(field.FieldType, nonPublic: true) : ScriptableObject.CreateInstance(field.FieldType));
                }
                objectDrawer.Value = value;
                objectDrawer.OnGUI(guiContent);
                if (objectDrawer.Value != value)
                {
                    value = objectDrawer.Value;
                    GUI.changed = true;
                }
                return value;
            }
            if ((array = field.GetCustomAttributes(typeof(ObjectDrawerAttribute), inherit: true) as ObjectDrawerAttribute[]).Length > 0 && (objectDrawer = ObjectDrawerUtility.GetObjectDrawer(task, field, array[0])) != null)
            {
                if (value == null)
                {
                    value = ((!typeof(ScriptableObject).IsAssignableFrom(field.FieldType)) ? Activator.CreateInstance(field.FieldType, nonPublic: true) : ScriptableObject.CreateInstance(field.FieldType));
                }
                objectDrawer.Value = value;
                objectDrawer.OnGUI(guiContent);
                if (objectDrawer.Value != value)
                {
                    value = objectDrawer.Value;
                    GUI.changed = true;
                }
                return value;
            }
            return DrawField(task, guiContent, field, field.FieldType, value);
        }

        private static object DrawField(Task task, GUIContent guiContent, FieldInfo fieldInfo, Type fieldType, object value)
        {
            if (typeof(IList).IsAssignableFrom(fieldType))
            {
                return DrawArrayField(task, guiContent, fieldInfo, fieldType, value);
            }
            return DrawSingleField(task, guiContent, fieldInfo, fieldType, value);
        }

        private static object DrawArrayField(Task task, GUIContent guiContent, FieldInfo fieldInfo, Type fieldType, object value)
        {
            Type type;
            if (fieldType.IsArray)
            {
                type = fieldType.GetElementType();
            }
            else
            {
                Type type2 = fieldType;
                while (!type2.IsGenericType)
                {
                    type2 = type2.BaseType;
                }
                type = type2.GetGenericArguments()[0];
            }
            IList list;
            if (value == null)
            {
                list = ((!fieldType.IsGenericType && !fieldType.IsArray) ? (Activator.CreateInstance(fieldType, nonPublic: true) as IList) : (Activator.CreateInstance(typeof(List<>).MakeGenericType(type), nonPublic: true) as IList));
                if (fieldType.IsArray)
                {
                    Array array = Array.CreateInstance(type, list.Count);
                    list.CopyTo(array, 0);
                    list = array;
                }
                GUI.changed = true;
            }
            else
            {
                list = (IList)value;
            }
            EditorGUILayout.BeginVertical();
            if (DrawFoldout(guiContent.text.GetHashCode(), guiContent))
            {
                EditorGUI.indentLevel++;
                bool flag = fieldInfo.GetHashCode() + (value?.GetHashCode() ?? 0) == editingFieldHash;
                int num = ((!flag) ? list.Count : savedArraySize);
                int num2 = EditorGUILayout.IntField("Size", num);
                if (flag && editingArray && (GUIUtility.keyboardControl != currentKeyboardControl || Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
                {
                    if (num2 != list.Count)
                    {
                        Array array2 = Array.CreateInstance(type, num2);
                        int num3 = -1;
                        for (int i = 0; i < num2; i++)
                        {
                            if (i < list.Count)
                            {
                                num3 = i;
                            }
                            if (num3 == -1)
                            {
                                break;
                            }
                            object value2 = list[num3];
                            if (i >= list.Count && !typeof(UnityEngine.Object).IsAssignableFrom(type) && !typeof(string).IsAssignableFrom(type))
                            {
                                value2 = Activator.CreateInstance(list[num3].GetType(), nonPublic: true);
                            }
                            array2.SetValue(value2, i);
                        }
                        if (fieldType.IsArray)
                        {
                            list = array2;
                        }
                        else
                        {
                            list = ((!fieldType.IsGenericType) ? (Activator.CreateInstance(fieldType, nonPublic: true) as IList) : (Activator.CreateInstance(typeof(List<>).MakeGenericType(type), nonPublic: true) as IList));
                            for (int j = 0; j < array2.Length; j++)
                            {
                                list.Add(array2.GetValue(j));
                            }
                        }
                    }
                    editingArray = false;
                    savedArraySize = -1;
                    editingFieldHash = -1;
                    GUI.changed = true;
                }
                else if (num2 != num)
                {
                    if (!editingArray)
                    {
                        currentKeyboardControl = GUIUtility.keyboardControl;
                        editingArray = true;
                        editingFieldHash = fieldInfo.GetHashCode() + (value?.GetHashCode() ?? 0);
                    }
                    savedArraySize = num2;
                }
                for (int k = 0; k < list.Count; k++)
                {
                    GUILayout.BeginHorizontal();
                    guiContent.text = "Element " + k;
                    list[k] = DrawField(task, guiContent, fieldInfo, type, list[k]);
                    GUILayout.Space(6f);
                    GUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            return list;
        }

        private static object DrawSingleField(Task task, GUIContent guiContent, FieldInfo fieldInfo, Type fieldType, object value)
        {
            if (fieldType.Equals(typeof(int)))
            {
                return EditorGUILayout.IntField(guiContent, (int)value);
            }
            if (fieldType.Equals(typeof(float)))
            {
                return EditorGUILayout.FloatField(guiContent, (float)value);
            }
            if (fieldType.Equals(typeof(double)))
            {
                return EditorGUILayout.FloatField(guiContent, Convert.ToSingle((double)value));
            }
            if (fieldType.Equals(typeof(long)))
            {
                return (long)EditorGUILayout.IntField(guiContent, Convert.ToInt32((long)value));
            }
            if (fieldType.Equals(typeof(bool)))
            {
                return EditorGUILayout.Toggle(guiContent, (bool)value);
            }
            if (fieldType.Equals(typeof(string)))
            {
                return EditorGUILayout.TextField(guiContent, (string)value);
            }
            if (fieldType.Equals(typeof(byte)))
            {
                return Convert.ToByte(EditorGUILayout.IntField(guiContent, Convert.ToInt32(value)));
            }
            if (fieldType.Equals(typeof(uint)))
            {
                int num = EditorGUILayout.IntField(guiContent, Convert.ToInt32(value));
                if (num < 0)
                {
                    num = 0;
                }
                return Convert.ToUInt32(num);
            }
            if (fieldType.Equals(typeof(ushort)))
            {
                int num2 = EditorGUILayout.IntField(guiContent, Convert.ToUInt16(value));
                if (num2 < 0)
                {
                    num2 = 0;
                }
                return Convert.ToUInt16(num2);
            }
            if (fieldType.Equals(typeof(Vector2)))
            {
                return EditorGUILayout.Vector2Field(guiContent, (Vector2)value);
            }
            if (fieldType.Equals(typeof(Vector2Int)))
            {
                return EditorGUILayout.Vector2IntField(guiContent, (Vector2Int)value);
            }
            if (fieldType.Equals(typeof(Vector3)))
            {
                return EditorGUILayout.Vector3Field(guiContent, (Vector3)value);
            }
            if (fieldType.Equals(typeof(Vector3Int)))
            {
                return EditorGUILayout.Vector3IntField(guiContent, (Vector3Int)value);
            }
            if (fieldType.Equals(typeof(Vector3)))
            {
                return EditorGUILayout.Vector3Field(guiContent, (Vector3)value);
            }
            if (fieldType.Equals(typeof(Vector4)))
            {
                return EditorGUILayout.Vector4Field(guiContent.text, (Vector4)value);
            }
            if (fieldType.Equals(typeof(Quaternion)))
            {
                Quaternion quaternion = (Quaternion)value;
                Vector4 zero = Vector4.zero;
                zero.Set(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
                zero = EditorGUILayout.Vector4Field(guiContent.text, zero);
                quaternion.Set(zero.x, zero.y, zero.z, zero.w);
                return quaternion;
            }
            if (fieldType.Equals(typeof(Color)))
            {
                return EditorGUILayout.ColorField(guiContent, (Color)value);
            }
            if (fieldType.Equals(typeof(Rect)))
            {
                return EditorGUILayout.RectField(guiContent, (Rect)value);
            }
            if (fieldType.Equals(typeof(Matrix4x4)))
            {
                GUILayout.BeginVertical();
                if (DrawFoldout(guiContent.text.GetHashCode(), guiContent))
                {
                    EditorGUI.indentLevel++;
                    Matrix4x4 matrix4x = (Matrix4x4)value;
                    for (int i = 0; i < 4; i++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            EditorGUI.BeginChangeCheck();
                            matrix4x[i, j] = EditorGUILayout.FloatField("E" + i + j, matrix4x[i, j]);
                            if (EditorGUI.EndChangeCheck())
                            {
                                GUI.changed = true;
                            }
                        }
                    }
                    value = matrix4x;
                    EditorGUI.indentLevel--;
                }
                GUILayout.EndVertical();
                return value;
            }
            if (fieldType.Equals(typeof(AnimationCurve)))
            {
                if (value == null)
                {
                    value = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                    GUI.changed = true;
                }
                return EditorGUILayout.CurveField(guiContent, (AnimationCurve)value);
            }
            if (fieldType.Equals(typeof(LayerMask)))
            {
                return DrawLayerMask(guiContent, (LayerMask)value);
            }
            if (typeof(SharedVariable).IsAssignableFrom(fieldType))
            {
                return DrawSharedVariable(task, guiContent, fieldInfo, fieldType, value as SharedVariable);
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                return EditorGUILayout.ObjectField(guiContent, (UnityEngine.Object)value, fieldType, true);
            }
            if (fieldType.IsEnum)
            {
                return EditorGUILayout.EnumPopup(guiContent, (Enum)value);
            }
            if (fieldType.IsClass || (fieldType.IsValueType && !fieldType.IsPrimitive))
            {
                if (typeof(Delegate).IsAssignableFrom(fieldType))
                {
                    return null;
                }
                int hashCode = guiContent.text.GetHashCode();
                if (drawnObjects.Contains(hashCode))
                {
                    return null;
                }
                try
                {
                    drawnObjects.Add(hashCode);
                    GUILayout.BeginVertical();
                    if (value == null)
                    {
                        if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        {
                            fieldType = Nullable.GetUnderlyingType(fieldType);
                        }
                        value = Activator.CreateInstance(fieldType, nonPublic: true);
                    }
                    if (DrawFoldout(hashCode, guiContent))
                    {
                        EditorGUI.indentLevel++;
                        value = DrawFields(task, value);
                        EditorGUI.indentLevel--;
                    }
                    drawnObjects.Remove(hashCode);
                    GUILayout.EndVertical();
                }
                catch (Exception)
                {
                    GUILayout.EndVertical();
                    drawnObjects.Remove(hashCode);
                }
                return value;
            }
            EditorGUILayout.LabelField("Unsupported Type: " + fieldType);
            return null;
        }

        public static SharedVariable DrawSharedVariable(Task task, GUIContent guiContent, FieldInfo fieldInfo, Type fieldType, SharedVariable sharedVariable)
        {
            if (!fieldType.Equals(typeof(SharedVariable)))
            {
                if (sharedVariable == null)
                {
                    sharedVariable = Activator.CreateInstance(fieldType, nonPublic: true) as SharedVariable;
                    GUI.changed = true;
                }
                if (!sharedVariable.IsShared && (TaskUtility.HasAttribute(fieldInfo, typeof(RequiredFieldAttribute)) || TaskUtility.HasAttribute(fieldInfo, typeof(SharedRequiredAttribute))))
                {
                    sharedVariable.IsShared = true;
                    GUI.changed = true;
                }
            }
            if (sharedVariable != null && sharedVariable.IsDynamic)
            {
                sharedVariable.Name = EditorGUILayout.TextField(guiContent, sharedVariable.Name);
                sharedVariable = DrawSharedVariableToggleSharedButton(sharedVariable);
                if (!sharedVariable.IsDynamic && (TaskUtility.HasAttribute(fieldInfo, typeof(RequiredFieldAttribute)) || TaskUtility.HasAttribute(fieldInfo, typeof(SharedRequiredAttribute))))
                {
                    sharedVariable = null;
                }
            }
            else if (sharedVariable == null || sharedVariable.IsShared)
            {
                GUILayout.BeginHorizontal();
                string[] names = null;
                int globalStartIndex = -1;
                bool flag = !fieldType.Equals(typeof(SharedVariable));
                int variablesOfType = GetVariablesOfType(sharedVariable?.GetType().GetProperty("Value").PropertyType, sharedVariable?.IsGlobal ?? false, (sharedVariable == null) ? string.Empty : sharedVariable.Name, behaviorSource, out names, ref globalStartIndex, fieldType.Equals(typeof(SharedVariable)), flag);
                Color backgroundColor = GUI.backgroundColor;
                if (variablesOfType == 0 && !TaskUtility.HasAttribute(fieldInfo, typeof(SharedRequiredAttribute)))
                {
                    GUI.backgroundColor = Color.red;
                }
                int num = variablesOfType;
                variablesOfType = EditorGUILayout.Popup(guiContent.text, variablesOfType, names, BehaviorDesignerUtility.SharedVariableToolbarPopup);
                GUI.backgroundColor = backgroundColor;
                if (variablesOfType != num)
                {
                    if (variablesOfType == 0)
                    {
                        if (fieldType.Equals(typeof(SharedVariable)))
                        {
                            sharedVariable = null;
                        }
                        else
                        {
                            sharedVariable = Activator.CreateInstance(fieldType, nonPublic: true) as SharedVariable;
                            sharedVariable.IsShared = true;
                        }
                    }
                    else if (variablesOfType < names.Length - (flag ? 1 : 0))
                    {
                        sharedVariable = ((globalStartIndex == -1 || variablesOfType < globalStartIndex) ? behaviorSource.GetVariable(names[variablesOfType]) : GlobalVariables.Instance.GetVariable(names[variablesOfType].Substring(8, names[variablesOfType].Length - 8)));
                    }
                    else
                    {
                        sharedVariable = Activator.CreateInstance(fieldType, nonPublic: true) as SharedVariable;
                        sharedVariable.IsShared = true;
                        sharedVariable.IsDynamic = true;
                    }
                    GUI.changed = true;
                }
                if (!fieldType.Equals(typeof(SharedVariable)) && !TaskUtility.HasAttribute(fieldInfo, typeof(RequiredFieldAttribute)) && !TaskUtility.HasAttribute(fieldInfo, typeof(SharedRequiredAttribute)))
                {
                    sharedVariable = DrawSharedVariableToggleSharedButton(sharedVariable);
                    GUILayout.Space(-3f);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(3f);
            }
            else
            {
                GUILayout.BeginHorizontal();
                ObjectDrawer objectDrawer = null;
                ObjectDrawerAttribute[] array = null;
                if (fieldInfo != null && (array = fieldInfo.GetCustomAttributes(typeof(ObjectDrawerAttribute), inherit: true) as ObjectDrawerAttribute[]).Length > 0 && (objectDrawer = ObjectDrawerUtility.GetObjectDrawer(task, fieldInfo, array[0])) != null)
                {
                    objectDrawer.Value = sharedVariable;
                    objectDrawer.OnGUI(guiContent);
                }
                else
                {
                    DrawFields(task, sharedVariable, guiContent);
                }
                if (!TaskUtility.HasAttribute(fieldInfo, typeof(RequiredFieldAttribute)) && !TaskUtility.HasAttribute(fieldInfo, typeof(SharedRequiredAttribute)))
                {
                    sharedVariable = DrawSharedVariableToggleSharedButton(sharedVariable);
                }
                GUILayout.EndHorizontal();
            }
            return sharedVariable;
        }

        public static int GetVariablesOfType(Type valueType, bool isGlobal, string name, BehaviorSource behaviorSource, out string[] names, ref int globalStartIndex, bool getAll, bool addDynamic)
        {
            if (behaviorSource == null)
            {
                names = new string[0];
                return 0;
            }
            List<SharedVariable> variables = behaviorSource.Variables;
            int result = 0;
            List<string> list = new List<string>();
            list.Add("(None)");
            if (variables != null)
            {
                for (int i = 0; i < variables.Count; i++)
                {
                    if (variables[i] == null)
                    {
                        continue;
                    }
                    Type propertyType = variables[i].GetType().GetProperty("Value").PropertyType;
                    if (valueType == null || getAll || valueType.IsAssignableFrom(propertyType))
                    {
                        list.Add(variables[i].Name);
                        if (!isGlobal && variables[i].Name.Equals(name))
                        {
                            result = list.Count - 1;
                        }
                    }
                }
            }
            GlobalVariables globalVariables = null;
            if ((globalVariables = GlobalVariables.Instance) != null)
            {
                globalStartIndex = list.Count;
                variables = globalVariables.Variables;
                if (variables != null)
                {
                    for (int j = 0; j < variables.Count; j++)
                    {
                        if (variables[j] == null)
                        {
                            continue;
                        }
                        Type propertyType2 = variables[j].GetType().GetProperty("Value").PropertyType;
                        if (valueType == null || getAll || propertyType2.Equals(valueType))
                        {
                            list.Add("Globals/" + variables[j].Name);
                            if (isGlobal && variables[j].Name.Equals(name))
                            {
                                result = list.Count - 1;
                            }
                        }
                    }
                }
            }
            if (addDynamic)
            {
                list.Add("(Dynamic)");
            }
            names = list.ToArray();
            return result;
        }

        internal static SharedVariable DrawSharedVariableToggleSharedButton(SharedVariable sharedVariable)
        {
            if (sharedVariable == null)
            {
                return null;
            }
            if (GUILayout.Button((!sharedVariable.IsShared) ? BehaviorDesignerUtility.VariableButtonTexture : BehaviorDesignerUtility.VariableButtonSelectedTexture, BehaviorDesignerUtility.PlainButtonGUIStyle, GUILayout.Width(15f)))
            {
                bool flag = !sharedVariable.IsShared;
                sharedVariable = ((!sharedVariable.GetType().Equals(typeof(SharedVariable))) ? (Activator.CreateInstance(sharedVariable.GetType(), nonPublic: true) as SharedVariable) : (Activator.CreateInstance(FriendlySharedVariableName(sharedVariable.GetType().GetProperty("Value").PropertyType), nonPublic: true) as SharedVariable));
                sharedVariable.IsShared = flag;
                if (!flag)
                {
                    sharedVariable.IsDynamic = false;
                }
            }
            return sharedVariable;
        }

        internal static Type FriendlySharedVariableName(Type type)
        {
            if (type.Equals(typeof(bool)))
            {
                return TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.SharedBool");
            }
            if (type.Equals(typeof(int)))
            {
                return TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.SharedInt");
            }
            if (type.Equals(typeof(float)))
            {
                return TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.SharedFloat");
            }
            if (type.Equals(typeof(string)))
            {
                return TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.SharedString");
            }
            Type typeWithinAssembly = TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.Shared" + type.Name);
            if (typeWithinAssembly != null)
            {
                return typeWithinAssembly;
            }
            typeWithinAssembly = TaskUtility.GetTypeWithinAssembly("Shared" + type.Name);
            if (typeWithinAssembly != null)
            {
                return typeWithinAssembly;
            }
            return type;
        }

        private static LayerMask DrawLayerMask(GUIContent guiContent, LayerMask layerMask)
        {
            if (layerNames == null)
            {
                InitLayers();
            }
            int num = 0;
            for (int i = 0; i < layerNames.Length; i++)
            {
                if ((layerMask.value & maskValues[i]) == maskValues[i])
                {
                    num |= 1 << i;
                }
            }
            int num2 = EditorGUILayout.MaskField(guiContent, num, layerNames);
            if (num2 != num)
            {
                num = 0;
                for (int j = 0; j < layerNames.Length; j++)
                {
                    if ((num2 & (1 << j)) != 0)
                    {
                        num |= maskValues[j];
                    }
                }
                layerMask.value = num;
            }
            return layerMask;
        }

        private static void InitLayers()
        {
            List<string> list = new List<string>();
            List<int> list2 = new List<int>();
            for (int i = 0; i < 32; i++)
            {
                string text = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(text))
                {
                    list.Add(text);
                    list2.Add(1 << i);
                }
            }
            layerNames = list.ToArray();
            maskValues = list2.ToArray();
        }
    }
}