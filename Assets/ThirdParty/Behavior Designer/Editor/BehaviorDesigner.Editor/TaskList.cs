using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    [Serializable]
    public class TaskList : ScriptableObject
    {
        public enum TaskTypes
        {
            Action,
            Composite,
            Conditional,
            Decorator,
            Last
        }

        private class SearchableType
        {
            private Type mType;

            private bool mVisible = true;

            private string mName;

            public Type Type => mType;

            public bool Visible
            {
                get
                {
                    return mVisible;
                }
                set
                {
                    mVisible = value;
                }
            }

            public string Name => mName;

            public SearchableType(Type type)
            {
                mType = type;
                mName = BehaviorDesignerUtility.SplitCamelCase(mType.Name);
            }
        }

        private class CategoryList
        {
            private string mName = string.Empty;

            private string mFullpath = string.Empty;

            private List<CategoryList> mSubcategories;

            private List<SearchableType> mTasks;

            private bool mExpanded = true;

            private bool mVisible = true;

            private int mID;

            public string Name => mName;

            public string Fullpath => mFullpath;

            public List<CategoryList> Subcategories => mSubcategories;

            public List<SearchableType> Tasks => mTasks;

            public bool Expanded
            {
                get
                {
                    return mExpanded;
                }
                set
                {
                    mExpanded = value;
                }
            }

            public bool Visible
            {
                get
                {
                    return mVisible;
                }
                set
                {
                    mVisible = value;
                }
            }

            public int ID => mID;

            public CategoryList(string name, string fullpath, bool expanded, int id)
            {
                mName = name;
                mFullpath = fullpath;
                mExpanded = expanded;
                mID = id;
            }

            public void AddSubcategory(CategoryList category)
            {
                if (mSubcategories == null)
                {
                    mSubcategories = new List<CategoryList>();
                }
                mSubcategories.Add(category);
            }

            public void AddTask(Type taskType)
            {
                if (mTasks == null)
                {
                    mTasks = new List<SearchableType>();
                }
                mTasks.Add(new SearchableType(taskType));
            }
        }

        private List<CategoryList> mCategoryList;

        private List<CategoryList> mQuickCategoryList;

        private Dictionary<Type, TaskNameAttribute[]> mTaskNameAttribute = new Dictionary<Type, TaskNameAttribute[]>();

        private Vector2 mScrollPosition = Vector2.zero;

        private string mSearchString = string.Empty;

        private bool mFocusSearch;

        private Vector2 mQuickScrollPosition = Vector2.zero;

        private string mQuickSearchString = string.Empty;

        private bool mFocusQuickSearch;

        private int mSelectedQuickIndex;

        private Type mSelectedQuickIndexType;

        private int mQuickIndexCount;

        public void OnEnable()
        {
            base.hideFlags = HideFlags.HideAndDontSave;
        }

        public void Init()
        {
            mCategoryList = new List<CategoryList>();
            mQuickCategoryList = new List<CategoryList>();
            List<Type> list = new List<Type>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type[] types = assemblies[i].GetTypes();
                    for (int j = 0; j < types.Length; j++)
                    {
                        if (typeof(Task).IsAssignableFrom(types[j]) && !types[j].Equals(typeof(BehaviorReference)) && !types[j].IsAbstract && (types[j].IsSubclassOf(typeof(BehaviorDesigner.Runtime.Tasks.Action)) || types[j].IsSubclassOf(typeof(Composite)) || types[j].IsSubclassOf(typeof(Conditional)) || types[j].IsSubclassOf(typeof(Decorator))))
                        {
                            list.Add(types[j]);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
            list.Sort(new AlphanumComparator<Type>());
            Dictionary<string, CategoryList> dictionary = new Dictionary<string, CategoryList>();
            Dictionary<string, CategoryList> dictionary2 = new Dictionary<string, CategoryList>();
            string empty = string.Empty;
            TaskCategoryAttribute[] array = null;
            int id = 0;
            for (int k = 0; k < list.Count; k++)
            {
                empty = (list[k].IsSubclassOf(typeof(BehaviorDesigner.Runtime.Tasks.Action)) ? "Actions" : (list[k].IsSubclassOf(typeof(Composite)) ? "Composites" : ((!list[k].IsSubclassOf(typeof(Conditional))) ? "Decorators" : "Conditionals")));
                if ((array = list[k].GetCustomAttributes(typeof(TaskCategoryAttribute), inherit: true) as TaskCategoryAttribute[]).Length > 0)
                {
                    empty = empty + "/" + array[0].Category.TrimEnd(TaskUtility.TrimCharacters);
                }
                string text = string.Empty;
                CategoryList categoryList = null;
                CategoryList categoryList2 = null;
                string[] array2 = empty.Split('/');
                CategoryList categoryList3 = null;
                CategoryList categoryList4 = null;
                for (int l = 0; l < array2.Length; l++)
                {
                    if (l > 0)
                    {
                        text += "/";
                    }
                    text += array2[l];
                    if (!dictionary.ContainsKey(text))
                    {
                        categoryList = new CategoryList(array2[l], text, PreviouslyExpanded(id), id++);
                        categoryList2 = new CategoryList(array2[l], text, expanded: true, 0);
                        if (categoryList3 == null)
                        {
                            mCategoryList.Add(categoryList);
                            mQuickCategoryList.Add(categoryList2);
                        }
                        else
                        {
                            categoryList3.AddSubcategory(categoryList);
                            categoryList4.AddSubcategory(categoryList2);
                        }
                        dictionary.Add(text, categoryList);
                        dictionary2.Add(text, categoryList2);
                    }
                    else
                    {
                        categoryList = dictionary[text];
                        categoryList2 = dictionary2[text];
                    }
                    categoryList3 = categoryList;
                    categoryList4 = categoryList2;
                }
                categoryList = dictionary[text];
                categoryList.AddTask(list[k]);
                categoryList2 = dictionary2[text];
                categoryList2.AddTask(list[k]);
            }
            Search(BehaviorDesignerUtility.SplitCamelCase(mSearchString).ToLower().Replace(" ", string.Empty), mCategoryList);
            Search(BehaviorDesignerUtility.SplitCamelCase(mQuickSearchString).ToLower().Replace(" ", string.Empty), mQuickCategoryList);
        }

        public void AddTasksToMenu(ref GenericMenu genericMenu, Type selectedTaskType, string parentName, GenericMenu.MenuFunction2 menuFunction)
        {
            AddCategoryTasksToMenu(ref genericMenu, mCategoryList, selectedTaskType, parentName, menuFunction);
        }

        public void AddTaskTypesToMenu(int typeIndex, ref GenericMenu genericMenu, Type selectedTaskType, Type ignoredTaskType, string parentName, bool includeFullPath, GenericMenu.MenuFunction2 menuFunction)
        {
            if (typeIndex < 0 || typeIndex >= mCategoryList.Count)
            {
                return;
            }
            if (mCategoryList[typeIndex].Tasks != null)
            {
                for (int i = 0; i < mCategoryList[typeIndex].Tasks.Count; i++)
                {
                    if (mCategoryList[typeIndex].Tasks[i].Type == ignoredTaskType)
                    {
                        continue;
                    }
                    if (parentName.Equals(string.Empty))
                    {
                        if (includeFullPath)
                        {
                            genericMenu.AddItem(new GUIContent($"{mCategoryList[typeIndex].Fullpath}/{mCategoryList[typeIndex].Tasks[i].Name.ToString()}"), mCategoryList[typeIndex].Tasks[i].Type.Equals(selectedTaskType), menuFunction, mCategoryList[typeIndex].Tasks[i].Type);
                        }
                        else
                        {
                            genericMenu.AddItem(new GUIContent(mCategoryList[typeIndex].Tasks[i].Name.ToString()), mCategoryList[typeIndex].Tasks[i].Type.Equals(selectedTaskType), menuFunction, mCategoryList[typeIndex].Tasks[i].Type);
                        }
                    }
                    else if (includeFullPath)
                    {
                        genericMenu.AddItem(new GUIContent($"{parentName}/{mCategoryList[typeIndex].Fullpath}/{mCategoryList[typeIndex].Tasks[i].Name.ToString()}"), mCategoryList[typeIndex].Tasks[i].Type.Equals(selectedTaskType), menuFunction, mCategoryList[typeIndex].Tasks[i].Type);
                    }
                    else
                    {
                        genericMenu.AddItem(new GUIContent($"{parentName}/{mCategoryList[typeIndex].Tasks[i].Name.ToString()}"), mCategoryList[typeIndex].Tasks[i].Type.Equals(selectedTaskType), menuFunction, mCategoryList[typeIndex].Tasks[i].Type);
                    }
                }
            }
            AddCategoryTasksToMenu(ref genericMenu, mCategoryList[typeIndex].Subcategories, selectedTaskType, parentName, menuFunction);
        }

        private void AddCategoryTasksToMenu(ref GenericMenu genericMenu, List<CategoryList> categoryList, Type selectedTaskType, string parentName, GenericMenu.MenuFunction2 menuFunction)
        {
            for (int i = 0; i < categoryList.Count; i++)
            {
                if (categoryList[i].Subcategories != null)
                {
                    AddCategoryTasksToMenu(ref genericMenu, categoryList[i].Subcategories, selectedTaskType, parentName, menuFunction);
                }
                if (categoryList[i].Tasks == null)
                {
                    continue;
                }
                for (int j = 0; j < categoryList[i].Tasks.Count; j++)
                {
                    if (parentName.Equals(string.Empty))
                    {
                        genericMenu.AddItem(new GUIContent($"{categoryList[i].Fullpath}/{categoryList[i].Tasks[j].Name.ToString()}"), categoryList[i].Tasks[j].Type.Equals(selectedTaskType), menuFunction, categoryList[i].Tasks[j].Type);
                    }
                    else
                    {
                        genericMenu.AddItem(new GUIContent($"{parentName}/{categoryList[i].Fullpath}/{categoryList[i].Tasks[j].Name.ToString()}"), categoryList[i].Tasks[j].Type.Equals(selectedTaskType), menuFunction, categoryList[i].Tasks[j].Type);
                    }
                }
            }
        }

        public void FocusSearchField(bool quickTaskList, bool clearQuickSearchString)
        {
            if (quickTaskList)
            {
                mFocusQuickSearch = true;
                if (clearQuickSearchString)
                {
                    mQuickSearchString = string.Empty;
                    mSelectedQuickIndex = 0;
                    mSelectedQuickIndexType = null;
                    Search(string.Empty, mQuickCategoryList);
                }
            }
            else
            {
                mFocusSearch = true;
            }
        }

        public void SelectQuickTask(BehaviorDesignerWindow window)
        {
            if (!(mSelectedQuickIndexType == null))
            {
                window.AddTask(mSelectedQuickIndexType, useMousePosition: true);
            }
        }

        public void MoveSelectedQuickTask(bool increase)
        {
            mSelectedQuickIndex = Mathf.Min(Mathf.Max(0, mSelectedQuickIndex + (increase ? 1 : (-1))), mQuickIndexCount);
        }

        public void UpdateQuickTaskSearch()
        {
            Search(BehaviorDesignerUtility.SplitCamelCase(mQuickSearchString).ToLower().Replace(" ", string.Empty), mQuickCategoryList);
        }

        public void DrawTaskList(BehaviorDesignerWindow window, bool enabled)
        {
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("Search");
            string value = GUILayout.TextField(mSearchString, GUI.skin.FindStyle("ToolbarSeachTextField"));
            if (mFocusSearch)
            {
                GUI.FocusControl("Search");
                mFocusSearch = false;
            }
            if (!mSearchString.Equals(value))
            {
                mSearchString = value;
                Search(BehaviorDesignerUtility.SplitCamelCase(mSearchString).ToLower().Replace(" ", string.Empty), mCategoryList);
            }
            if (GUILayout.Button(string.Empty, (!mSearchString.Equals(string.Empty)) ? GUI.skin.FindStyle("ToolbarSeachCancelButton") : GUI.skin.FindStyle("ToolbarSeachCancelButtonEmpty")))
            {
                mSearchString = string.Empty;
                Search(string.Empty, mCategoryList);
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();
            BehaviorDesignerUtility.DrawContentSeperator(2);
            GUILayout.Space(4f);
            mScrollPosition = GUILayout.BeginScrollView(mScrollPosition);
            GUI.enabled = enabled;
            if (mCategoryList.Count > 1)
            {
                DrawCategory(window, mCategoryList[1], quickList: false);
            }
            if (mCategoryList.Count > 3)
            {
                DrawCategory(window, mCategoryList[3], quickList: false);
            }
            if (mCategoryList.Count > 0)
            {
                DrawCategory(window, mCategoryList[0], quickList: false);
            }
            if (mCategoryList.Count > 2)
            {
                DrawCategory(window, mCategoryList[2], quickList: false);
            }
            GUI.enabled = true;
            GUILayout.EndScrollView();
        }

        public void DrawQuickTaskList(BehaviorDesignerWindow window, bool enabled)
        {
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("QuickSearch");
            string value = GUILayout.TextField(mQuickSearchString, GUI.skin.FindStyle("ToolbarSeachTextField"));
            if (mFocusQuickSearch)
            {
                GUI.FocusControl("QuickSearch");
                mFocusQuickSearch = false;
            }
            if (!mQuickSearchString.Equals(value))
            {
                mQuickSearchString = value;
                mSelectedQuickIndex = 0;
                Search(BehaviorDesignerUtility.SplitCamelCase(mQuickSearchString).ToLower().Replace(" ", string.Empty), mQuickCategoryList);
            }
            if (GUILayout.Button(string.Empty, (!mSearchString.Equals(string.Empty)) ? GUI.skin.FindStyle("ToolbarSeachCancelButton") : GUI.skin.FindStyle("ToolbarSeachCancelButtonEmpty")))
            {
                mQuickSearchString = string.Empty;
                Search(string.Empty, mQuickCategoryList);
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();
            BehaviorDesignerUtility.DrawContentSeperator(2);
            GUILayout.Space(4f);
            mQuickScrollPosition = GUILayout.BeginScrollView(mQuickScrollPosition, false, true);
            GUI.enabled = enabled;
            if (mQuickCategoryList.Count > 0)
            {
                mQuickIndexCount = 0;
                for (int i = 0; i < mQuickCategoryList.Count; i++)
                {
                    DrawCategory(window, mQuickCategoryList[i], quickList: true);
                }
                if (mQuickIndexCount == 0)
                {
                    GUILayout.Label("(No Tasks Found)");
                }
            }
            GUI.enabled = true;
            GUILayout.EndScrollView();
        }

        private void DrawCategory(BehaviorDesignerWindow window, CategoryList category, bool quickList)
        {
            if (!category.Visible)
            {
                return;
            }
            if (!quickList)
            {
                category.Expanded = EditorGUILayout.Foldout(category.Expanded, category.Name, BehaviorDesignerUtility.TaskFoldoutGUIStyle);
                SetExpanded(category.ID, category.Expanded);
            }
            if (!category.Expanded)
            {
                return;
            }
            if (!quickList)
            {
                EditorGUI.indentLevel++;
            }
            string text = string.Empty;
            if (category.Tasks != null)
            {
                for (int i = 0; i < category.Tasks.Count; i++)
                {
                    if (!category.Tasks[i].Visible)
                    {
                        continue;
                    }
                    if (quickList)
                    {
                        string text2 = category.Fullpath.TrimEnd(TaskUtility.TrimCharacters);
                        if (text != text2)
                        {
                            text = text2;
                            string[] array = text.Split('/');
                            if (array.Length > 1)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(-2f);
                                GUILayout.Label(array[array.Length - 1]);
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(EditorGUI.indentLevel * 16);
                    TaskNameAttribute[] value = null;
                    if (!mTaskNameAttribute.TryGetValue(category.Tasks[i].Type, out value))
                    {
                        value = category.Tasks[i].Type.GetCustomAttributes(typeof(TaskNameAttribute), inherit: false) as TaskNameAttribute[];
                        mTaskNameAttribute.Add(category.Tasks[i].Type, value);
                    }
                    string text3 = ((value == null || value.Length <= 0) ? category.Tasks[i].Name : value[0].Name);
                    if (quickList && mQuickIndexCount == mSelectedQuickIndex)
                    {
                        GUI.backgroundColor = new Color(1f, 0.64f, 0f);
                        mSelectedQuickIndexType = category.Tasks[i].Type;
                    }
                    mQuickIndexCount++;
                    if (GUILayout.Button(text3, EditorStyles.miniButton, GUILayout.MaxWidth(((!quickList) ? 300 : 200) - EditorGUI.indentLevel * 16 - 24)))
                    {
                        window.AddTask(category.Tasks[i].Type, quickList);
                    }
                    GUI.backgroundColor = Color.white;
                    GUILayout.Space(3f);
                    GUILayout.EndHorizontal();
                }
            }
            if (category.Subcategories != null)
            {
                DrawCategoryTaskList(window, category.Subcategories, quickList);
            }
            if (!quickList)
            {
                EditorGUI.indentLevel--;
            }
        }

        private void DrawCategoryTaskList(BehaviorDesignerWindow window, List<CategoryList> categoryList, bool quickList)
        {
            for (int i = 0; i < categoryList.Count; i++)
            {
                DrawCategory(window, categoryList[i], quickList);
            }
        }

        private bool Search(string searchString, List<CategoryList> categoryList)
        {
            bool result = searchString.Equals(string.Empty);
            for (int i = 0; i < categoryList.Count; i++)
            {
                bool flag = false;
                categoryList[i].Visible = false;
                if (categoryList[i].Subcategories != null && Search(searchString, categoryList[i].Subcategories))
                {
                    categoryList[i].Visible = true;
                    result = true;
                }
                if (BehaviorDesignerUtility.SplitCamelCase(categoryList[i].Name).ToLower().Replace(" ", string.Empty)
                    .Contains(searchString))
                {
                    result = true;
                    flag = true;
                    categoryList[i].Visible = true;
                    if (categoryList[i].Subcategories != null)
                    {
                        MarkVisible(categoryList[i].Subcategories);
                    }
                }
                if (categoryList[i].Tasks == null)
                {
                    continue;
                }
                for (int j = 0; j < categoryList[i].Tasks.Count; j++)
                {
                    categoryList[i].Tasks[j].Visible = searchString.Equals(string.Empty);
                    if (flag || categoryList[i].Tasks[j].Name.ToLower().Replace(" ", string.Empty).Contains(searchString))
                    {
                        categoryList[i].Tasks[j].Visible = true;
                        result = true;
                        categoryList[i].Visible = true;
                    }
                }
            }
            return result;
        }

        private void MarkVisible(List<CategoryList> categoryList)
        {
            for (int i = 0; i < categoryList.Count; i++)
            {
                categoryList[i].Visible = true;
                if (categoryList[i].Subcategories != null)
                {
                    MarkVisible(categoryList[i].Subcategories);
                }
                if (categoryList[i].Tasks != null)
                {
                    for (int j = 0; j < categoryList[i].Tasks.Count; j++)
                    {
                        categoryList[i].Tasks[j].Visible = true;
                    }
                }
            }
        }

        private bool PreviouslyExpanded(int id)
        {
            return EditorPrefs.GetBool($"BehaviorDesignerTaskList{id}", defaultValue: true);
        }

        private void SetExpanded(int id, bool visible)
        {
            EditorPrefs.SetBool($"BehaviorDesignerTaskList{id}", visible);
        }
    }
}