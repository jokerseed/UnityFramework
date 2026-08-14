using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    public class TaskReferences : MonoBehaviour
    {
        public static void CheckReferences(BehaviorSource behaviorSource)
        {
            if (behaviorSource.RootTask != null)
            {
                CheckReferences(behaviorSource, behaviorSource.RootTask);
            }
            if (behaviorSource.DetachedTasks != null)
            {
                for (int i = 0; i < behaviorSource.DetachedTasks.Count; i++)
                {
                    CheckReferences(behaviorSource, behaviorSource.DetachedTasks[i]);
                }
            }
        }

        private static void CheckReferences(BehaviorSource behaviorSource, Task task)
        {
            FieldInfo[] serializableFields = TaskUtility.GetSerializableFields(task.GetType());
            for (int i = 0; i < serializableFields.Length; i++)
            {
                if (!serializableFields[i].FieldType.IsArray && (serializableFields[i].FieldType.Equals(typeof(Task)) || serializableFields[i].FieldType.IsSubclassOf(typeof(Task))))
                {
                    if (serializableFields[i].GetValue(task) is Task referencedTask)
                    {
                        Task task2 = FindReferencedTask(behaviorSource, referencedTask);
                        if (task2 != null)
                        {
                            serializableFields[i].SetValue(task, task2);
                        }
                    }
                }
                else
                {
                    if (!serializableFields[i].FieldType.IsArray || (!serializableFields[i].FieldType.GetElementType().Equals(typeof(Task)) && !serializableFields[i].FieldType.GetElementType().IsSubclassOf(typeof(Task))) || !(serializableFields[i].GetValue(task) is Task[] array))
                    {
                        continue;
                    }
                    IList list = Activator.CreateInstance(typeof(List<>).MakeGenericType(serializableFields[i].FieldType.GetElementType())) as IList;
                    if (BehaviorDesignerUtility.HasAttribute(serializableFields[i], typeof(InspectTaskAttribute)))
                    {
                        continue;
                    }
                    for (int j = 0; j < array.Length; j++)
                    {
                        Task task3 = FindReferencedTask(behaviorSource, array[j]);
                        if (task3 != null)
                        {
                            list.Add(task3);
                        }
                    }
                    Array array2 = Array.CreateInstance(serializableFields[i].FieldType.GetElementType(), list.Count);
                    list.CopyTo(array2, 0);
                    serializableFields[i].SetValue(task, array2);
                }
            }
            if (!task.GetType().IsSubclassOf(typeof(ParentTask)))
            {
                return;
            }
            ParentTask parentTask = task as ParentTask;
            if (parentTask.Children != null)
            {
                for (int k = 0; k < parentTask.Children.Count; k++)
                {
                    CheckReferences(behaviorSource, parentTask.Children[k]);
                }
            }
        }

        private static Task FindReferencedTask(BehaviorSource behaviorSource, Task referencedTask)
        {
            if (referencedTask == null)
            {
                return null;
            }
            int iD = referencedTask.ID;
            Task result;
            if (behaviorSource.RootTask != null && (result = FindReferencedTask(behaviorSource.RootTask, iD)) != null)
            {
                return result;
            }
            if (behaviorSource.DetachedTasks != null)
            {
                for (int i = 0; i < behaviorSource.DetachedTasks.Count; i++)
                {
                    if ((result = FindReferencedTask(behaviorSource.DetachedTasks[i], iD)) != null)
                    {
                        return result;
                    }
                }
            }
            return null;
        }

        private static Task FindReferencedTask(Task task, int referencedTaskID)
        {
            if (task.ID == referencedTaskID)
            {
                return task;
            }
            if (task.GetType().IsSubclassOf(typeof(ParentTask)))
            {
                ParentTask parentTask = task as ParentTask;
                if (parentTask.Children != null)
                {
                    for (int i = 0; i < parentTask.Children.Count; i++)
                    {
                        Task result;
                        if ((result = FindReferencedTask(parentTask.Children[i], referencedTaskID)) != null)
                        {
                            return result;
                        }
                    }
                }
            }
            return null;
        }

        public static void CheckReferences(Behavior behavior, List<Task> taskList)
        {
            for (int i = 0; i < taskList.Count; i++)
            {
                CheckReferences(behavior, taskList[i], taskList);
            }
        }

        private static void CheckReferences(Behavior behavior, Task task, List<Task> taskList)
        {
            if (TaskUtility.CompareType(task.GetType(), "BehaviorDesigner.Runtime.Tasks.ConditionalEvaluator"))
            {
                FieldInfo field = task.GetType().GetField("conditionalTask");
                object value = field.GetValue(task);
                if (value != null)
                {
                    task = value as Task;
                }
            }
            FieldInfo[] serializableFields = TaskUtility.GetSerializableFields(task.GetType());
            for (int i = 0; i < serializableFields.Length; i++)
            {
                if (!serializableFields[i].FieldType.IsArray && (serializableFields[i].FieldType.Equals(typeof(Task)) || serializableFields[i].FieldType.IsSubclassOf(typeof(Task))))
                {
                    if (serializableFields[i].GetValue(task) is Task task2 && !task2.Owner.Equals(behavior))
                    {
                        Task task3 = FindReferencedTask(task2, taskList);
                        if (task3 != null)
                        {
                            serializableFields[i].SetValue(task, task3);
                        }
                    }
                }
                else
                {
                    if (!serializableFields[i].FieldType.IsArray || (!serializableFields[i].FieldType.GetElementType().Equals(typeof(Task)) && !serializableFields[i].FieldType.GetElementType().IsSubclassOf(typeof(Task))) || !(serializableFields[i].GetValue(task) is Task[] array))
                    {
                        continue;
                    }
                    IList list = Activator.CreateInstance(typeof(List<>).MakeGenericType(serializableFields[i].FieldType.GetElementType())) as IList;
                    for (int j = 0; j < array.Length; j++)
                    {
                        Task task4 = FindReferencedTask(array[j], taskList);
                        if (task4 != null)
                        {
                            list.Add(task4);
                        }
                    }
                    Array array2 = Array.CreateInstance(serializableFields[i].FieldType.GetElementType(), list.Count);
                    list.CopyTo(array2, 0);
                    serializableFields[i].SetValue(task, array2);
                }
            }
        }

        private static Task FindReferencedTask(Task referencedTask, List<Task> taskList)
        {
            int referenceID = referencedTask.ReferenceID;
            for (int i = 0; i < taskList.Count; i++)
            {
                if (taskList[i].ReferenceID == referenceID)
                {
                    return taskList[i];
                }
            }
            return null;
        }
    }
}