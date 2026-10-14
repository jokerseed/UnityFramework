using System;
using System.Collections.Generic;
using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Editor
{
    public class TypeNameComparer : IComparer<Type>
    {
        public int Compare(Type x, Type y)
        {
            string name = x.Name;
            string name2 = y.Name;
            int length = name.Length;
            int length2 = name2.Length;
            int i = 0;
            int j = 0;
            while (i < length && j < length2)
            {
                int num = 0;
                if (char.IsDigit(name[i]) && char.IsDigit(name[j]))
                {
                    string text = string.Empty;
                    for (; i < length && char.IsDigit(name[i]); i++)
                    {
                        text += name[i];
                    }
                    string text2 = string.Empty;
                    for (; j < length2 && char.IsDigit(name2[j]); j++)
                    {
                        text2 += name2[j];
                    }
                    int result = 0;
                    int.TryParse(text, out result);
                    int result2 = 0;
                    int.TryParse(text2, out result2);
                    num = result.CompareTo(result2);
                }
                else
                {
                    num = name[i].CompareTo(name2[j]);
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