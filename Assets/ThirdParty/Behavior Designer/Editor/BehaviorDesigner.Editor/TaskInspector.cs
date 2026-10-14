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
    [Serializable]
    public class TaskInspector : ScriptableObject
    {
        private class TaskColor
        {
            public Task task;

            public int colorIndex;

            public TaskColor(Task task, int colorIndex)
            {
                this.task = task;
                this.colorIndex = colorIndex;
            }
        }

        private BehaviorDesignerWindow behaviorDesignerWindow;

        private Task activeReferenceTask;

        private FieldInfo activeReferenceTaskFieldInfo;

        private Task mActiveMenuSelectionTask;

        private Vector2 mScrollPosition = Vector2.zero;

        public Task ActiveReferenceTask => activeReferenceTask;

        public FieldInfo ActiveReferenceTaskFieldInfo => activeReferenceTaskFieldInfo;

        public void OnEnable()
        {
            base.hideFlags = HideFlags.HideAndDontSave;
        }

        public void ClearFocus()
        {
            GUIUtility.keyboardControl = 0;
        }

        public bool HasFocus()
        {
            return GUIUtility.keyboardControl != 0;
        }

        public bool DrawTaskInspector(BehaviorSource behaviorSource, TaskList taskList, Task task, bool enabled)
        {
            if (task == null || (task.NodeData.NodeDesigner as NodeDesigner).IsEntryDisplay)
            {
                return false;
            }
            mScrollPosition = GUILayout.BeginScrollView(mScrollPosition);
            GUI.enabled = enabled;
            if (behaviorDesignerWindow == null)
            {
                behaviorDesignerWindow = BehaviorDesignerWindow.instance;
            }
            GUILayout.Space(6f);
            EditorGUIUtility.labelWidth = 150f;
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Name", GUILayout.Width(90f));
            task.FriendlyName = EditorGUILayout.TextField(task.FriendlyName);
            if (GUILayout.Button(BehaviorDesignerUtility.DocTexture, BehaviorDesignerUtility.TransparentButtonOffsetGUIStyle))
            {
                OpenHelpURL(task);
            }
            if (GUILayout.Button(BehaviorDesignerUtility.ColorSelectorTexture(task.NodeData.ColorIndex), BehaviorDesignerUtility.TransparentButtonOffsetGUIStyle))
            {
                GenericMenu menu = new GenericMenu();
                AddColorMenuItem(ref menu, task, "Default", 0);
                AddColorMenuItem(ref menu, task, "Red", 1);
                AddColorMenuItem(ref menu, task, "Pink", 2);
                AddColorMenuItem(ref menu, task, "Brown", 3);
                AddColorMenuItem(ref menu, task, "Orange", 4);
                AddColorMenuItem(ref menu, task, "Turquoise", 5);
                AddColorMenuItem(ref menu, task, "Cyan", 6);
                AddColorMenuItem(ref menu, task, "Blue", 7);
                AddColorMenuItem(ref menu, task, "Purple", 8);
                menu.ShowAsContext();
            }
            if (GUILayout.Button(BehaviorDesignerUtility.GearTexture, BehaviorDesignerUtility.TransparentButtonOffsetGUIStyle))
            {
                GenericMenu genericMenu = new GenericMenu();
                genericMenu.AddItem(new GUIContent("Edit Script"), on: false, OpenInFileEditor, task);
                genericMenu.AddItem(new GUIContent("Locate Script"), on: false, SelectInProject, task);
                genericMenu.AddItem(new GUIContent("Reset"), on: false, ResetTask, task);
                genericMenu.ShowAsContext();
            }
            GUILayout.EndHorizontal();
            string text = BehaviorDesignerUtility.SplitCamelCase(task.GetType().Name.ToString());
            if (!task.FriendlyName.Equals(text))
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Type", GUILayout.Width(90f));
                EditorGUILayout.LabelField(text, GUILayout.MaxWidth(170f));
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Instant", GUILayout.Width(90f));
            task.IsInstant = EditorGUILayout.Toggle(task.IsInstant);
            GUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Comment");
            task.NodeData.Comment = EditorGUILayout.TextArea(task.NodeData.Comment, BehaviorDesignerUtility.TaskInspectorCommentGUIStyle, GUILayout.Height(48f));
            if (EditorGUI.EndChangeCheck())
            {
                BehaviorUndo.RegisterUndo("Inspector", behaviorSource.Owner.GetObject());
                GUI.changed = true;
            }
            BehaviorDesignerUtility.DrawContentSeperator(2);
            GUILayout.Space(6f);
            if (DrawTaskFields(behaviorSource, taskList, task, enabled))
            {
                BehaviorUndo.RegisterUndo("Inspector", behaviorSource.Owner.GetObject());
                GUI.changed = true;
            }
            GUI.enabled = true;
            GUILayout.EndScrollView();
            return GUI.changed;
        }

        private bool DrawTaskFields(BehaviorSource behaviorSource, TaskList taskList, Task task, bool enabled)
        {
            if (task == null)
            {
                return false;
            }
            EditorGUI.BeginChangeCheck();
            FieldInspector.behaviorSource = behaviorSource;
            DrawObjectFields(behaviorSource, taskList, task, task, enabled, drawWatch: true);
            if (EditorGUI.EndChangeCheck())
            {
                return true;
            }
            return false;
        }

        private void DrawObjectFields(BehaviorSource behaviorSource, TaskList taskList, Task task, object obj, bool enabled, bool drawWatch)
        {
            if (obj == null)
            {
                return;
            }
            ObjectDrawer objectDrawer;
            if ((objectDrawer = ObjectDrawerUtility.GetObjectDrawer(task)) != null)
            {
                objectDrawer.OnGUI(new GUIContent());
                return;
            }
            List<Type> baseClasses = FieldInspector.GetBaseClasses(obj.GetType());
            BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            bool flag = IsReflectionTask(obj.GetType());
            for (int num = baseClasses.Count - 1; num > -1; num--)
            {
                FieldInfo[] fields = baseClasses[num].GetFields(bindingAttr);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (BehaviorDesignerUtility.HasAttribute(fields[i], typeof(NonSerializedAttribute)) || BehaviorDesignerUtility.HasAttribute(fields[i], typeof(HideInInspector)) || ((fields[i].IsPrivate || fields[i].IsFamily) && !BehaviorDesignerUtility.HasAttribute(fields[i], typeof(SerializeField))) || (obj is ParentTask && fields[i].Name.Equals("children")) || (flag && (fields[i].FieldType.Equals(typeof(SharedVariable)) || fields[i].FieldType.IsSubclassOf(typeof(SharedVariable))) && !CanDrawReflectedField(obj, fields[i])))
                    {
                        continue;
                    }
                    HeaderAttribute[] array;
                    if ((array = fields[i].GetCustomAttributes(typeof(HeaderAttribute), inherit: true) as HeaderAttribute[]).Length > 0)
                    {
                        EditorGUILayout.LabelField(array[0].header, BehaviorDesignerUtility.BoldLabelGUIStyle);
                    }
                    SpaceAttribute[] array2;
                    if ((array2 = fields[i].GetCustomAttributes(typeof(SpaceAttribute), inherit: true) as SpaceAttribute[]).Length > 0)
                    {
                        GUILayout.Space(array2[0].height);
                    }
                    GUIContent gUIContent = null;
                    BehaviorDesigner.Runtime.Tasks.TooltipAttribute[] array3 = null;
                    string s = fields[i].Name;
                    if (flag && (fields[i].FieldType.Equals(typeof(SharedVariable)) || fields[i].FieldType.IsSubclassOf(typeof(SharedVariable))))
                    {
                        s = InvokeParameterName(obj, fields[i]);
                    }
                    gUIContent = (((array3 = fields[i].GetCustomAttributes(typeof(BehaviorDesigner.Runtime.Tasks.TooltipAttribute), inherit: false) as BehaviorDesigner.Runtime.Tasks.TooltipAttribute[]).Length <= 0) ? new GUIContent(BehaviorDesignerUtility.SplitCamelCase(s)) : new GUIContent(BehaviorDesignerUtility.SplitCamelCase(s), array3[0].Tooltip));
                    object value = fields[i].GetValue(obj);
                    Type fieldType = fields[i].FieldType;
                    if (typeof(Task).IsAssignableFrom(fieldType) || (typeof(IList).IsAssignableFrom(fieldType) && (typeof(Task).IsAssignableFrom(fieldType.GetElementType()) || (fieldType.IsGenericType && typeof(Task).IsAssignableFrom(fieldType.GetGenericArguments()[0])))))
                    {
                        EditorGUI.BeginChangeCheck();
                        DrawTaskValue(behaviorSource, taskList, fields[i], gUIContent, task, value as Task, enabled);
                        if (BehaviorDesignerWindow.instance.ContainsError(task, fields[i].Name))
                        {
                            GUILayout.Space(-3f);
                            GUILayout.Box(BehaviorDesignerUtility.ErrorIconTexture, BehaviorDesignerUtility.PlainTextureGUIStyle, GUILayout.Width(20f));
                        }
                        if (EditorGUI.EndChangeCheck())
                        {
                            GUI.changed = true;
                        }
                        continue;
                    }
                    if (fieldType.Equals(typeof(SharedVariable)) || fieldType.IsSubclassOf(typeof(SharedVariable)))
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUI.BeginChangeCheck();
                        if (drawWatch)
                        {
                            DrawWatchedButton(task, fields[i]);
                        }
                        SharedVariable value2 = DrawSharedVariableValue(behaviorSource, fields[i], gUIContent, task, value as SharedVariable, flag, enabled, drawWatch);
                        if (BehaviorDesignerWindow.instance.ContainsError(task, fields[i].Name))
                        {
                            GUILayout.Space(-3f);
                            GUILayout.Box(BehaviorDesignerUtility.ErrorIconTexture, BehaviorDesignerUtility.PlainTextureGUIStyle, GUILayout.Width(20f));
                        }
                        GUILayout.EndHorizontal();
                        GUILayout.Space(4f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            fields[i].SetValue(obj, value2);
                            GUI.changed = true;
                        }
                        continue;
                    }
                    GUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    if (drawWatch)
                    {
                        DrawWatchedButton(task, fields[i]);
                    }
                    object value3 = FieldInspector.DrawField(task, gUIContent, fields[i], value);
                    if (BehaviorDesignerWindow.instance.ContainsError(task, fields[i].Name))
                    {
                        GUILayout.Space(-3f);
                        GUILayout.Box(BehaviorDesignerUtility.ErrorIconTexture, BehaviorDesignerUtility.PlainTextureGUIStyle, GUILayout.Width(20f));
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        fields[i].SetValue(obj, value3);
                        GUI.changed = true;
                    }
                    if (TaskUtility.HasAttribute(fields[i], typeof(RequiredFieldAttribute)) && !ErrorCheck.IsRequiredFieldValid(fieldType, value))
                    {
                        GUILayout.Space(-3f);
                        GUILayout.Box(BehaviorDesignerUtility.ErrorIconTexture, BehaviorDesignerUtility.PlainTextureGUIStyle, GUILayout.Width(20f));
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4f);
                }
            }
        }

        private bool DrawWatchedButton(Task task, FieldInfo field)
        {
            GUILayout.Space(3f);
            bool flag = task.NodeData.GetWatchedFieldIndex(field) != -1;
            if (GUILayout.Button((!flag) ? BehaviorDesignerUtility.VariableWatchButtonTexture : BehaviorDesignerUtility.VariableWatchButtonSelectedTexture, BehaviorDesignerUtility.PlainButtonGUIStyle, GUILayout.Width(15f)))
            {
                if (flag)
                {
                    task.NodeData.RemoveWatchedField(field);
                }
                else
                {
                    task.NodeData.AddWatchedField(field);
                }
                return true;
            }
            return false;
        }

        private void DrawTaskValue(BehaviorSource behaviorSource, TaskList taskList, FieldInfo field, GUIContent guiContent, Task parentTask, Task task, bool enabled)
        {
            if (BehaviorDesignerUtility.HasAttribute(field, typeof(InspectTaskAttribute)))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(guiContent, GUILayout.Width(144f));
                if (GUILayout.Button((task == null) ? "Select" : BehaviorDesignerUtility.SplitCamelCase(task.GetType().Name.ToString()), EditorStyles.toolbarPopup, GUILayout.Width(134f)))
                {
                    GenericMenu genericMenu = new GenericMenu();
                    genericMenu.AddItem(new GUIContent("None"), task == null, InspectedTaskCallback, null);
                    taskList.AddTaskTypesToMenu(2, ref genericMenu, task?.GetType(), null, string.Empty, includeFullPath: true, InspectedTaskCallback);
                    genericMenu.ShowAsContext();
                    mActiveMenuSelectionTask = parentTask;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(2f);
                DrawObjectFields(behaviorSource, taskList, task, task, enabled, drawWatch: false);
                return;
            }
            GUILayout.BeginHorizontal();
            DrawWatchedButton(parentTask, field);
            GUILayout.Label(guiContent, BehaviorDesignerUtility.TaskInspectorGUIStyle, GUILayout.Width(165f));
            bool flag = behaviorDesignerWindow.IsReferencingField(field);
            Color backgroundColor = GUI.backgroundColor;
            if (flag)
            {
                GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
            }
            if (GUILayout.Button((!flag) ? "Select" : "Done", EditorStyles.miniButtonMid, GUILayout.Width(80f)))
            {
                if (behaviorDesignerWindow.IsReferencingTasks() && !flag)
                {
                    behaviorDesignerWindow.ToggleReferenceTasks();
                }
                behaviorDesignerWindow.ToggleReferenceTasks(parentTask, field);
            }
            GUI.backgroundColor = backgroundColor;
            EditorGUILayout.EndHorizontal();
            if (typeof(IList).IsAssignableFrom(field.FieldType))
            {
                if (!(field.GetValue(parentTask) is IList list) || list.Count == 0)
                {
                    GUILayout.Label("No Tasks Referenced", BehaviorDesignerUtility.TaskInspectorGUIStyle);
                    return;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is Task)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label((list[i] as Task).NodeData.NodeDesigner.ToString(), BehaviorDesignerUtility.TaskInspectorGUIStyle, GUILayout.Width(232f));
                        if (GUILayout.Button(BehaviorDesignerUtility.DeleteButtonTexture, BehaviorDesignerUtility.PlainButtonGUIStyle, GUILayout.Width(14f)))
                        {
                            ReferenceTasks(parentTask, ((list[i] as Task).NodeData.NodeDesigner as NodeDesigner).Task, field);
                            GUI.changed = true;
                        }
                        GUILayout.Space(3f);
                        if (GUILayout.Button(BehaviorDesignerUtility.IdentifyButtonTexture, BehaviorDesignerUtility.PlainButtonGUIStyle, GUILayout.Width(14f)))
                        {
                            behaviorDesignerWindow.IdentifyNode((list[i] as Task).NodeData.NodeDesigner as NodeDesigner);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                return;
            }
            EditorGUILayout.BeginHorizontal();
            Task task2 = field.GetValue(parentTask) as Task;
            GUILayout.Label((task2 == null) ? "No Tasks Referenced" : task2.NodeData.NodeDesigner.ToString(), BehaviorDesignerUtility.TaskInspectorGUIStyle, GUILayout.Width(232f));
            if (task2 != null)
            {
                if (GUILayout.Button(BehaviorDesignerUtility.DeleteButtonTexture, BehaviorDesignerUtility.PlainButtonGUIStyle, GUILayout.Width(14f)))
                {
                    ReferenceTasks(parentTask, null, field);
                    GUI.changed = true;
                }
                GUILayout.Space(3f);
                if (GUILayout.Button(BehaviorDesignerUtility.IdentifyButtonTexture, BehaviorDesignerUtility.PlainButtonGUIStyle, GUILayout.Width(14f)))
                {
                    behaviorDesignerWindow.IdentifyNode(task2.NodeData.NodeDesigner as NodeDesigner);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private SharedVariable DrawSharedVariableValue(BehaviorSource behaviorSource, FieldInfo field, GUIContent guiContent, Task task, SharedVariable sharedVariable, bool isReflectionTask, bool enabled, bool drawWatch)
        {
            if (isReflectionTask)
            {
                if (!field.FieldType.Equals(typeof(SharedVariable)) && sharedVariable == null)
                {
                    sharedVariable = Activator.CreateInstance(field.FieldType) as SharedVariable;
                    if (TaskUtility.HasAttribute(field, typeof(RequiredFieldAttribute)) || TaskUtility.HasAttribute(field, typeof(SharedRequiredAttribute)))
                    {
                        sharedVariable.IsShared = true;
                    }
                    GUI.changed = true;
                }
                if (sharedVariable == null)
                {
                    mActiveMenuSelectionTask = task;
                    SecondaryReflectionSelectionCallback(null);
                    ClearInvokeVariablesTask();
                    return null;
                }
                if (sharedVariable.IsShared)
                {
                    GUILayout.Label(guiContent, GUILayout.Width(126f));
                    string[] names = null;
                    int globalStartIndex = -1;
                    int variablesOfType = FieldInspector.GetVariablesOfType(sharedVariable.GetType().GetProperty("Value").PropertyType, sharedVariable.IsGlobal, sharedVariable.Name, behaviorSource, out names, ref globalStartIndex, getAll: false, addDynamic: true);
                    Color backgroundColor = GUI.backgroundColor;
                    if (variablesOfType == 0 && !TaskUtility.HasAttribute(field, typeof(SharedRequiredAttribute)))
                    {
                        GUI.backgroundColor = Color.red;
                    }
                    int num = variablesOfType;
                    variablesOfType = EditorGUILayout.Popup(variablesOfType, names, EditorStyles.toolbarPopup);
                    GUI.backgroundColor = backgroundColor;
                    if (variablesOfType != num)
                    {
                        if (variablesOfType != 0)
                        {
                            sharedVariable = ((globalStartIndex == -1 || variablesOfType < globalStartIndex) ? behaviorSource.GetVariable(names[variablesOfType]) : GlobalVariables.Instance.GetVariable(names[variablesOfType].Substring(8, names[variablesOfType].Length - 8)));
                        }
                        else
                        {
                            sharedVariable = ((!field.FieldType.Equals(typeof(SharedVariable))) ? (Activator.CreateInstance(field.FieldType) as SharedVariable) : (Activator.CreateInstance(FieldInspector.FriendlySharedVariableName(sharedVariable.GetType().GetProperty("Value").PropertyType)) as SharedVariable));
                            sharedVariable.IsShared = true;
                        }
                    }
                    GUILayout.Space(8f);
                }
                else
                {
                    bool flag = false;
                    if ((flag = field.Name.Equals("componentName")) || field.Name.Equals("methodName") || field.Name.Equals("fieldName") || field.Name.Equals("propertyName"))
                    {
                        DrawReflectionField(task, guiContent, flag, field);
                    }
                    else
                    {
                        FieldInspector.DrawFields(task, sharedVariable, guiContent);
                    }
                }
                if (!TaskUtility.HasAttribute(field, typeof(RequiredFieldAttribute)) && !TaskUtility.HasAttribute(field, typeof(SharedRequiredAttribute)))
                {
                    sharedVariable = FieldInspector.DrawSharedVariableToggleSharedButton(sharedVariable);
                }
                else if (!sharedVariable.IsShared)
                {
                    sharedVariable.IsShared = true;
                }
            }
            else
            {
                sharedVariable = FieldInspector.DrawSharedVariable(task, guiContent, field, field.FieldType, sharedVariable);
            }
            GUILayout.Space(8f);
            return sharedVariable;
        }

        private void InspectedTaskCallback(object obj)
        {
            if (mActiveMenuSelectionTask != null)
            {
                FieldInfo field = mActiveMenuSelectionTask.GetType().GetField("conditionalTask");
                if (obj == null)
                {
                    field.SetValue(mActiveMenuSelectionTask, null);
                }
                else
                {
                    Type type = (Type)obj;
                    Task task = Activator.CreateInstance(type, nonPublic: true) as Task;
                    field.SetValue(mActiveMenuSelectionTask, task);
                    FieldInfo[] serializableFields = TaskUtility.GetSerializableFields(type);
                    for (int i = 0; i < serializableFields.Length; i++)
                    {
                        if (serializableFields[i].FieldType.IsSubclassOf(typeof(SharedVariable)) && !BehaviorDesignerUtility.HasAttribute(serializableFields[i], typeof(HideInInspector)) && !BehaviorDesignerUtility.HasAttribute(serializableFields[i], typeof(NonSerializedAttribute)) && ((!serializableFields[i].IsPrivate && !serializableFields[i].IsFamily) || BehaviorDesignerUtility.HasAttribute(serializableFields[i], typeof(SerializeField))))
                        {
                            SharedVariable sharedVariable = Activator.CreateInstance(serializableFields[i].FieldType) as SharedVariable;
                            sharedVariable.IsShared = false;
                            serializableFields[i].SetValue(task, sharedVariable);
                        }
                    }
                }
            }
            BehaviorDesignerWindow.instance.SaveBehavior();
        }

        public void SetActiveReferencedTasks(Task referenceTask, FieldInfo fieldInfo)
        {
            activeReferenceTask = referenceTask;
            activeReferenceTaskFieldInfo = fieldInfo;
        }

        public bool ReferenceTasks(Task referenceTask)
        {
            return ReferenceTasks(activeReferenceTask, referenceTask, activeReferenceTaskFieldInfo);
        }

        private bool ReferenceTasks(Task sourceTask, Task referenceTask, FieldInfo sourceFieldInfo)
        {
            bool fullSync = false;
            bool doReference = false;
            if (ReferenceTasks(sourceTask, referenceTask, sourceFieldInfo, ref fullSync, ref doReference, synchronize: true, unreferenceAll: false))
            {
                if (referenceTask != null)
                {
                    (referenceTask.NodeData.NodeDesigner as NodeDesigner).ShowReferenceIcon = doReference;
                    if (fullSync)
                    {
                        PerformFullSync(activeReferenceTask);
                    }
                }
                return true;
            }
            return false;
        }

        public static bool ReferenceTasks(Task sourceTask, Task referenceTask, FieldInfo sourceFieldInfo, ref bool fullSync, ref bool doReference, bool synchronize, bool unreferenceAll)
        {
            if (referenceTask == null)
            {
                if (sourceFieldInfo.GetValue(sourceTask) is Task task)
                {
                    (task.NodeData.NodeDesigner as NodeDesigner).ShowReferenceIcon = false;
                }
                sourceFieldInfo.SetValue(sourceTask, null);
                return true;
            }
            if (referenceTask.Equals(sourceTask) || sourceFieldInfo == null || (!typeof(IList).IsAssignableFrom(sourceFieldInfo.FieldType) && !sourceFieldInfo.FieldType.IsAssignableFrom(referenceTask.GetType())) || (typeof(IList).IsAssignableFrom(sourceFieldInfo.FieldType) && ((sourceFieldInfo.FieldType.IsGenericType && !sourceFieldInfo.FieldType.GetGenericArguments()[0].IsAssignableFrom(referenceTask.GetType())) || (!sourceFieldInfo.FieldType.IsGenericType && !sourceFieldInfo.FieldType.GetElementType().IsAssignableFrom(referenceTask.GetType())))))
            {
                return false;
            }
            if (synchronize && !IsFieldLinked(sourceFieldInfo))
            {
                synchronize = false;
            }
            if (unreferenceAll)
            {
                sourceFieldInfo.SetValue(sourceTask, null);
                (sourceTask.NodeData.NodeDesigner as NodeDesigner).ShowReferenceIcon = false;
            }
            else
            {
                doReference = true;
                bool fullSync2 = false;
                if (typeof(IList).IsAssignableFrom(sourceFieldInfo.FieldType))
                {
                    Task[] array = sourceFieldInfo.GetValue(sourceTask) as Task[];
                    Type type;
                    if (sourceFieldInfo.FieldType.IsArray)
                    {
                        type = sourceFieldInfo.FieldType.GetElementType();
                    }
                    else
                    {
                        Type type2 = sourceFieldInfo.FieldType;
                        while (!type2.IsGenericType)
                        {
                            type2 = type2.BaseType;
                        }
                        type = type2.GetGenericArguments()[0];
                    }
                    IList list = Activator.CreateInstance(typeof(List<>).MakeGenericType(type)) as IList;
                    if (array != null)
                    {
                        for (int i = 0; i < array.Length; i++)
                        {
                            if (referenceTask.Equals(array[i]))
                            {
                                doReference = false;
                            }
                            else
                            {
                                list.Add(array[i]);
                            }
                        }
                    }
                    if (synchronize)
                    {
                        if (array != null && array.Length > 0)
                        {
                            for (int j = 0; j < array.Length; j++)
                            {
                                ReferenceTasks(array[j], referenceTask, array[j].GetType().GetField(sourceFieldInfo.Name), ref fullSync2, ref doReference, synchronize: false, unreferenceAll: false);
                                if (doReference)
                                {
                                    ReferenceTasks(referenceTask, array[j], referenceTask.GetType().GetField(sourceFieldInfo.Name), ref fullSync2, ref doReference, synchronize: false, unreferenceAll: false);
                                }
                            }
                        }
                        else if (doReference)
                        {
                            FieldInfo field = referenceTask.GetType().GetField(sourceFieldInfo.Name);
                            if (field != null && field.GetValue(referenceTask) is Task[] array2)
                            {
                                for (int k = 0; k < array2.Length; k++)
                                {
                                    list.Add(array2[k]);
                                    (array2[k].NodeData.NodeDesigner as NodeDesigner).ShowReferenceIcon = true;
                                    ReferenceTasks(array2[k], sourceTask, array2[k].GetType().GetField(sourceFieldInfo.Name), ref doReference, ref fullSync2, synchronize: false, unreferenceAll: false);
                                }
                                doReference = true;
                            }
                        }
                        ReferenceTasks(referenceTask, sourceTask, referenceTask.GetType().GetField(sourceFieldInfo.Name), ref fullSync2, ref doReference, synchronize: false, !doReference);
                    }
                    if (doReference)
                    {
                        list.Add(referenceTask);
                    }
                    if (sourceFieldInfo.FieldType.IsArray)
                    {
                        Array array3 = Array.CreateInstance(sourceFieldInfo.FieldType.GetElementType(), list.Count);
                        list.CopyTo(array3, 0);
                        sourceFieldInfo.SetValue(sourceTask, array3);
                    }
                    else
                    {
                        sourceFieldInfo.SetValue(sourceTask, list);
                    }
                }
                else
                {
                    Task task2 = sourceFieldInfo.GetValue(sourceTask) as Task;
                    doReference = !referenceTask.Equals(task2);
                    if (IsFieldLinked(sourceFieldInfo) && task2 != null)
                    {
                        ReferenceTasks(task2, sourceTask, task2.GetType().GetField(sourceFieldInfo.Name), ref fullSync2, ref doReference, synchronize: false, unreferenceAll: true);
                    }
                    if (synchronize)
                    {
                        ReferenceTasks(referenceTask, sourceTask, referenceTask.GetType().GetField(sourceFieldInfo.Name), ref fullSync2, ref doReference, synchronize: false, !doReference);
                    }
                    sourceFieldInfo.SetValue(sourceTask, (!doReference) ? null : referenceTask);
                }
                if (synchronize)
                {
                    (referenceTask.NodeData.NodeDesigner as NodeDesigner).ShowReferenceIcon = doReference;
                }
                fullSync = doReference && synchronize;
            }
            return true;
        }

        public bool IsActiveTaskArray()
        {
            return activeReferenceTaskFieldInfo.FieldType.IsArray;
        }

        public bool IsActiveTaskNull()
        {
            return activeReferenceTaskFieldInfo.GetValue(activeReferenceTask) == null;
        }

        public static bool IsFieldLinked(FieldInfo field)
        {
            return BehaviorDesignerUtility.HasAttribute(field, typeof(LinkedTaskAttribute));
        }

        public static List<Task> GetReferencedTasks(Task task)
        {
            List<Task> list = new List<Task>();
            FieldInfo[] serializableFields = TaskUtility.GetSerializableFields(task.GetType());
            for (int i = 0; i < serializableFields.Length; i++)
            {
                if ((serializableFields[i].IsPrivate || serializableFields[i].IsFamily) && !BehaviorDesignerUtility.HasAttribute(serializableFields[i], typeof(SerializeField)))
                {
                    continue;
                }
                if (typeof(IList).IsAssignableFrom(serializableFields[i].FieldType) && (typeof(Task).IsAssignableFrom(serializableFields[i].FieldType.GetElementType()) || (serializableFields[i].FieldType.IsGenericType && typeof(Task).IsAssignableFrom(serializableFields[i].FieldType.GetGenericArguments()[0]))))
                {
                    if (serializableFields[i].GetValue(task) is Task[] array)
                    {
                        for (int j = 0; j < array.Length; j++)
                        {
                            list.Add(array[j]);
                        }
                    }
                }
                else if (serializableFields[i].FieldType.IsSubclassOf(typeof(Task)) && serializableFields[i].GetValue(task) != null)
                {
                    list.Add(serializableFields[i].GetValue(task) as Task);
                }
            }
            return (list.Count <= 0) ? null : list;
        }

        private void PerformFullSync(Task task)
        {
            List<Task> referencedTasks = GetReferencedTasks(task);
            if (referencedTasks == null)
            {
                return;
            }
            FieldInfo[] serializableFields = TaskUtility.GetSerializableFields(task.GetType());
            for (int i = 0; i < serializableFields.Length; i++)
            {
                if (IsFieldLinked(serializableFields[i]))
                {
                    continue;
                }
                for (int j = 0; j < referencedTasks.Count; j++)
                {
                    FieldInfo field;
                    if ((field = referencedTasks[j].GetType().GetField(serializableFields[i].Name)) != null)
                    {
                        field.SetValue(referencedTasks[j], serializableFields[i].GetValue(task));
                    }
                }
            }
        }

        public static void OpenInFileEditor(object task)
        {
            MonoScript[] array = (MonoScript[])Resources.FindObjectsOfTypeAll(typeof(MonoScript));
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] != null && array[i].GetClass() != null && array[i].GetClass().Equals(task.GetType()))
                {
                    AssetDatabase.OpenAsset(array[i]);
                    break;
                }
            }
        }

        public static void SelectInProject(object task)
        {
            MonoScript[] array = (MonoScript[])Resources.FindObjectsOfTypeAll(typeof(MonoScript));
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] != null && array[i].GetClass() != null && array[i].GetClass().Equals(task.GetType()))
                {
                    Selection.activeObject = array[i];
                    break;
                }
            }
        }

        private void ResetTask(object task)
        {
            (task as Task).OnReset();
            List<Type> baseClasses = FieldInspector.GetBaseClasses(task.GetType());
            BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (int num = baseClasses.Count - 1; num > -1; num--)
            {
                FieldInfo[] fields = baseClasses[num].GetFields(bindingAttr);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (typeof(SharedVariable).IsAssignableFrom(fields[i].FieldType))
                    {
                        SharedVariable sharedVariable = fields[i].GetValue(task) as SharedVariable;
                        if (TaskUtility.HasAttribute(fields[i], typeof(RequiredFieldAttribute)) && sharedVariable != null && !sharedVariable.IsShared)
                        {
                            sharedVariable.IsShared = true;
                        }
                    }
                }
            }
        }

        private void AddColorMenuItem(ref GenericMenu menu, Task task, string color, int index)
        {
            menu.AddItem(new GUIContent(color), task.NodeData.ColorIndex == index, SetTaskColor, new TaskColor(task, index));
        }

        private void SetTaskColor(object value)
        {
            TaskColor taskColor = value as TaskColor;
            if (taskColor.task.NodeData.ColorIndex != taskColor.colorIndex)
            {
                taskColor.task.NodeData.ColorIndex = taskColor.colorIndex;
                BehaviorDesignerWindow.instance.SaveBehavior();
            }
        }

        private void OpenHelpURL(Task task)
        {
            BehaviorDesigner.Runtime.Tasks.HelpURLAttribute[] array = null;
            if ((array = task.GetType().GetCustomAttributes(typeof(BehaviorDesigner.Runtime.Tasks.HelpURLAttribute), inherit: false) as BehaviorDesigner.Runtime.Tasks.HelpURLAttribute[]).Length > 0)
            {
                Application.OpenURL(array[0].URL);
            }
        }

        private bool IsReflectionTask(Type type)
        {
            return IsInvokeMethodTask(type) || IsFieldReflectionTask(type) || IsPropertyReflectionTask(type);
        }

        private bool IsInvokeMethodTask(Type type)
        {
            return TaskUtility.CompareType(type, "BehaviorDesigner.Runtime.Tasks.InvokeMethod");
        }

        private bool IsFieldReflectionTask(Type type)
        {
            return TaskUtility.CompareType(type, "BehaviorDesigner.Runtime.Tasks.GetFieldValue") || TaskUtility.CompareType(type, "BehaviorDesigner.Runtime.Tasks.SetFieldValue") || TaskUtility.CompareType(type, "BehaviorDesigner.Runtime.Tasks.CompareFieldValue");
        }

        private bool IsPropertyReflectionTask(Type type)
        {
            return TaskUtility.CompareType(type, "BehaviorDesigner.Runtime.Tasks.GetPropertyValue") || TaskUtility.CompareType(type, "BehaviorDesigner.Runtime.Tasks.SetPropertyValue") || TaskUtility.CompareType(type, "BehaviorDesigner.Runtime.Tasks.ComparePropertyValue");
        }

        private bool IsReflectionGetterTask(Type type)
        {
            return TaskUtility.CompareType(type, "BehaviorDesigner.Runtime.Tasks.GetFieldValue") || TaskUtility.CompareType(type, "BehaviorDesigner.Runtime.Tasks.GetPropertyValue");
        }

        private void DrawReflectionField(Task task, GUIContent guiContent, bool drawComponentField, FieldInfo field)
        {
            FieldInfo field2 = task.GetType().GetField("targetGameObject");
            SharedVariable sharedVariable = field2.GetValue(task) as SharedVariable;
            if (drawComponentField)
            {
                GUILayout.Label(guiContent, GUILayout.Width(146f));
                SharedVariable sharedVariable2 = field.GetValue(task) as SharedVariable;
                string empty = string.Empty;
                if (sharedVariable2 == null || string.IsNullOrEmpty((string)sharedVariable2.GetValue()))
                {
                    empty = "Select";
                }
                else
                {
                    string text = (string)sharedVariable2.GetValue();
                    string[] array = text.Split('.');
                    empty = array[array.Length - 1];
                }
                if (GUILayout.Button(empty, EditorStyles.toolbarPopup, GUILayout.Width(92f)))
                {
                    GenericMenu genericMenu = new GenericMenu();
                    genericMenu.AddItem(new GUIContent("None"), string.IsNullOrEmpty((string)sharedVariable2.GetValue()), ComponentSelectionCallback, null);
                    GameObject gameObject = null;
                    if (sharedVariable == null || (GameObject)sharedVariable.GetValue() == null)
                    {
                        if (task.Owner != null)
                        {
                            gameObject = task.Owner.gameObject;
                        }
                    }
                    else
                    {
                        gameObject = (GameObject)sharedVariable.GetValue();
                    }
                    if (gameObject != null)
                    {
                        Component[] components = gameObject.GetComponents<Component>();
                        for (int i = 0; i < components.Length; i++)
                        {
                            genericMenu.AddItem(new GUIContent(components[i].GetType().Name), components[i].GetType().FullName.Equals((string)sharedVariable2.GetValue()), ComponentSelectionCallback, components[i].GetType().FullName);
                        }
                        genericMenu.ShowAsContext();
                        mActiveMenuSelectionTask = task;
                    }
                }
            }
            else
            {
                GUILayout.Label(guiContent, GUILayout.Width(146f));
                FieldInfo field3 = task.GetType().GetField("componentName");
                SharedVariable sharedVariable3 = field3.GetValue(task) as SharedVariable;
                SharedVariable sharedVariable4 = field.GetValue(task) as SharedVariable;
                string empty2 = string.Empty;
                empty2 = ((sharedVariable3 == null || string.IsNullOrEmpty((string)sharedVariable3.GetValue())) ? "Component Required" : ((!string.IsNullOrEmpty((string)sharedVariable4.GetValue())) ? ((string)sharedVariable4.GetValue()) : "Select"));
                if (GUILayout.Button(empty2, EditorStyles.toolbarPopup, GUILayout.Width(92f)) && !string.IsNullOrEmpty((string)sharedVariable3.GetValue()))
                {
                    GenericMenu genericMenu2 = new GenericMenu();
                    genericMenu2.AddItem(new GUIContent("None"), string.IsNullOrEmpty((string)sharedVariable4.GetValue()), SecondaryReflectionSelectionCallback, null);
                    GameObject gameObject2 = null;
                    if (sharedVariable == null || (GameObject)sharedVariable.GetValue() == null)
                    {
                        if (task.Owner != null)
                        {
                            gameObject2 = task.Owner.gameObject;
                        }
                    }
                    else
                    {
                        gameObject2 = (GameObject)sharedVariable.GetValue();
                    }
                    if (gameObject2 != null)
                    {
                        Component component = gameObject2.GetComponent(TaskUtility.GetTypeWithinAssembly((string)sharedVariable3.GetValue()));
                        List<Type> sharedVariableTypes = VariableInspector.FindAllSharedVariableTypes(removeShared: false);
                        if (IsInvokeMethodTask(task.GetType()))
                        {
                            MethodInfo[] methods = component.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
                            for (int j = 0; j < methods.Length; j++)
                            {
                                if (methods[j].IsSpecialName || methods[j].IsGenericMethod || methods[j].GetParameters().Length > 4)
                                {
                                    continue;
                                }
                                ParameterInfo[] parameters = methods[j].GetParameters();
                                bool flag = true;
                                for (int k = 0; k < parameters.Length; k++)
                                {
                                    if (!SharedVariableTypeExists(sharedVariableTypes, parameters[k].ParameterType))
                                    {
                                        flag = false;
                                        break;
                                    }
                                }
                                if (flag && (methods[j].ReturnType.Equals(typeof(void)) || SharedVariableTypeExists(sharedVariableTypes, methods[j].ReturnType)))
                                {
                                    genericMenu2.AddItem(new GUIContent(methods[j].Name), methods[j].Name.Equals((string)sharedVariable4.GetValue()), SecondaryReflectionSelectionCallback, methods[j]);
                                }
                            }
                        }
                        else if (IsFieldReflectionTask(task.GetType()))
                        {
                            FieldInfo[] fields = component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
                            for (int l = 0; l < fields.Length; l++)
                            {
                                if (!fields[l].IsSpecialName && SharedVariableTypeExists(sharedVariableTypes, fields[l].FieldType))
                                {
                                    genericMenu2.AddItem(new GUIContent(fields[l].Name), fields[l].Name.Equals((string)sharedVariable4.GetValue()), SecondaryReflectionSelectionCallback, fields[l]);
                                }
                            }
                        }
                        else
                        {
                            PropertyInfo[] properties = component.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
                            for (int m = 0; m < properties.Length; m++)
                            {
                                if (!properties[m].IsSpecialName && SharedVariableTypeExists(sharedVariableTypes, properties[m].PropertyType))
                                {
                                    genericMenu2.AddItem(new GUIContent(properties[m].Name), properties[m].Name.Equals((string)sharedVariable4.GetValue()), SecondaryReflectionSelectionCallback, properties[m]);
                                }
                            }
                        }
                        genericMenu2.ShowAsContext();
                        mActiveMenuSelectionTask = task;
                    }
                }
            }
            GUILayout.Space(8f);
        }

        private void ComponentSelectionCallback(object obj)
        {
            if (mActiveMenuSelectionTask != null)
            {
                FieldInfo field = mActiveMenuSelectionTask.GetType().GetField("componentName");
                SharedVariable value = Activator.CreateInstance(TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.SharedString")) as SharedVariable;
                if (obj == null)
                {
                    field.SetValue(mActiveMenuSelectionTask, value);
                    value = Activator.CreateInstance(TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.SharedString")) as SharedVariable;
                    FieldInfo fieldInfo = null;
                    if (!IsInvokeMethodTask(mActiveMenuSelectionTask.GetType()))
                    {
                        fieldInfo = ((!IsFieldReflectionTask(mActiveMenuSelectionTask.GetType())) ? mActiveMenuSelectionTask.GetType().GetField("propertyName") : mActiveMenuSelectionTask.GetType().GetField("fieldName"));
                    }
                    else
                    {
                        fieldInfo = mActiveMenuSelectionTask.GetType().GetField("methodName");
                        ClearInvokeVariablesTask();
                    }
                    fieldInfo.SetValue(mActiveMenuSelectionTask, value);
                }
                else
                {
                    string text = (string)obj;
                    SharedVariable sharedVariable = field.GetValue(mActiveMenuSelectionTask) as SharedVariable;
                    if (!text.Equals((string)sharedVariable.GetValue()))
                    {
                        FieldInfo fieldInfo2 = null;
                        FieldInfo fieldInfo3 = null;
                        if (IsInvokeMethodTask(mActiveMenuSelectionTask.GetType()))
                        {
                            fieldInfo2 = mActiveMenuSelectionTask.GetType().GetField("methodName");
                            for (int i = 0; i < 4; i++)
                            {
                                FieldInfo field2 = mActiveMenuSelectionTask.GetType().GetField("parameter" + (i + 1));
                                field2.SetValue(mActiveMenuSelectionTask, null);
                            }
                            fieldInfo3 = mActiveMenuSelectionTask.GetType().GetField("storeResult");
                        }
                        else if (IsFieldReflectionTask(mActiveMenuSelectionTask.GetType()))
                        {
                            fieldInfo2 = mActiveMenuSelectionTask.GetType().GetField("fieldName");
                            fieldInfo3 = mActiveMenuSelectionTask.GetType().GetField("fieldValue");
                            if (fieldInfo3 == null)
                            {
                                fieldInfo3 = mActiveMenuSelectionTask.GetType().GetField("compareValue");
                            }
                        }
                        else
                        {
                            fieldInfo2 = mActiveMenuSelectionTask.GetType().GetField("propertyName");
                            fieldInfo3 = mActiveMenuSelectionTask.GetType().GetField("propertyValue");
                            if (fieldInfo3 == null)
                            {
                                fieldInfo3 = mActiveMenuSelectionTask.GetType().GetField("compareValue");
                            }
                        }
                        fieldInfo2.SetValue(mActiveMenuSelectionTask, value);
                        fieldInfo3.SetValue(mActiveMenuSelectionTask, null);
                    }
                    value = Activator.CreateInstance(TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.SharedString")) as SharedVariable;
                    value.SetValue(text);
                    field.SetValue(mActiveMenuSelectionTask, value);
                }
            }
            BehaviorDesignerWindow.instance.SaveBehavior();
        }

        private void SecondaryReflectionSelectionCallback(object obj)
        {
            if (mActiveMenuSelectionTask != null)
            {
                SharedVariable sharedVariable = Activator.CreateInstance(TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.SharedString")) as SharedVariable;
                FieldInfo fieldInfo = null;
                if (!IsInvokeMethodTask(mActiveMenuSelectionTask.GetType()))
                {
                    fieldInfo = ((!IsFieldReflectionTask(mActiveMenuSelectionTask.GetType())) ? mActiveMenuSelectionTask.GetType().GetField("propertyName") : mActiveMenuSelectionTask.GetType().GetField("fieldName"));
                }
                else
                {
                    ClearInvokeVariablesTask();
                    fieldInfo = mActiveMenuSelectionTask.GetType().GetField("methodName");
                }
                if (obj == null)
                {
                    fieldInfo.SetValue(mActiveMenuSelectionTask, sharedVariable);
                }
                else if (IsInvokeMethodTask(mActiveMenuSelectionTask.GetType()))
                {
                    MethodInfo methodInfo = (MethodInfo)obj;
                    sharedVariable.SetValue(methodInfo.Name);
                    fieldInfo.SetValue(mActiveMenuSelectionTask, sharedVariable);
                    ParameterInfo[] parameters = methodInfo.GetParameters();
                    for (int i = 0; i < 4; i++)
                    {
                        FieldInfo field = mActiveMenuSelectionTask.GetType().GetField("parameter" + (i + 1));
                        if (i < parameters.Length)
                        {
                            sharedVariable = Activator.CreateInstance(FieldInspector.FriendlySharedVariableName(parameters[i].ParameterType)) as SharedVariable;
                            field.SetValue(mActiveMenuSelectionTask, sharedVariable);
                        }
                        else
                        {
                            field.SetValue(mActiveMenuSelectionTask, null);
                        }
                    }
                    if (!methodInfo.ReturnType.Equals(typeof(void)))
                    {
                        FieldInfo field2 = mActiveMenuSelectionTask.GetType().GetField("storeResult");
                        sharedVariable = Activator.CreateInstance(FieldInspector.FriendlySharedVariableName(methodInfo.ReturnType)) as SharedVariable;
                        sharedVariable.IsShared = true;
                        field2.SetValue(mActiveMenuSelectionTask, sharedVariable);
                    }
                }
                else if (IsFieldReflectionTask(mActiveMenuSelectionTask.GetType()))
                {
                    FieldInfo fieldInfo2 = (FieldInfo)obj;
                    sharedVariable.SetValue(fieldInfo2.Name);
                    fieldInfo.SetValue(mActiveMenuSelectionTask, sharedVariable);
                    FieldInfo field3 = mActiveMenuSelectionTask.GetType().GetField("fieldValue");
                    if (field3 == null)
                    {
                        field3 = mActiveMenuSelectionTask.GetType().GetField("compareValue");
                    }
                    sharedVariable = Activator.CreateInstance(FieldInspector.FriendlySharedVariableName(fieldInfo2.FieldType)) as SharedVariable;
                    sharedVariable.IsShared = IsReflectionGetterTask(mActiveMenuSelectionTask.GetType());
                    field3.SetValue(mActiveMenuSelectionTask, sharedVariable);
                }
                else
                {
                    PropertyInfo propertyInfo = (PropertyInfo)obj;
                    sharedVariable.SetValue(propertyInfo.Name);
                    fieldInfo.SetValue(mActiveMenuSelectionTask, sharedVariable);
                    FieldInfo field4 = mActiveMenuSelectionTask.GetType().GetField("propertyValue");
                    if (field4 == null)
                    {
                        field4 = mActiveMenuSelectionTask.GetType().GetField("compareValue");
                    }
                    sharedVariable = Activator.CreateInstance(FieldInspector.FriendlySharedVariableName(propertyInfo.PropertyType)) as SharedVariable;
                    sharedVariable.IsShared = IsReflectionGetterTask(mActiveMenuSelectionTask.GetType());
                    field4.SetValue(mActiveMenuSelectionTask, sharedVariable);
                }
            }
            BehaviorDesignerWindow.instance.SaveBehavior();
        }

        private void ClearInvokeVariablesTask()
        {
            for (int i = 0; i < 4; i++)
            {
                FieldInfo field = mActiveMenuSelectionTask.GetType().GetField("parameter" + (i + 1));
                field.SetValue(mActiveMenuSelectionTask, null);
            }
            FieldInfo field2 = mActiveMenuSelectionTask.GetType().GetField("storeResult");
            field2.SetValue(mActiveMenuSelectionTask, null);
        }

        private bool CanDrawReflectedField(object task, FieldInfo field)
        {
            if (!field.Name.Contains("parameter") && !field.Name.Contains("storeResult") && !field.Name.Contains("fieldValue") && !field.Name.Contains("propertyValue") && !field.Name.Contains("compareValue"))
            {
                return true;
            }
            if (IsInvokeMethodTask(task.GetType()))
            {
                if (field.Name.Contains("parameter"))
                {
                    FieldInfo field2 = task.GetType().GetField(field.Name);
                    return field2.GetValue(task) != null;
                }
                MethodInfo methodInfo = null;
                if ((methodInfo = GetInvokeMethodInfo(task)) == null)
                {
                    return false;
                }
                if (field.Name.Equals("storeResult"))
                {
                    return !methodInfo.ReturnType.Equals(typeof(void));
                }
                return true;
            }
            if (IsFieldReflectionTask(task.GetType()))
            {
                FieldInfo field3 = task.GetType().GetField("fieldName");
                return field3.GetValue(task) is SharedVariable sharedVariable && !string.IsNullOrEmpty((string)sharedVariable.GetValue());
            }
            FieldInfo field4 = task.GetType().GetField("propertyName");
            return field4.GetValue(task) is SharedVariable sharedVariable2 && !string.IsNullOrEmpty((string)sharedVariable2.GetValue());
        }

        private string InvokeParameterName(object task, FieldInfo field)
        {
            if (!field.Name.Contains("parameter"))
            {
                return field.Name;
            }
            MethodInfo methodInfo = null;
            if ((methodInfo = GetInvokeMethodInfo(task)) == null)
            {
                return field.Name;
            }
            ParameterInfo[] parameters = methodInfo.GetParameters();
            int num = int.Parse(field.Name.Substring(9)) - 1;
            if (num < parameters.Length)
            {
                return parameters[num].Name;
            }
            return field.Name;
        }

        private MethodInfo GetInvokeMethodInfo(object task)
        {
            FieldInfo field = task.GetType().GetField("targetGameObject");
            SharedVariable sharedVariable = field.GetValue(task) as SharedVariable;
            GameObject gameObject = null;
            if (sharedVariable == null || (GameObject)sharedVariable.GetValue() == null)
            {
                if ((task as Task).Owner != null)
                {
                    gameObject = (task as Task).Owner.gameObject;
                }
            }
            else
            {
                gameObject = (GameObject)sharedVariable.GetValue();
            }
            if (gameObject == null)
            {
                return null;
            }
            FieldInfo field2 = task.GetType().GetField("componentName");
            if (!(field2.GetValue(task) is SharedVariable sharedVariable2) || string.IsNullOrEmpty((string)sharedVariable2.GetValue()))
            {
                return null;
            }
            FieldInfo field3 = task.GetType().GetField("methodName");
            if (!(field3.GetValue(task) is SharedVariable sharedVariable3) || string.IsNullOrEmpty((string)sharedVariable3.GetValue()))
            {
                return null;
            }
            List<Type> list = new List<Type>();
            SharedVariable sharedVariable4 = null;
            for (int i = 0; i < 4; i++)
            {
                FieldInfo field4 = task.GetType().GetField("parameter" + (i + 1));
                if (field4.GetValue(task) is SharedVariable sharedVariable5)
                {
                    list.Add(sharedVariable5.GetType().GetProperty("Value").PropertyType);
                    continue;
                }
                break;
            }
            Component component = gameObject.GetComponent(TaskUtility.GetTypeWithinAssembly((string)sharedVariable2.GetValue()));
            if (component == null)
            {
                return null;
            }
            return component.GetType().GetMethod((string)sharedVariable3.GetValue(), list.ToArray());
        }

        private bool SharedVariableTypeExists(List<Type> sharedVariableTypes, Type type)
        {
            Type type2 = FieldInspector.FriendlySharedVariableName(type);
            for (int i = 0; i < sharedVariableTypes.Count; i++)
            {
                if (type2.IsAssignableFrom(sharedVariableTypes[i]))
                {
                    return true;
                }
            }
            return false;
        }
    }
}