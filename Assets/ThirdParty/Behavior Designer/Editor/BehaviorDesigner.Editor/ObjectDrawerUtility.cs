using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Editor
{
    internal static class ObjectDrawerUtility
    {
        private static Dictionary<Type, Type> objectDrawerTypeMap = new Dictionary<Type, Type>();

        private static Dictionary<int, ObjectDrawer> objectDrawerMap = new Dictionary<int, ObjectDrawer>();

        private static bool mapBuilt = false;

        private static void BuildObjectDrawers()
        {
            if (mapBuilt)
            {
                return;
            }
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                if (assembly == null)
                {
                    continue;
                }
                try
                {
                    Type[] exportedTypes = assembly.GetExportedTypes();
                    foreach (Type type in exportedTypes)
                    {
                        if (typeof(ObjectDrawer).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
                        {
                            CustomObjectDrawer[] array = null;
                            if ((array = type.GetCustomAttributes(typeof(CustomObjectDrawer), inherit: false) as CustomObjectDrawer[]).Length > 0)
                            {
                                objectDrawerTypeMap.Add(array[0].Type, type);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
            mapBuilt = true;
        }

        private static bool ObjectDrawerForType(Type type, ref ObjectDrawer objectDrawer, ref Type objectDrawerType, int hash)
        {
            BuildObjectDrawers();
            if (!objectDrawerTypeMap.ContainsKey(type))
            {
                return false;
            }
            objectDrawerType = objectDrawerTypeMap[type];
            if (objectDrawerMap.ContainsKey(hash))
            {
                objectDrawer = objectDrawerMap[hash];
            }
            return true;
        }

        public static ObjectDrawer GetObjectDrawer(Task task)
        {
            if (task == null)
            {
                return null;
            }
            ObjectDrawer objectDrawer = null;
            Type objectDrawerType = null;
            if (!ObjectDrawerForType(task.GetType(), ref objectDrawer, ref objectDrawerType, task.GetHashCode()))
            {
                return null;
            }
            if (objectDrawer == null)
            {
                objectDrawer = Activator.CreateInstance(objectDrawerType) as ObjectDrawer;
                objectDrawerMap.Add(task.GetHashCode(), objectDrawer);
            }
            objectDrawer.FieldInfo = null;
            objectDrawer.Task = task;
            return objectDrawer;
        }

        public static ObjectDrawer GetObjectDrawer(Task task, FieldInfo field)
        {
            ObjectDrawer objectDrawer = null;
            Type objectDrawerType = null;
            if (!ObjectDrawerForType(field.FieldType, ref objectDrawer, ref objectDrawerType, (task?.GetHashCode() ?? 0) + field.GetHashCode()))
            {
                return null;
            }
            if (objectDrawer == null)
            {
                objectDrawer = Activator.CreateInstance(objectDrawerType) as ObjectDrawer;
                objectDrawerMap.Add((task?.GetHashCode() ?? 0) + field.GetHashCode(), objectDrawer);
            }
            objectDrawer.FieldInfo = field;
            objectDrawer.Task = task;
            return objectDrawer;
        }

        public static ObjectDrawer GetObjectDrawer(Task task, FieldInfo field, ObjectDrawerAttribute attribute)
        {
            ObjectDrawer objectDrawer = null;
            Type objectDrawerType = null;
            if (!ObjectDrawerForType(attribute.GetType(), ref objectDrawer, ref objectDrawerType, (task?.GetHashCode() ?? 0) + field.GetHashCode() + attribute.GetHashCode()))
            {
                return null;
            }
            if (objectDrawer != null)
            {
                objectDrawer.Task = task;
                return objectDrawer;
            }
            objectDrawer = Activator.CreateInstance(objectDrawerType) as ObjectDrawer;
            objectDrawer.Attribute = attribute;
            objectDrawer.Task = task;
            objectDrawer.FieldInfo = field;
            objectDrawerMap.Add((task?.GetHashCode() ?? 0) + field.GetHashCode() + attribute.GetHashCode(), objectDrawer);
            return objectDrawer;
        }
    }
}