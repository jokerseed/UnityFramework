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
    public static class ErrorCheck
    {
        private static HashSet<int> fieldHashes = new HashSet<int>();

        public static List<ErrorDetails> CheckForErrors(BehaviorSource behaviorSource)
        {
            if (behaviorSource == null || behaviorSource.Owner == null)
            {
                return null;
            }
            List<ErrorDetails> errorDetails = null;
            fieldHashes.Clear();
            BehaviorSource behaviorSource2 = behaviorSource;
            if (!Application.isPlaying && behaviorSource.Owner is Behavior && (behaviorSource.Owner as Behavior).ExternalBehavior != null)
            {
                behaviorSource = (behaviorSource.Owner as Behavior).ExternalBehavior.BehaviorSource;
            }
            bool projectLevelBehavior = AssetDatabase.GetAssetPath(behaviorSource.Owner.GetObject()).Length > 0;
            if (behaviorSource.EntryTask != null)
            {
                CheckTaskForErrors(behaviorSource.EntryTask, projectLevelBehavior, ref errorDetails);
                if (behaviorSource.RootTask == null)
                {
                    AddError(ref errorDetails, ErrorDetails.ErrorType.MissingChildren, behaviorSource.EntryTask, null);
                }
            }
            if (behaviorSource.RootTask != null)
            {
                CheckTaskForErrors(behaviorSource.RootTask, projectLevelBehavior, ref errorDetails);
            }
            if (!EditorApplication.isPlaying && AssetDatabase.GetAssetPath(behaviorSource2.Owner.GetObject()).Length > 0 && behaviorSource2.Variables != null)
            {
                for (int i = 0; i < behaviorSource2.Variables.Count; i++)
                {
                    if (behaviorSource2.Variables[i] != null)
                    {
                        object value = behaviorSource2.Variables[i].GetValue();
                        if (value != null && value is UnityEngine.Object && AssetDatabase.GetAssetPath(value as UnityEngine.Object).Length == 0)
                        {
                            AddError(ref errorDetails, ErrorDetails.ErrorType.InvalidVariableReference, null, behaviorSource2.Variables[i].Name);
                        }
                    }
                }
            }
            return errorDetails;
        }

        private static void CheckTaskForErrors(Task task, bool projectLevelBehavior, ref List<ErrorDetails> errorDetails)
        {
            if (task.Disabled)
            {
                return;
            }
            if (task is UnknownTask || task is UnknownParentTask)
            {
                AddError(ref errorDetails, ErrorDetails.ErrorType.UnknownTask, task, null);
            }
            if (task.GetType().GetCustomAttributes(typeof(SkipErrorCheckAttribute), inherit: false).Length == 0)
            {
                FieldInfo[] serializableFields = TaskUtility.GetSerializableFields(task.GetType());
                for (int i = 0; i < serializableFields.Length; i++)
                {
                    CheckField(task, projectLevelBehavior, ref errorDetails, serializableFields[i], 0, serializableFields[i].GetValue(task));
                }
            }
            if (!(task is ParentTask) || task.NodeData.NodeDesigner == null || (task.NodeData.NodeDesigner as NodeDesigner).IsEntryDisplay)
            {
                return;
            }
            ParentTask parentTask = task as ParentTask;
            if (parentTask.Children == null || parentTask.Children.Count == 0)
            {
                AddError(ref errorDetails, ErrorDetails.ErrorType.MissingChildren, task, null);
                return;
            }
            for (int j = 0; j < parentTask.Children.Count; j++)
            {
                CheckTaskForErrors(parentTask.Children[j], projectLevelBehavior, ref errorDetails);
            }
        }

        private static void CheckField(Task task, bool projectLevelBehavior, ref List<ErrorDetails> errorDetails, FieldInfo field, int hashPrefix, object value)
        {
            if (value == null)
            {
                return;
            }
            int num = hashPrefix + field.Name.GetHashCode() + field.GetHashCode();
            if (fieldHashes.Contains(num))
            {
                return;
            }
            fieldHashes.Add(num);
            if (TaskUtility.HasAttribute(field, typeof(RequiredFieldAttribute)) && !IsRequiredFieldValid(field.FieldType, value))
            {
                AddError(ref errorDetails, ErrorDetails.ErrorType.RequiredField, task, field.Name);
            }
            if (typeof(SharedVariable).IsAssignableFrom(field.FieldType))
            {
                if (value is SharedVariable sharedVariable)
                {
                    if (sharedVariable.IsShared && !sharedVariable.IsDynamic && string.IsNullOrEmpty(sharedVariable.Name) && !TaskUtility.HasAttribute(field, typeof(SharedRequiredAttribute)))
                    {
                        AddError(ref errorDetails, ErrorDetails.ErrorType.SharedVariable, task, field.Name);
                    }
                    SharedVariable variable;
                    if (!Application.isPlaying && sharedVariable.IsShared && sharedVariable.IsDynamic && !string.IsNullOrEmpty(sharedVariable.Name) && task.Owner != null && (variable = task.Owner.GetBehaviorSource().GetVariable(sharedVariable.Name)) != null && !variable.IsDynamic)
                    {
                        AddError(ref errorDetails, ErrorDetails.ErrorType.NonUniqueDynamicVariable, task, field.Name);
                    }
                    object value2 = sharedVariable.GetValue();
                    if (!EditorApplication.isPlaying && projectLevelBehavior && !sharedVariable.IsShared && value2 is UnityEngine.Object && AssetDatabase.GetAssetPath(value2 as UnityEngine.Object).Length <= 0)
                    {
                        AddError(ref errorDetails, ErrorDetails.ErrorType.InvalidTaskReference, task, field.Name);
                    }
                }
            }
            else if (value is UnityEngine.Object)
            {
                bool flag = AssetDatabase.GetAssetPath(value as UnityEngine.Object).Length > 0;
                if (!EditorApplication.isPlaying && projectLevelBehavior && !flag)
                {
                    AddError(ref errorDetails, ErrorDetails.ErrorType.InvalidTaskReference, task, field.Name);
                }
            }
            else if (!typeof(Delegate).IsAssignableFrom(field.FieldType) && !typeof(Task).IsAssignableFrom(field.FieldType) && !typeof(Behavior).IsAssignableFrom(field.FieldType) && (field.FieldType.IsClass || (field.FieldType.IsValueType && !field.FieldType.IsPrimitive)))
            {
                FieldInfo[] serializableFields = TaskUtility.GetSerializableFields(field.FieldType);
                for (int i = 0; i < serializableFields.Length; i++)
                {
                    CheckField(task, projectLevelBehavior, ref errorDetails, serializableFields[i], num, serializableFields[i].GetValue(value));
                }
            }
        }

        private static void AddError(ref List<ErrorDetails> errorDetails, ErrorDetails.ErrorType type, Task task, string fieldName)
        {
            if (errorDetails == null)
            {
                errorDetails = new List<ErrorDetails>();
            }
            errorDetails.Add(new ErrorDetails(type, task, fieldName));
        }

        public static bool IsRequiredFieldValid(Type fieldType, object value)
        {
            if (value == null || value.Equals(null))
            {
                return false;
            }
            if (typeof(IList).IsAssignableFrom(fieldType))
            {
                IList list = value as IList;
                if (list.Count == 0)
                {
                    return false;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == null || list[i].Equals(null))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}