using System;
using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Editor
{
    public class AlphanumComparator<T> : IComparer<T>
    {
        public int Compare(T x, T y)
        {
            string empty = string.Empty;
            if (x.GetType().IsSubclassOf(typeof(Type)))
            {
                Type type = x as Type;
                empty = TypePrefix(type) + "/";
                TaskCategoryAttribute[] array = null;
                if ((array = type.GetCustomAttributes(typeof(TaskCategoryAttribute), inherit: true) as TaskCategoryAttribute[]).Length > 0)
                {
                    string text = array[0].Category.TrimEnd(TaskUtility.TrimCharacters);
                    empty = empty + text + "/";
                }
                TaskNameAttribute[] array2 = null;
                empty = (((array2 = type.GetCustomAttributes(typeof(TaskNameAttribute), inherit: false) as TaskNameAttribute[]).Length <= 0) ? (empty + BehaviorDesignerUtility.SplitCamelCase(type.Name.ToString())) : (empty + array2[0].Name));
            }
            else if (x.GetType().IsSubclassOf(typeof(SharedVariable)))
            {
                string text2 = x.GetType().Name;
                if (text2.Length > 6 && text2.Substring(0, 6).Equals("Shared"))
                {
                    text2 = text2.Substring(6, text2.Length - 6);
                }
                empty = BehaviorDesignerUtility.SplitCamelCase(text2);
            }
            else
            {
                empty = BehaviorDesignerUtility.SplitCamelCase(x.ToString());
            }
            if (empty == null)
            {
                return 0;
            }
            string empty2 = string.Empty;
            if (y.GetType().IsSubclassOf(typeof(Type)))
            {
                Type type2 = y as Type;
                empty2 = TypePrefix(type2) + "/";
                TaskCategoryAttribute[] array3 = null;
                if ((array3 = type2.GetCustomAttributes(typeof(TaskCategoryAttribute), inherit: true) as TaskCategoryAttribute[]).Length > 0)
                {
                    string text3 = array3[0].Category.TrimEnd(TaskUtility.TrimCharacters);
                    empty2 = empty2 + text3 + "/";
                }
                TaskNameAttribute[] array4 = null;
                empty2 = (((array4 = type2.GetCustomAttributes(typeof(TaskNameAttribute), inherit: false) as TaskNameAttribute[]).Length <= 0) ? (empty2 + BehaviorDesignerUtility.SplitCamelCase(type2.Name.ToString())) : (empty2 + array4[0].Name));
            }
            else if (y.GetType().IsSubclassOf(typeof(SharedVariable)))
            {
                string text4 = y.GetType().Name;
                if (text4.Length > 6 && text4.Substring(0, 6).Equals("Shared"))
                {
                    text4 = text4.Substring(6, text4.Length - 6);
                }
                empty2 = BehaviorDesignerUtility.SplitCamelCase(text4);
            }
            else
            {
                empty2 = BehaviorDesignerUtility.SplitCamelCase(y.ToString());
            }
            if (empty2 == null)
            {
                return 0;
            }
            int length = empty.Length;
            int length2 = empty2.Length;
            int i = 0;
            int j = 0;
            while (i < length && j < length2)
            {
                int num = 0;
                if (char.IsDigit(empty[i]) && char.IsDigit(empty[j]))
                {
                    string text5 = string.Empty;
                    for (; i < length && char.IsDigit(empty[i]); i++)
                    {
                        text5 += empty[i];
                    }
                    string text6 = string.Empty;
                    for (; j < length2 && char.IsDigit(empty2[j]); j++)
                    {
                        text6 += empty2[j];
                    }
                    int result = 0;
                    int.TryParse(text5, out result);
                    int result2 = 0;
                    int.TryParse(text6, out result2);
                    num = result.CompareTo(result2);
                }
                else
                {
                    num = empty[i].CompareTo(empty2[j]);
                }
                if (num != 0)
                {
                    return num;
                }
                i++;
                j++;
            }
            return length - length2;
        }

        private string TypePrefix(Type t)
        {
            if (t.IsSubclassOf(typeof(BehaviorDesigner.Runtime.Tasks.Action)))
            {
                return "Action";
            }
            if (t.IsSubclassOf(typeof(Composite)))
            {
                return "Composite";
            }
            if (t.IsSubclassOf(typeof(Conditional)))
            {
                return "Conditional";
            }
            return "Decorator";
        }
    }
}