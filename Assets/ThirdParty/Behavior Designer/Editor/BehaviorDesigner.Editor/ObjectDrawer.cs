using System.Reflection;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    public class ObjectDrawer
    {
        protected FieldInfo fieldInfo;

        protected ObjectDrawerAttribute attribute;

        protected object value;

        protected Task task;

        public FieldInfo FieldInfo
        {
            get
            {
                return fieldInfo;
            }
            set
            {
                fieldInfo = value;
            }
        }

        public ObjectDrawerAttribute Attribute
        {
            get
            {
                return attribute;
            }
            set
            {
                attribute = value;
            }
        }

        public object Value
        {
            get
            {
                return value;
            }
            set
            {
                this.value = value;
            }
        }

        public Task Task
        {
            get
            {
                return task;
            }
            set
            {
                task = value;
            }
        }

        public virtual void OnGUI(GUIContent label)
        {
        }
    }
}