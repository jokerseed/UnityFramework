using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    public class JSONSerialization : UnityEngine.Object
    {
        private static TaskSerializationData taskSerializationData;

        private static FieldSerializationData fieldSerializationData;

        private static VariableSerializationData variableSerializationData;

        public static void Save(BehaviorSource behaviorSource)
        {
            behaviorSource.CheckForSerialization(force: false);
            taskSerializationData = new TaskSerializationData();
            fieldSerializationData = taskSerializationData.fieldSerializationData;
            Dictionary<string, object> dictionary = new Dictionary<string, object>();
            if (behaviorSource.EntryTask != null)
            {
                dictionary.Add("EntryTask", SerializeTask(behaviorSource.EntryTask, serializeChildren: true, ref fieldSerializationData.unityObjects));
            }
            if (behaviorSource.RootTask != null)
            {
                dictionary.Add("RootTask", SerializeTask(behaviorSource.RootTask, serializeChildren: true, ref fieldSerializationData.unityObjects));
            }
            if (behaviorSource.DetachedTasks != null && behaviorSource.DetachedTasks.Count > 0)
            {
                Dictionary<string, object>[] array = new Dictionary<string, object>[behaviorSource.DetachedTasks.Count];
                for (int i = 0; i < behaviorSource.DetachedTasks.Count; i++)
                {
                    array[i] = SerializeTask(behaviorSource.DetachedTasks[i], serializeChildren: true, ref fieldSerializationData.unityObjects);
                }
                dictionary.Add("DetachedTasks", array);
            }
            if (behaviorSource.Variables != null && behaviorSource.Variables.Count > 0)
            {
                dictionary.Add("Variables", SerializeVariables(behaviorSource.Variables, ref fieldSerializationData.unityObjects));
            }
            taskSerializationData.Version = "1.7.4";
            taskSerializationData.JSONSerialization = MiniJSON.Serialize(dictionary);
            behaviorSource.TaskData = taskSerializationData;
            if (behaviorSource.Owner != null && !behaviorSource.Owner.Equals(null))
            {
                BehaviorDesignerUtility.SetObjectDirty(behaviorSource.Owner.GetObject());
            }
        }

        public static void Save(GlobalVariables variables)
        {
            if (!(variables == null))
            {
                variableSerializationData = new VariableSerializationData();
                fieldSerializationData = variableSerializationData.fieldSerializationData;
                Dictionary<string, object> dictionary = new Dictionary<string, object>();
                dictionary.Add("Variables", SerializeVariables(variables.Variables, ref fieldSerializationData.unityObjects));
                variableSerializationData.JSONSerialization = MiniJSON.Serialize(dictionary);
                variables.VariableData = variableSerializationData;
                variables.Version = "1.7.4";
                BehaviorDesignerUtility.SetObjectDirty(variables);
            }
        }

        private static Dictionary<string, object>[] SerializeVariables(List<SharedVariable> variables, ref List<UnityEngine.Object> unityObjects)
        {
            Dictionary<string, object>[] array = new Dictionary<string, object>[variables.Count];
            for (int i = 0; i < variables.Count; i++)
            {
                array[i] = SerializeVariable(variables[i], ref unityObjects);
            }
            return array;
        }

        public static Dictionary<string, object> SerializeTask(Task task, bool serializeChildren, ref List<UnityEngine.Object> unityObjects)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("Type", task.GetType());
            dict.Add("NodeData", SerializeNodeData(task.NodeData));
            dict.Add("ID", task.ID);
            dict.Add("Name", task.FriendlyName);
            dict.Add("Instant", task.IsInstant);
            if (task.Disabled)
            {
                dict.Add("Disabled", task.Disabled);
            }
            SerializeFields(task, ref dict, ref unityObjects);
            if (serializeChildren && task is ParentTask)
            {
                ParentTask parentTask = task as ParentTask;
                if (parentTask.Children != null && parentTask.Children.Count > 0)
                {
                    Dictionary<string, object>[] array = new Dictionary<string, object>[parentTask.Children.Count];
                    for (int i = 0; i < parentTask.Children.Count; i++)
                    {
                        array[i] = SerializeTask(parentTask.Children[i], serializeChildren, ref unityObjects);
                    }
                    dict.Add("Children", array);
                }
            }
            return dict;
        }

        private static Dictionary<string, object> SerializeNodeData(NodeData nodeData)
        {
            Dictionary<string, object> dictionary = new Dictionary<string, object>();
            dictionary.Add("Offset", nodeData.Offset);
            if (nodeData.Comment.Length > 0)
            {
                dictionary.Add("Comment", nodeData.Comment);
            }
            if (nodeData.IsBreakpoint)
            {
                dictionary.Add("IsBreakpoint", nodeData.IsBreakpoint);
            }
            if (nodeData.Collapsed)
            {
                dictionary.Add("Collapsed", nodeData.Collapsed);
            }
            if (nodeData.ColorIndex != 0)
            {
                dictionary.Add("ColorIndex", nodeData.ColorIndex);
            }
            if (nodeData.WatchedFieldNames != null && nodeData.WatchedFieldNames.Count > 0)
            {
                dictionary.Add("WatchedFields", nodeData.WatchedFieldNames);
            }
            return dictionary;
        }

        private static Dictionary<string, object> SerializeVariable(SharedVariable sharedVariable, ref List<UnityEngine.Object> unityObjects)
        {
            if (sharedVariable == null)
            {
                return null;
            }
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("Type", sharedVariable.GetType());
            dict.Add("Name", sharedVariable.Name);
            if (sharedVariable.IsShared)
            {
                dict.Add("IsShared", sharedVariable.IsShared);
            }
            if (sharedVariable.IsGlobal)
            {
                dict.Add("IsGlobal", sharedVariable.IsGlobal);
            }
            if (sharedVariable.IsDynamic)
            {
                dict.Add("IsDynamic", sharedVariable.IsDynamic);
            }
            if (!string.IsNullOrEmpty(sharedVariable.Tooltip))
            {
                dict.Add("Tooltip", sharedVariable.Tooltip);
            }
            if (!string.IsNullOrEmpty(sharedVariable.PropertyMapping))
            {
                dict.Add("PropertyMapping", sharedVariable.PropertyMapping);
                if (!object.Equals(sharedVariable.PropertyMappingOwner, null))
                {
                    dict.Add("PropertyMappingOwner", unityObjects.Count);
                    unityObjects.Add(sharedVariable.PropertyMappingOwner);
                }
            }
            SerializeFields(sharedVariable, ref dict, ref unityObjects);
            return dict;
        }

        private static void SerializeFields(object obj, ref Dictionary<string, object> dict, ref List<UnityEngine.Object> unityObjects)
        {
            FieldInfo[] serializableFields = TaskUtility.GetSerializableFields(obj.GetType());
            for (int i = 0; i < serializableFields.Length; i++)
            {
                if (BehaviorDesignerUtility.HasAttribute(serializableFields[i], typeof(NonSerializedAttribute)) || ((serializableFields[i].IsPrivate || serializableFields[i].IsFamily) && !BehaviorDesignerUtility.HasAttribute(serializableFields[i], typeof(SerializeField))) || (obj is ParentTask && serializableFields[i].Name.Equals("children")) || serializableFields[i].GetValue(obj) == null)
                {
                    continue;
                }
                string key = (serializableFields[i].FieldType.Name + serializableFields[i].Name).ToString();
                if (typeof(IList).IsAssignableFrom(serializableFields[i].FieldType))
                {
                    if (!(serializableFields[i].GetValue(obj) is IList list))
                    {
                        continue;
                    }
                    List<object> list2 = new List<object>();
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (list[j] == null)
                        {
                            list2.Add(null);
                            continue;
                        }
                        Type type = list[j].GetType();
                        if (list[j] is Task && !TaskUtility.HasAttribute(serializableFields[i], typeof(InspectTaskAttribute)))
                        {
                            Task task = list[j] as Task;
                            list2.Add(task.ID);
                        }
                        else if (list[j] is SharedVariable)
                        {
                            list2.Add(SerializeVariable(list[j] as SharedVariable, ref unityObjects));
                        }
                        else if (list[j] is UnityEngine.Object)
                        {
                            UnityEngine.Object @object = list[j] as UnityEngine.Object;
                            if (!object.ReferenceEquals(@object, null) && @object != null)
                            {
                                list2.Add(unityObjects.Count);
                                unityObjects.Add(@object);
                            }
                        }
                        else if (type.Equals(typeof(LayerMask)))
                        {
                            list2.Add(((LayerMask)list[j]).value);
                        }
                        else if (type.IsPrimitive || type.IsEnum || type.Equals(typeof(string)) || type.Equals(typeof(Vector2)) || type.Equals(typeof(Vector2Int)) || type.Equals(typeof(Vector3)) || type.Equals(typeof(Vector3Int)) || type.Equals(typeof(Vector4)) || type.Equals(typeof(Quaternion)) || type.Equals(typeof(Matrix4x4)) || type.Equals(typeof(Color)) || type.Equals(typeof(Rect)))
                        {
                            list2.Add(list[j]);
                        }
                        else
                        {
                            Dictionary<string, object> dict2 = new Dictionary<string, object>();
                            SerializeFields(list[j], ref dict2, ref unityObjects);
                            Dictionary<string, object> dictionary = new Dictionary<string, object>();
                            dictionary.Add("Type", list[j].GetType().FullName);
                            dictionary.Add("Value", dict2);
                            list2.Add(dictionary);
                        }
                    }
                    if (list2 != null)
                    {
                        dict.Add(key, list2);
                    }
                }
                else if (typeof(Task).IsAssignableFrom(serializableFields[i].FieldType))
                {
                    if (serializableFields[i].GetValue(obj) is Task task2)
                    {
                        if (BehaviorDesignerUtility.HasAttribute(serializableFields[i], typeof(InspectTaskAttribute)))
                        {
                            Dictionary<string, object> dict3 = new Dictionary<string, object>();
                            dict3.Add("Type", task2.GetType());
                            SerializeFields(task2, ref dict3, ref unityObjects);
                            dict.Add(key, dict3);
                        }
                        else
                        {
                            dict.Add(key, task2.ID);
                        }
                    }
                }
                else if (typeof(SharedVariable).IsAssignableFrom(serializableFields[i].FieldType))
                {
                    if (!dict.ContainsKey(key))
                    {
                        dict.Add(key, SerializeVariable(serializableFields[i].GetValue(obj) as SharedVariable, ref unityObjects));
                    }
                }
                else if (typeof(UnityEngine.Object).IsAssignableFrom(serializableFields[i].FieldType))
                {
                    UnityEngine.Object object2 = serializableFields[i].GetValue(obj) as UnityEngine.Object;
                    if (!object.ReferenceEquals(object2, null) && object2 != null)
                    {
                        dict.Add(key, unityObjects.Count);
                        unityObjects.Add(object2);
                    }
                }
                else if (serializableFields[i].FieldType.Equals(typeof(LayerMask)))
                {
                    dict.Add(key, ((LayerMask)serializableFields[i].GetValue(obj)).value);
                }
                else if (serializableFields[i].FieldType.IsPrimitive || serializableFields[i].FieldType.IsEnum || serializableFields[i].FieldType.Equals(typeof(string)) || serializableFields[i].FieldType.Equals(typeof(Vector2)) || serializableFields[i].FieldType.Equals(typeof(Vector2Int)) || serializableFields[i].FieldType.Equals(typeof(Vector3)) || serializableFields[i].FieldType.Equals(typeof(Vector3Int)) || serializableFields[i].FieldType.Equals(typeof(Vector4)) || serializableFields[i].FieldType.Equals(typeof(Quaternion)) || serializableFields[i].FieldType.Equals(typeof(Matrix4x4)) || serializableFields[i].FieldType.Equals(typeof(Color)) || serializableFields[i].FieldType.Equals(typeof(Rect)))
                {
                    dict.Add(key, serializableFields[i].GetValue(obj));
                }
                else if (serializableFields[i].FieldType.Equals(typeof(AnimationCurve)))
                {
                    AnimationCurve animationCurve = serializableFields[i].GetValue(obj) as AnimationCurve;
                    Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
                    if (animationCurve.keys != null)
                    {
                        Keyframe[] keys = animationCurve.keys;
                        List<List<object>> list3 = new List<List<object>>();
                        for (int k = 0; k < keys.Length; k++)
                        {
                            List<object> list4 = new List<object>();
                            list4.Add(keys[k].time);
                            list4.Add(keys[k].value);
                            list4.Add(keys[k].inTangent);
                            list4.Add(keys[k].outTangent);
                            list3.Add(list4);
                        }
                        dictionary2.Add("Keys", list3);
                    }
                    dictionary2.Add("PreWrapMode", animationCurve.preWrapMode);
                    dictionary2.Add("PostWrapMode", animationCurve.postWrapMode);
                    dict.Add(key, dictionary2);
                }
                else
                {
                    Dictionary<string, object> dict4 = new Dictionary<string, object>();
                    SerializeFields(serializableFields[i].GetValue(obj), ref dict4, ref unityObjects);
                    dict.Add(key, dict4);
                }
            }
        }
    }
}