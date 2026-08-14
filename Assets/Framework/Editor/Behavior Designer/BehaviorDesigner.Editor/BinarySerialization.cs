using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    public class BinarySerialization
    {
        private static int fieldIndex;

        private static TaskSerializationData taskSerializationData;

        private static FieldSerializationData fieldSerializationData;

        private static HashSet<int> fieldHashes = new HashSet<int>();

        public static void Save(BehaviorSource behaviorSource)
        {
            fieldIndex = 0;
            taskSerializationData = new TaskSerializationData();
            fieldSerializationData = taskSerializationData.fieldSerializationData;
            if (behaviorSource.Variables != null)
            {
                for (int i = 0; i < behaviorSource.Variables.Count; i++)
                {
                    taskSerializationData.variableStartIndex.Add(fieldSerializationData.startIndex.Count);
                    SaveSharedVariable(behaviorSource.Variables[i], 0);
                }
            }
            if (!object.ReferenceEquals(behaviorSource.EntryTask, null))
            {
                SaveTask(behaviorSource.EntryTask, -1);
            }
            if (!object.ReferenceEquals(behaviorSource.RootTask, null))
            {
                SaveTask(behaviorSource.RootTask, 0);
            }
            if (behaviorSource.DetachedTasks != null)
            {
                for (int j = 0; j < behaviorSource.DetachedTasks.Count; j++)
                {
                    SaveTask(behaviorSource.DetachedTasks[j], -1);
                }
            }
            taskSerializationData.Version = "1.7.4";
            taskSerializationData.fieldSerializationData.byteDataArray = taskSerializationData.fieldSerializationData.byteData.ToArray();
            taskSerializationData.fieldSerializationData.byteData = null;
            behaviorSource.TaskData = taskSerializationData;
            if (behaviorSource.Owner != null && !behaviorSource.Owner.Equals(null))
            {
                BehaviorDesignerUtility.SetObjectDirty(behaviorSource.Owner.GetObject());
            }
        }

        public static void Save(GlobalVariables globalVariables)
        {
            if (globalVariables == null)
            {
                return;
            }
            fieldIndex = 0;
            globalVariables.VariableData = new VariableSerializationData();
            if (globalVariables.Variables != null && globalVariables.Variables.Count != 0)
            {
                fieldSerializationData = globalVariables.VariableData.fieldSerializationData;
                for (int i = 0; i < globalVariables.Variables.Count; i++)
                {
                    globalVariables.VariableData.variableStartIndex.Add(fieldSerializationData.startIndex.Count);
                    SaveSharedVariable(globalVariables.Variables[i], 0);
                }
                globalVariables.Version = "1.7.4";
                globalVariables.VariableData.fieldSerializationData.byteDataArray = globalVariables.VariableData.fieldSerializationData.byteData.ToArray();
                globalVariables.VariableData.fieldSerializationData.byteData = null;
                BehaviorDesignerUtility.SetObjectDirty(globalVariables);
            }
        }

        private static void SaveTask(Task task, int parentTaskIndex)
        {
            taskSerializationData.types.Add(task.GetType().ToString());
            taskSerializationData.parentIndex.Add(parentTaskIndex);
            taskSerializationData.startIndex.Add(fieldSerializationData.startIndex.Count);
            SaveField(typeof(int), "ID", 0, task.ID);
            SaveField(typeof(string), "FriendlyName", 0, task.FriendlyName);
            SaveField(typeof(bool), "IsInstant", 0, task.IsInstant);
            SaveField(typeof(bool), "Disabled", 0, task.Disabled);
            SaveNodeData(task.NodeData);
            SaveFields(task, 0);
            if (!(task is ParentTask))
            {
                return;
            }
            ParentTask parentTask = task as ParentTask;
            if (parentTask.Children != null && parentTask.Children.Count > 0)
            {
                for (int i = 0; i < parentTask.Children.Count; i++)
                {
                    SaveTask(parentTask.Children[i], parentTask.ID);
                }
            }
        }

        private static void SaveNodeData(NodeData nodeData)
        {
            SaveField(typeof(Vector2), "NodeDataOffset", 0, nodeData.Offset);
            SaveField(typeof(string), "NodeDataComment", 0, nodeData.Comment);
            SaveField(typeof(bool), "NodeDataIsBreakpoint", 0, nodeData.IsBreakpoint);
            SaveField(typeof(bool), "NodeDataCollapsed", 0, nodeData.Collapsed);
            SaveField(typeof(int), "NodeDataColorIndex", 0, nodeData.ColorIndex);
            SaveField(typeof(List<string>), "NodeDataWatchedFields", 0, nodeData.WatchedFieldNames);
        }

        private static void SaveSharedVariable(SharedVariable sharedVariable, int hashPrefix)
        {
            if (sharedVariable == null)
            {
                return;
            }
            SaveField(typeof(string), "Type", hashPrefix, sharedVariable.GetType().ToString());
            SaveField(typeof(string), "Name", hashPrefix, sharedVariable.Name);
            if (sharedVariable.IsShared)
            {
                SaveField(typeof(bool), "IsShared", hashPrefix, sharedVariable.IsShared);
            }
            if (sharedVariable.IsGlobal)
            {
                SaveField(typeof(bool), "IsGlobal", hashPrefix, sharedVariable.IsGlobal);
            }
            if (sharedVariable.IsDynamic)
            {
                SaveField(typeof(bool), "IsDynamic", hashPrefix, sharedVariable.IsDynamic);
            }
            if (!string.IsNullOrEmpty(sharedVariable.Tooltip))
            {
                SaveField(typeof(string), "Tooltip", hashPrefix, sharedVariable.Tooltip);
            }
            if (!string.IsNullOrEmpty(sharedVariable.PropertyMapping))
            {
                SaveField(typeof(string), "PropertyMapping", hashPrefix, sharedVariable.PropertyMapping);
                if (!object.Equals(sharedVariable.PropertyMappingOwner, null))
                {
                    SaveField(typeof(GameObject), "PropertyMappingOwner", hashPrefix, sharedVariable.PropertyMappingOwner);
                }
            }
            SaveFields(sharedVariable, hashPrefix);
        }

        private static void SaveFields(object obj, int hashPrefix)
        {
            fieldHashes.Clear();
            FieldInfo[] allFields = TaskUtility.GetAllFields(obj.GetType());
            for (int i = 0; i < allFields.Length; i++)
            {
                if (!BehaviorDesignerUtility.HasAttribute(allFields[i], typeof(NonSerializedAttribute)) && ((!allFields[i].IsPrivate && !allFields[i].IsFamily) || BehaviorDesignerUtility.HasAttribute(allFields[i], typeof(SerializeField))) && (!(obj is ParentTask) || !allFields[i].Name.Equals("children")))
                {
                    object value = allFields[i].GetValue(obj);
                    if (!object.ReferenceEquals(value, null))
                    {
                        SaveField(allFields[i].FieldType, allFields[i].Name, hashPrefix, value, allFields[i]);
                    }
                }
            }
        }

        private static void SaveField(Type fieldType, string fieldName, int hashPrefix, object value, FieldInfo fieldInfo = null)
        {
            int num = hashPrefix + BinaryDeserialization.StringHash(fieldType.Name.ToString(), fastHash: true) + BinaryDeserialization.StringHash(fieldName, fastHash: true);
            if (fieldHashes.Contains(num))
            {
                return;
            }
            fieldHashes.Add(num);
            fieldSerializationData.fieldNameHash.Add(num);
            fieldSerializationData.startIndex.Add(fieldIndex);
            if (typeof(IList).IsAssignableFrom(fieldType))
            {
                Type fieldType2;
                if (fieldType.IsArray)
                {
                    fieldType2 = fieldType.GetElementType();
                }
                else
                {
                    Type type = fieldType;
                    while (!type.IsGenericType)
                    {
                        type = type.BaseType;
                    }
                    fieldType2 = type.GetGenericArguments()[0];
                }
                if (!(value is IList list))
                {
                    AddByteData(IntToBytes(0));
                    return;
                }
                AddByteData(IntToBytes(list.Count));
                if (list.Count <= 0)
                {
                    return;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    if (object.ReferenceEquals(list[i], null))
                    {
                        AddByteData(IntToBytes(-1));
                    }
                    else
                    {
                        SaveField(fieldType2, i.ToString(), num / (i + 1), list[i], fieldInfo);
                    }
                }
            }
            else if (typeof(Task).IsAssignableFrom(fieldType))
            {
                if (fieldInfo != null && BehaviorDesignerUtility.HasAttribute(fieldInfo, typeof(InspectTaskAttribute)))
                {
                    AddByteData(StringToBytes(value.GetType().ToString()));
                    SaveFields(value, num);
                }
                else
                {
                    AddByteData(IntToBytes((value as Task).ID));
                }
            }
            else if (typeof(SharedVariable).IsAssignableFrom(fieldType))
            {
                SaveSharedVariable(value as SharedVariable, num);
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                AddByteData(IntToBytes(fieldSerializationData.unityObjects.Count));
                fieldSerializationData.unityObjects.Add(value as UnityEngine.Object);
            }
            else if (fieldType.Equals(typeof(int)))
            {
                AddByteData(IntToBytes((int)value));
            }
            else if (fieldType.Equals(typeof(ushort)))
            {
                AddByteData(UshortToBytes((ushort)value));
            }
            else if (fieldType.Equals(typeof(short)))
            {
                AddByteData(ShortToBytes((short)value));
            }
            else if (fieldType.Equals(typeof(uint)))
            {
                AddByteData(UIntToBytes((uint)value));
            }
            else if (fieldType.Equals(typeof(ulong)) || fieldType.Equals(typeof(ulong)))
            {
                AddByteData(ULongToBytes((ulong)value));
            }
            else if (fieldType.Equals(typeof(float)))
            {
                AddByteData(FloatToBytes((float)value));
            }
            else if (fieldType.Equals(typeof(double)))
            {
                AddByteData(DoubleToBytes((double)value));
            }
            else if (fieldType.Equals(typeof(long)))
            {
                AddByteData(LongToBytes((long)value));
            }
            else if (fieldType.Equals(typeof(bool)))
            {
                AddByteData(BoolToBytes((bool)value));
            }
            else if (fieldType.Equals(typeof(string)))
            {
                AddByteData(StringToBytes((string)value));
            }
            else if (fieldType.Equals(typeof(byte)))
            {
                AddByteData(ByteToBytes((byte)value));
            }
            else if (fieldType.IsEnum)
            {
                SaveField(Enum.GetUnderlyingType(fieldType), fieldName, num, value, fieldInfo);
            }
            else if (fieldType.Equals(typeof(Vector2)))
            {
                AddByteData(Vector2ToBytes((Vector2)value));
            }
            else if (fieldType.Equals(typeof(Vector2Int)))
            {
                AddByteData(Vector2IntToBytes((Vector2Int)value));
            }
            else if (fieldType.Equals(typeof(Vector3)))
            {
                AddByteData(Vector3ToBytes((Vector3)value));
            }
            else if (fieldType.Equals(typeof(Vector3Int)))
            {
                AddByteData(Vector3IntToBytes((Vector3Int)value));
            }
            else if (fieldType.Equals(typeof(Vector4)))
            {
                AddByteData(Vector4ToBytes((Vector4)value));
            }
            else if (fieldType.Equals(typeof(Quaternion)))
            {
                AddByteData(QuaternionToBytes((Quaternion)value));
            }
            else if (fieldType.Equals(typeof(Color)))
            {
                AddByteData(ColorToBytes((Color)value));
            }
            else if (fieldType.Equals(typeof(Rect)))
            {
                AddByteData(RectToBytes((Rect)value));
            }
            else if (fieldType.Equals(typeof(Matrix4x4)))
            {
                AddByteData(Matrix4x4ToBytes((Matrix4x4)value));
            }
            else if (fieldType.Equals(typeof(LayerMask)))
            {
                AddByteData(IntToBytes(((LayerMask)value).value));
            }
            else if (fieldType.Equals(typeof(AnimationCurve)))
            {
                AddByteData(AnimationCurveToBytes((AnimationCurve)value));
            }
            else if (fieldType.IsClass || (fieldType.IsValueType && !fieldType.IsPrimitive))
            {
                if (object.ReferenceEquals(value, null))
                {
                    value = Activator.CreateInstance(fieldType, nonPublic: true);
                }
                SaveFields(value, num);
            }
            else
            {
                Debug.LogError("Missing Serialization for " + fieldType);
            }
        }

        private static byte[] IntToBytes(int value)
        {
            return BitConverter.GetBytes(value);
        }

        private static byte[] ShortToBytes(short value)
        {
            return BitConverter.GetBytes(value);
        }

        private static byte[] UIntToBytes(uint value)
        {
            return BitConverter.GetBytes(value);
        }

        private static byte[] ULongToBytes(ulong value)
        {
            return BitConverter.GetBytes(value);
        }

        private static byte[] UshortToBytes(ushort value)
        {
            return BitConverter.GetBytes(value);
        }

        private static byte[] FloatToBytes(float value)
        {
            return BitConverter.GetBytes(value);
        }

        private static byte[] DoubleToBytes(double value)
        {
            return BitConverter.GetBytes(value);
        }

        private static byte[] LongToBytes(long value)
        {
            return BitConverter.GetBytes(value);
        }

        private static byte[] BoolToBytes(bool value)
        {
            return BitConverter.GetBytes(value);
        }

        private static byte[] StringToBytes(string str)
        {
            if (str == null)
            {
                str = string.Empty;
            }
            return Encoding.UTF8.GetBytes(str);
        }

        private static byte[] ByteToBytes(byte value)
        {
            return new byte[1] { value };
        }

        private static ICollection<byte> ColorToBytes(Color color)
        {
            List<byte> list = new List<byte>();
            list.AddRange(BitConverter.GetBytes(color.r));
            list.AddRange(BitConverter.GetBytes(color.g));
            list.AddRange(BitConverter.GetBytes(color.b));
            list.AddRange(BitConverter.GetBytes(color.a));
            return list;
        }

        private static ICollection<byte> Vector2ToBytes(Vector2 vector2)
        {
            List<byte> list = new List<byte>();
            list.AddRange(BitConverter.GetBytes(vector2.x));
            list.AddRange(BitConverter.GetBytes(vector2.y));
            return list;
        }

        private static ICollection<byte> Vector2IntToBytes(Vector2Int vector2)
        {
            List<byte> list = new List<byte>();
            list.AddRange(BitConverter.GetBytes(vector2.x));
            list.AddRange(BitConverter.GetBytes(vector2.y));
            return list;
        }

        private static ICollection<byte> Vector3ToBytes(Vector3 vector3)
        {
            List<byte> list = new List<byte>();
            list.AddRange(BitConverter.GetBytes(vector3.x));
            list.AddRange(BitConverter.GetBytes(vector3.y));
            list.AddRange(BitConverter.GetBytes(vector3.z));
            return list;
        }

        private static ICollection<byte> Vector3IntToBytes(Vector3Int vector3)
        {
            List<byte> list = new List<byte>();
            list.AddRange(BitConverter.GetBytes(vector3.x));
            list.AddRange(BitConverter.GetBytes(vector3.y));
            list.AddRange(BitConverter.GetBytes(vector3.z));
            return list;
        }

        private static ICollection<byte> Vector4ToBytes(Vector4 vector4)
        {
            List<byte> list = new List<byte>();
            list.AddRange(BitConverter.GetBytes(vector4.x));
            list.AddRange(BitConverter.GetBytes(vector4.y));
            list.AddRange(BitConverter.GetBytes(vector4.z));
            list.AddRange(BitConverter.GetBytes(vector4.w));
            return list;
        }

        private static ICollection<byte> QuaternionToBytes(Quaternion quaternion)
        {
            List<byte> list = new List<byte>();
            list.AddRange(BitConverter.GetBytes(quaternion.x));
            list.AddRange(BitConverter.GetBytes(quaternion.y));
            list.AddRange(BitConverter.GetBytes(quaternion.z));
            list.AddRange(BitConverter.GetBytes(quaternion.w));
            return list;
        }

        private static ICollection<byte> RectToBytes(Rect rect)
        {
            List<byte> list = new List<byte>();
            list.AddRange(BitConverter.GetBytes(rect.x));
            list.AddRange(BitConverter.GetBytes(rect.y));
            list.AddRange(BitConverter.GetBytes(rect.width));
            list.AddRange(BitConverter.GetBytes(rect.height));
            return list;
        }

        private static ICollection<byte> Matrix4x4ToBytes(Matrix4x4 matrix4x4)
        {
            List<byte> list = new List<byte>();
            list.AddRange(BitConverter.GetBytes(matrix4x4.m00));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m01));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m02));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m03));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m10));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m11));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m12));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m13));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m20));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m21));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m22));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m23));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m30));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m31));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m32));
            list.AddRange(BitConverter.GetBytes(matrix4x4.m33));
            return list;
        }

        private static ICollection<byte> AnimationCurveToBytes(AnimationCurve animationCurve)
        {
            List<byte> list = new List<byte>();
            Keyframe[] keys = animationCurve.keys;
            if (keys != null)
            {
                list.AddRange(BitConverter.GetBytes(keys.Length));
                for (int i = 0; i < keys.Length; i++)
                {
                    list.AddRange(BitConverter.GetBytes(keys[i].time));
                    list.AddRange(BitConverter.GetBytes(keys[i].value));
                    list.AddRange(BitConverter.GetBytes(keys[i].inTangent));
                    list.AddRange(BitConverter.GetBytes(keys[i].outTangent));
                }
            }
            else
            {
                list.AddRange(BitConverter.GetBytes(0));
            }
            list.AddRange(BitConverter.GetBytes((int)animationCurve.preWrapMode));
            list.AddRange(BitConverter.GetBytes((int)animationCurve.postWrapMode));
            return list;
        }

        private static void AddByteData(ICollection<byte> bytes)
        {
            fieldSerializationData.dataPosition.Add(fieldSerializationData.byteData.Count);
            if (bytes != null)
            {
                fieldSerializationData.byteData.AddRange(bytes);
            }
            fieldIndex++;
        }
    }
}