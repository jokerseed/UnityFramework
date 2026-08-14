using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    public class TaskCopier : UnityEditor.Editor
    {
        public static TaskSerializer CopySerialized(Task task)
        {
            TaskSerializer taskSerializer = new TaskSerializer();
            taskSerializer.offset = (task.NodeData.NodeDesigner as NodeDesigner).GetAbsolutePosition() + new Vector2(10f, 10f);
            taskSerializer.unityObjects = new List<UnityEngine.Object>();
            taskSerializer.serialization = MiniJSON.Serialize(JSONSerialization.SerializeTask(task, serializeChildren: false, ref taskSerializer.unityObjects));
            return taskSerializer;
        }

        public static Task PasteTask(BehaviorSource behaviorSource, TaskSerializer serializer)
        {
            Dictionary<int, Task> IDtoTask = new Dictionary<int, Task>();
            JSONDeserialization.TaskIDs = new Dictionary<JSONDeserialization.TaskField, List<int>>();
            Task task = JSONDeserialization.DeserializeTask(behaviorSource, MiniJSON.Deserialize(serializer.serialization) as Dictionary<string, object>, ref IDtoTask, serializer.unityObjects);
            CheckSharedVariables(behaviorSource, task);
            if (JSONDeserialization.TaskIDs.Count > 0)
            {
                foreach (JSONDeserialization.TaskField key in JSONDeserialization.TaskIDs.Keys)
                {
                    List<int> list = JSONDeserialization.TaskIDs[key];
                    Type fieldType = key.fieldInfo.FieldType;
                    if (key.fieldInfo.FieldType.IsArray)
                    {
                        int num = 0;
                        for (int i = 0; i < list.Count; i++)
                        {
                            Task task2 = TaskWithID(behaviorSource, list[i]);
                            if (task2 != null && (task2.GetType().Equals(fieldType.GetElementType()) || task2.GetType().IsSubclassOf(fieldType.GetElementType())))
                            {
                                num++;
                            }
                        }
                        Array array = Array.CreateInstance(fieldType.GetElementType(), num);
                        int num2 = 0;
                        for (int j = 0; j < list.Count; j++)
                        {
                            Task task3 = TaskWithID(behaviorSource, list[j]);
                            if (task3 != null && (task3.GetType().Equals(fieldType.GetElementType()) || task3.GetType().IsSubclassOf(fieldType.GetElementType())))
                            {
                                array.SetValue(task3, num2);
                                num2++;
                            }
                        }
                        key.fieldInfo.SetValue(key.task, array);
                    }
                    else
                    {
                        Task task4 = TaskWithID(behaviorSource, list[0]);
                        if (task4 != null && (task4.GetType().Equals(key.fieldInfo.FieldType) || task4.GetType().IsSubclassOf(key.fieldInfo.FieldType)))
                        {
                            key.fieldInfo.SetValue(key.task, task4);
                        }
                    }
                }
                JSONDeserialization.TaskIDs = null;
            }
            return task;
        }

        private static void CheckSharedVariables(BehaviorSource behaviorSource, Task task)
        {
            if (task == null)
            {
                return;
            }
            CheckSharedVariableFields(behaviorSource, task, task, new HashSet<object>());
            if (!(task is ParentTask))
            {
                return;
            }
            ParentTask parentTask = task as ParentTask;
            if (parentTask.Children != null)
            {
                for (int i = 0; i < parentTask.Children.Count; i++)
                {
                    CheckSharedVariables(behaviorSource, parentTask.Children[i]);
                }
            }
        }

        private static void CheckSharedVariableFields(BehaviorSource behaviorSource, Task task, object obj, HashSet<object> visitedObjects)
        {
            if (obj == null || visitedObjects.Contains(obj))
            {
                return;
            }
            visitedObjects.Add(obj);
            FieldInfo[] serializableFields = TaskUtility.GetSerializableFields(obj.GetType());
            for (int i = 0; i < serializableFields.Length; i++)
            {
                if (typeof(SharedVariable).IsAssignableFrom(serializableFields[i].FieldType))
                {
                    if (serializableFields[i].GetValue(obj) is SharedVariable sharedVariable)
                    {
                        if (sharedVariable.IsShared && !sharedVariable.IsGlobal && !string.IsNullOrEmpty(sharedVariable.Name) && behaviorSource.GetVariable(sharedVariable.Name) == null)
                        {
                            behaviorSource.SetVariable(sharedVariable.Name, sharedVariable);
                        }
                        CheckSharedVariableFields(behaviorSource, task, sharedVariable, visitedObjects);
                    }
                }
                else if (serializableFields[i].FieldType.IsClass && !serializableFields[i].FieldType.Equals(typeof(Type)) && !typeof(Delegate).IsAssignableFrom(serializableFields[i].FieldType))
                {
                    CheckSharedVariableFields(behaviorSource, task, serializableFields[i].GetValue(obj), visitedObjects);
                }
            }
        }

        private static Task TaskWithID(BehaviorSource behaviorSource, int id)
        {
            Task task = null;
            if (behaviorSource.RootTask != null)
            {
                task = TaskWithID(id, behaviorSource.RootTask);
            }
            if (task == null && behaviorSource.DetachedTasks != null)
            {
                for (int i = 0; i < behaviorSource.DetachedTasks.Count; i++)
                {
                    if ((task = TaskWithID(id, behaviorSource.DetachedTasks[i])) != null)
                    {
                        break;
                    }
                }
            }
            return task;
        }

        private static Task TaskWithID(int id, Task task)
        {
            if (task == null)
            {
                return null;
            }
            if (task.ID == id)
            {
                return task;
            }
            if (task is ParentTask)
            {
                ParentTask parentTask = task as ParentTask;
                if (parentTask.Children != null)
                {
                    for (int i = 0; i < parentTask.Children.Count; i++)
                    {
                        Task task2 = TaskWithID(id, parentTask.Children[i]);
                        if (task2 != null)
                        {
                            return task2;
                        }
                    }
                }
            }
            return null;
        }
    }
}