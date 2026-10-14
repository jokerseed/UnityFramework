using System;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    [Serializable]
    public class ErrorDetails
    {
        public enum ErrorType
        {
            RequiredField,
            SharedVariable,
            NonUniqueDynamicVariable,
            MissingChildren,
            UnknownTask,
            InvalidTaskReference,
            InvalidVariableReference
        }

        [SerializeField]
        private ErrorType mType;

        [SerializeField]
        private NodeDesigner mNodeDesigner;

        [SerializeField]
        private string mTaskFriendlyName;

        [SerializeField]
        private string mTaskType;

        [SerializeField]
        private string mFieldName;

        public ErrorType Type => mType;

        public NodeDesigner NodeDesigner => mNodeDesigner;

        public string TaskFriendlyName => mTaskFriendlyName;

        public string TaskType => mTaskType;

        public string FieldName => mFieldName;

        public ErrorDetails(ErrorType type, Task task, string fieldName)
        {
            mType = type;
            if (task != null)
            {
                mNodeDesigner = task.NodeData.NodeDesigner as NodeDesigner;
                mTaskFriendlyName = task.FriendlyName;
                mTaskType = task.GetType().ToString();
            }
            mFieldName = fieldName;
        }
    }
}