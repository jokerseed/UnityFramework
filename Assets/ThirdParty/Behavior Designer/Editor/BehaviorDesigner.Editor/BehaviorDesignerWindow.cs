using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace BehaviorDesigner.Editor
{
    public class BehaviorDesignerWindow : EditorWindow
    {
        private enum BreadcrumbMenuType
        {
            GameObjectBehavior,
            GameObject,
            Behavior
        }

        public delegate void TaskCallbackHandler(BehaviorSource behaviorSource, Task task);

        [SerializeField]
        public static BehaviorDesignerWindow instance;

        private Rect mGraphRect;

        private Rect mGraphScrollRect;

        private Rect mFileToolBarRect;

        private Rect mDebugToolBarRect;

        private Rect mPropertyToolbarRect;

        private Rect mPropertyBoxRect;

        private Rect mPreferencesPaneRect;

        private Rect mFindDialogueRect;

        private Rect mQuickTaskListRect;

        private Vector2 mGraphScrollSize = new Vector2(20000f, 20000f);

        private bool mSizesInitialized;

        private float mPrevScreenWidth = -1f;

        private float mPrevScreenHeight = -1f;

        private bool mPropertiesPanelOnLeft = true;

        private Vector2 mCurrentMousePosition = Vector2.zero;

        private Vector2 mGraphScrollPosition = new Vector2(-1f, -1f);

        private Vector2 mGraphOffset = Vector2.zero;

        private float mGraphZoom = 1f;

        private float mGraphZoomMultiplier = 1f;

        private int mBehaviorToolbarSelection = 2;

        private string[] mBehaviorToolbarStrings = new string[4] { "Behavior", "Tasks", "Variables", "Inspector" };

        private string mGraphStatus = string.Empty;

        private Material mGridMaterial;

        private Vector2 mSelectStartPosition = Vector2.zero;

        private Rect mSelectionArea;

        private bool mIsSelecting;

        private bool mIsDragging;

        private bool mKeepTasksSelected;

        private bool mNodeClicked;

        private Vector2 mDragDelta = Vector2.zero;

        private bool mCommandDown;

        private bool mUpdateNodeTaskMap;

        private bool mStepApplication;

        private Dictionary<NodeDesigner, Task> mNodeDesignerTaskMap;

        private bool mEditorAtBreakpoint;

        [SerializeField]
        private List<ErrorDetails> mErrorDetails;

        private bool mShowFindDialogue;

        private string mFindTaskValue;

        private SharedVariable mFindSharedVariable;

        private bool mShowQuickTaskList;

        private GenericMenu mRightClickMenu;

        [SerializeField]
        private GenericMenu mBreadcrumbGameObjectBehaviorMenu;

        [SerializeField]
        private GenericMenu mBreadcrumbGameObjectMenu;

        [SerializeField]
        private GenericMenu mBreadcrumbBehaviorMenu;

        [SerializeField]
        private GenericMenu mReferencedBehaviorsMenu;

        private bool mShowRightClickMenu;

        private bool mShowPrefPane;

        [SerializeField]
        private GraphDesigner mGraphDesigner;

        private TaskInspector mTaskInspector;

        private TaskList mTaskList;

        private VariableInspector mVariableInspector;

        [SerializeField]
        private UnityEngine.Object mActiveObject;

        private UnityEngine.Object mPrevActiveObject;

        private BehaviorSource mActiveBehaviorSource;

        private BehaviorSource mExternalParent;

        private int mActiveBehaviorID = -1;

        [SerializeField]
        private List<UnityEngine.Object> mBehaviorSourceHistory = new List<UnityEngine.Object>();

        [SerializeField]
        private int mBehaviorSourceHistoryIndex = -1;

        private BehaviorManager mBehaviorManager;

        private bool mLockActiveGameObject;

        private bool mLoadedFromInspector;

        [SerializeField]
        private bool mIsPlaying;

        private UnityWebRequest mUpdateCheckRequest;

        private DateTime mLastUpdateCheck = DateTime.MinValue;

        private string mLatestVersion;

        private bool mTakingScreenshot;

        private float mScreenshotStartGraphZoom;

        private Vector2 mScreenshotStartGraphOffset;

        private Texture2D mScreenshotTexture;

        private Rect mScreenshotGraphSize;

        private Vector2 mScreenshotGraphOffset;

        private string mScreenshotPath;

        public TaskCallbackHandler onAddTask;

        public TaskCallbackHandler onRemoveTask;

        private List<TaskSerializer> mCopiedTasks;

        public List<ErrorDetails> ErrorDetails => mErrorDetails;

        public TaskList TaskList => mTaskList;

        public BehaviorSource ActiveBehaviorSource => mActiveBehaviorSource;

        public int ActiveBehaviorID => mActiveBehaviorID;

        private DateTime LastUpdateCheck
        {
            get
            {
                try
                {
                    if (mLastUpdateCheck != DateTime.MinValue)
                    {
                        return mLastUpdateCheck;
                    }
                    mLastUpdateCheck = DateTime.Parse(EditorPrefs.GetString("BehaviorDesignerLastUpdateCheck", "1/1/1971 00:00:01"), CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    mLastUpdateCheck = DateTime.UtcNow;
                }
                return mLastUpdateCheck;
            }
            set
            {
                mLastUpdateCheck = value;
                EditorPrefs.SetString("BehaviorDesignerLastUpdateCheck", mLastUpdateCheck.ToString(CultureInfo.InvariantCulture));
            }
        }

        public string LatestVersion
        {
            get
            {
                if (!string.IsNullOrEmpty(mLatestVersion))
                {
                    return mLatestVersion;
                }
                mLatestVersion = EditorPrefs.GetString("BehaviorDesignerLatestVersion", "1.7.4".ToString());
                return mLatestVersion;
            }
            set
            {
                mLatestVersion = value;
                EditorPrefs.SetString("BehaviorDesignerLatestVersion", mLatestVersion);
            }
        }

        public TaskCallbackHandler OnAddTask
        {
            get
            {
                return onAddTask;
            }
            set
            {
                onAddTask = (TaskCallbackHandler)Delegate.Combine(onAddTask, value);
            }
        }

        public TaskCallbackHandler OnRemoveTask
        {
            get
            {
                return onRemoveTask;
            }
            set
            {
                onRemoveTask = (TaskCallbackHandler)Delegate.Combine(onRemoveTask, value);
            }
        }

        [MenuItem("Tools/Behavior Designer/Editor", false, 0)]
        public static void ShowWindow()
        {
            BehaviorDesignerWindow window = EditorWindow.GetWindow<BehaviorDesignerWindow>(utility: false, "Behavior Designer");
            window.wantsMouseMove = true;
            window.minSize = new Vector2(700f, 100f);
            window.Init();
            BehaviorDesignerPreferences.InitPrefernces();
            if (BehaviorDesignerPreferences.GetBool(BDPreferences.ShowWelcomeScreen))
            {
                WelcomeScreen.ShowWindow();
            }
        }

        public void OnEnable()
        {
            mIsPlaying = EditorApplication.isPlaying;
            mSizesInitialized = false;
            Repaint();
            mGraphZoomMultiplier = BehaviorDesignerPreferences.GetFloat(BDPreferences.ZoomSpeedMultiplier);
            EditorApplication.projectChanged += OnProjectWindowChange;
            EditorApplication.playModeStateChanged += OnPlaymodeStateChange;
            Undo.undoRedoPerformed = (Undo.UndoRedoCallback)Delegate.Combine(Undo.undoRedoPerformed, new Undo.UndoRedoCallback(OnUndoRedo));
            Init();
            SetBehaviorManager();
        }

        public void OnFocus()
        {
            instance = this;
            base.wantsMouseMove = true;
            Init();
            if (!mLockActiveGameObject)
            {
                mActiveObject = Selection.activeObject;
                ReloadPreviousBehavior();
            }
            else if (mActiveBehaviorSource == null)
            {
                ReloadPreviousBehavior();
            }
            UpdateGraphStatus();
            if (mShowFindDialogue)
            {
                Find();
            }
        }

        public void OnSelectionChange()
        {
            if (!mLockActiveGameObject)
            {
                UpdateTree(firstLoad: false);
            }
            else
            {
                ReloadPreviousBehavior();
            }
            UpdateGraphStatus();
        }

        public void OnProjectWindowChange()
        {
            ReloadPreviousBehavior();
            ClearBreadcrumbMenu();
        }

        private void ReloadPreviousBehavior()
        {
            if (mActiveObject != null)
            {
                if ((bool)(mActiveObject as GameObject))
                {
                    GameObject gameObject = mActiveObject as GameObject;
                    int num = -1;
                    Behavior[] components = gameObject.GetComponents<Behavior>();
                    for (int i = 0; i < components.Length; i++)
                    {
                        if (components[i].GetInstanceID() == mActiveBehaviorID)
                        {
                            num = i;
                            break;
                        }
                    }
                    if (num != -1)
                    {
                        LoadBehavior(components[num].GetBehaviorSource(), loadPrevBehavior: true, inspectorLoad: false);
                    }
                    else if (components.Count() > 0)
                    {
                        LoadBehavior(components[0].GetBehaviorSource(), loadPrevBehavior: true, inspectorLoad: false);
                    }
                    else if (mGraphDesigner != null)
                    {
                        ClearGraph();
                    }
                }
                else if (mActiveObject is ExternalBehavior)
                {
                    ExternalBehavior externalBehavior = mActiveObject as ExternalBehavior;
                    BehaviorSource behaviorSource = externalBehavior.BehaviorSource;
                    if (externalBehavior.BehaviorSource.Owner == null)
                    {
                        externalBehavior.BehaviorSource.Owner = externalBehavior;
                    }
                    LoadBehavior(behaviorSource, loadPrevBehavior: true, inspectorLoad: false);
                }
                else if (mGraphDesigner != null)
                {
                    mActiveObject = null;
                    ClearGraph();
                }
            }
            else if (mGraphDesigner != null)
            {
                ClearGraph();
                Repaint();
            }
        }

        private void UpdateTree(bool firstLoad)
        {
            bool flag = firstLoad;
            if (Selection.activeObject != null)
            {
                bool loadPrevBehavior = false;
                if (!Selection.activeObject.Equals(mActiveObject))
                {
                    mActiveObject = Selection.activeObject;
                    flag = true;
                }
                BehaviorSource behaviorSource = null;
                GameObject gameObject = mActiveObject as GameObject;
                if (gameObject != null && gameObject.GetComponent<Behavior>() != null)
                {
                    if (flag)
                    {
                        if (mActiveObject.Equals(mPrevActiveObject) && mActiveBehaviorID != -1)
                        {
                            loadPrevBehavior = true;
                            int num = -1;
                            Behavior[] components = (mActiveObject as GameObject).GetComponents<Behavior>();
                            for (int i = 0; i < components.Length; i++)
                            {
                                if (components[i].GetInstanceID() == mActiveBehaviorID)
                                {
                                    num = i;
                                    break;
                                }
                            }
                            if (num != -1)
                            {
                                behaviorSource = gameObject.GetComponents<Behavior>()[num].GetBehaviorSource();
                            }
                            else if (components.Count() > 0)
                            {
                                behaviorSource = gameObject.GetComponents<Behavior>()[0].GetBehaviorSource();
                            }
                        }
                        else
                        {
                            behaviorSource = gameObject.GetComponents<Behavior>()[0].GetBehaviorSource();
                        }
                    }
                    else
                    {
                        Behavior[] components2 = gameObject.GetComponents<Behavior>();
                        bool flag2 = false;
                        if (mActiveBehaviorSource != null)
                        {
                            for (int j = 0; j < components2.Length; j++)
                            {
                                if (components2[j].Equals(mActiveBehaviorSource.Owner))
                                {
                                    flag2 = true;
                                    break;
                                }
                            }
                        }
                        if (!flag2)
                        {
                            behaviorSource = gameObject.GetComponents<Behavior>()[0].GetBehaviorSource();
                        }
                        else
                        {
                            behaviorSource = mActiveBehaviorSource;
                            loadPrevBehavior = true;
                        }
                    }
                }
                else if (mActiveObject is ExternalBehavior)
                {
                    ExternalBehavior externalBehavior = mActiveObject as ExternalBehavior;
                    if (externalBehavior.BehaviorSource.Owner == null)
                    {
                        externalBehavior.BehaviorSource.Owner = externalBehavior;
                    }
                    if (flag && mActiveObject.Equals(mPrevActiveObject))
                    {
                        loadPrevBehavior = true;
                    }
                    behaviorSource = externalBehavior.BehaviorSource;
                }
                else
                {
                    mPrevActiveObject = null;
                }
                if (behaviorSource != null)
                {
                    LoadBehavior(behaviorSource, loadPrevBehavior, inspectorLoad: false);
                }
                else if (behaviorSource == null)
                {
                    ClearGraph();
                }
            }
            else
            {
                if (mActiveObject != null && mActiveBehaviorSource != null)
                {
                    mPrevActiveObject = mActiveObject;
                }
                mActiveObject = null;
                ClearGraph();
            }
        }

        private void Init()
        {
            if (mTaskList == null)
            {
                mTaskList = ScriptableObject.CreateInstance<TaskList>();
            }
            if (mVariableInspector == null)
            {
                mVariableInspector = ScriptableObject.CreateInstance<VariableInspector>();
            }
            if (mGraphDesigner == null)
            {
                mGraphDesigner = ScriptableObject.CreateInstance<GraphDesigner>();
            }
            if (mTaskInspector == null)
            {
                mTaskInspector = ScriptableObject.CreateInstance<TaskInspector>();
            }
            if (mGridMaterial == null)
            {
                mGridMaterial = new Material(Shader.Find("Hidden/Behavior Designer/Grid"));
                mGridMaterial.hideFlags = HideFlags.HideAndDontSave;
                mGridMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
            }
            mTaskList.Init();
            FieldInspector.Init();
            ClearBreadcrumbMenu();
        }

        public void UpdateGraphStatus()
        {
            if (mActiveObject == null || mGraphDesigner == null || (mActiveObject as GameObject == null && mActiveObject as ExternalBehavior == null))
            {
                mGraphStatus = "Select a GameObject";
            }
            else if (mActiveObject as GameObject != null && object.ReferenceEquals((mActiveObject as GameObject).GetComponent<Behavior>(), null))
            {
                mGraphStatus = "Right Click, Add a Behavior Tree Component";
            }
            else if (ViewOnlyMode() && mActiveBehaviorSource != null)
            {
                ExternalBehavior externalBehavior = (mActiveBehaviorSource.Owner.GetObject() as Behavior).ExternalBehavior;
                if (externalBehavior != null)
                {
                    mGraphStatus = externalBehavior.BehaviorSource.ToString() + " (View Only Mode)";
                }
                else
                {
                    mGraphStatus = mActiveBehaviorSource.ToString() + " (View Only Mode)";
                }
            }
            else if (!mGraphDesigner.HasEntryNode())
            {
                mGraphStatus = "Add a Task";
            }
            else if (IsReferencingTasks())
            {
                mGraphStatus = "Select tasks to reference (right click to exit)";
            }
            else if (mActiveBehaviorSource != null && mActiveBehaviorSource.Owner != null && mActiveBehaviorSource.Owner.GetObject() != null)
            {
                if (mExternalParent != null)
                {
                    mGraphStatus = mExternalParent.ToString() + " (Editing External Behavior)";
                }
                else
                {
                    mGraphStatus = mActiveBehaviorSource.ToString();
                }
            }
        }

        private void BuildBreadcrumbMenus(BreadcrumbMenuType menuType)
        {
            Dictionary<BehaviorSource, string> dictionary = new Dictionary<BehaviorSource, string>();
            Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
            HashSet<UnityEngine.Object> hashSet = new HashSet<UnityEngine.Object>();
            List<BehaviorSource> list = new List<BehaviorSource>();
            Behavior[] array = Resources.FindObjectsOfTypeAll(typeof(Behavior)) as Behavior[];
            for (int num = array.Length - 1; num > -1; num--)
            {
                BehaviorSource behaviorSource = array[num].GetBehaviorSource();
                if (behaviorSource.Owner == null)
                {
                    behaviorSource.Owner = array[num];
                }
                list.Add(behaviorSource);
            }
            ExternalBehavior[] array2 = Resources.FindObjectsOfTypeAll(typeof(ExternalBehavior)) as ExternalBehavior[];
            for (int num2 = array2.Length - 1; num2 > -1; num2--)
            {
                BehaviorSource behaviorSource2 = array2[num2].GetBehaviorSource();
                if (behaviorSource2.Owner == null)
                {
                    behaviorSource2.Owner = array2[num2];
                }
                list.Add(behaviorSource2);
            }
            list.Sort(new AlphanumComparator<BehaviorSource>());
            for (int i = 0; i < list.Count; i++)
            {
                UnityEngine.Object @object = list[i].Owner.GetObject();
                if (menuType == BreadcrumbMenuType.Behavior)
                {
                    if (@object is Behavior)
                    {
                        if (!(@object as Behavior).gameObject.Equals(mActiveObject))
                        {
                            continue;
                        }
                    }
                    else if (!(@object as ExternalBehavior).Equals(mActiveObject))
                    {
                        continue;
                    }
                }
                if (menuType == BreadcrumbMenuType.GameObject && @object is Behavior)
                {
                    if (hashSet.Contains((@object as Behavior).gameObject))
                    {
                        continue;
                    }
                    hashSet.Add((@object as Behavior).gameObject);
                }
                string text = string.Empty;
                if (@object is Behavior)
                {
                    switch (menuType)
                    {
                        case BreadcrumbMenuType.GameObjectBehavior:
                            text = list[i].ToString();
                            break;
                        case BreadcrumbMenuType.GameObject:
                            text = (@object as Behavior).gameObject.name;
                            break;
                        case BreadcrumbMenuType.Behavior:
                            text = list[i].behaviorName;
                            break;
                    }
                    if (!AssetDatabase.GetAssetPath(@object).Equals(string.Empty))
                    {
                        text += " (prefab)";
                    }
                }
                else
                {
                    text = list[i].ToString() + " (external)";
                }
                int value = 0;
                if (dictionary2.TryGetValue(text, out value))
                {
                    value = (dictionary2[text] = value + 1);
                    text += $" ({value + 1})";
                }
                else
                {
                    dictionary2.Add(text, 0);
                }
                dictionary.Add(list[i], text);
            }
            switch (menuType)
            {
                case BreadcrumbMenuType.GameObjectBehavior:
                    mBreadcrumbGameObjectBehaviorMenu = new GenericMenu();
                    break;
                case BreadcrumbMenuType.GameObject:
                    mBreadcrumbGameObjectMenu = new GenericMenu();
                    break;
                case BreadcrumbMenuType.Behavior:
                    mBreadcrumbBehaviorMenu = new GenericMenu();
                    break;
            }
            foreach (KeyValuePair<BehaviorSource, string> item in dictionary)
            {
                switch (menuType)
                {
                    case BreadcrumbMenuType.GameObjectBehavior:
                        mBreadcrumbGameObjectBehaviorMenu.AddItem(new GUIContent(item.Value), item.Key.Equals(mActiveBehaviorSource), BehaviorSelectionCallback, item.Key);
                        break;
                    case BreadcrumbMenuType.GameObject:
                        {
                            bool flag = false;
                            flag = ((!(item.Key.Owner.GetObject() is ExternalBehavior)) ? (item.Key.Owner.GetObject() as Behavior).gameObject.Equals(mActiveObject) : (item.Key.Owner.GetObject() as ExternalBehavior).GetObject().Equals(mActiveObject));
                            mBreadcrumbGameObjectMenu.AddItem(new GUIContent(item.Value), flag, BehaviorSelectionCallback, item.Key);
                            break;
                        }
                    case BreadcrumbMenuType.Behavior:
                        mBreadcrumbBehaviorMenu.AddItem(new GUIContent(item.Value), item.Key.Equals(mActiveBehaviorSource), BehaviorSelectionCallback, item.Key);
                        break;
                }
            }
        }

        private void ClearBreadcrumbMenu()
        {
            mBreadcrumbGameObjectBehaviorMenu = null;
            mBreadcrumbGameObjectMenu = null;
            mBreadcrumbBehaviorMenu = null;
        }

        private void BuildRightClickMenu(NodeDesigner clickedNode)
        {
            if (mActiveObject == null)
            {
                return;
            }
            mRightClickMenu = new GenericMenu();
            if (clickedNode == null && (!EditorApplication.isPlaying || (bool)(mActiveObject as ExternalBehavior)) && !ViewOnlyMode())
            {
                mTaskList.AddTasksToMenu(ref mRightClickMenu, null, "Add Task", AddTaskCallback);
                if (mCopiedTasks != null && mCopiedTasks.Count > 0)
                {
                    mRightClickMenu.AddItem(new GUIContent("Paste Tasks"), on: false, PasteNodes);
                }
                else
                {
                    mRightClickMenu.AddDisabledItem(new GUIContent("Paste Tasks"));
                }
            }
            if (clickedNode != null && !clickedNode.IsEntryDisplay)
            {
                if (mGraphDesigner.SelectedNodes.Count == 1)
                {
                    mRightClickMenu.AddItem(new GUIContent("Edit Script"), on: false, OpenInFileEditor, clickedNode);
                    mRightClickMenu.AddItem(new GUIContent("Locate Script"), on: false, SelectInProject, clickedNode);
                    if (!ViewOnlyMode())
                    {
                        mRightClickMenu.AddItem(new GUIContent((!clickedNode.Task.Disabled) ? "Disable" : "Enable"), on: false, ToggleEnableState, clickedNode);
                        if (clickedNode.IsParent)
                        {
                            mRightClickMenu.AddItem(new GUIContent((!clickedNode.Task.NodeData.Collapsed) ? "Collapse" : "Expand"), on: false, ToggleCollapseState, clickedNode);
                        }
                        mRightClickMenu.AddItem(new GUIContent((!clickedNode.Task.NodeData.IsBreakpoint) ? "Set Breakpoint" : "Remove Breakpoint"), on: false, ToggleBreakpoint, clickedNode);
                    }
                }
                if ((!EditorApplication.isPlaying || (bool)(mActiveObject as ExternalBehavior)) && !ViewOnlyMode())
                {
                    mRightClickMenu.AddItem(new GUIContent(string.Format("Copy Task{0}", (mGraphDesigner.SelectedNodes.Count <= 1) ? string.Empty : "s")), on: false, CopyNodes);
                    if (mCopiedTasks != null && mCopiedTasks.Count > 0)
                    {
                        mRightClickMenu.AddItem(new GUIContent(string.Format("Paste Task{0}", (mCopiedTasks.Count <= 1) ? string.Empty : "s")), on: false, PasteNodes);
                    }
                    else
                    {
                        mRightClickMenu.AddDisabledItem(new GUIContent("Paste Tasks"));
                    }
                    mRightClickMenu.AddItem(new GUIContent(string.Format("Duplicate Task{0}", (mGraphDesigner.SelectedNodes.Count <= 1) ? string.Empty : "s")), on: false, DuplicateNodes);
                    if (mGraphDesigner.SelectedNodes.Count > 0)
                    {
                        mTaskList.AddTasksToMenu(ref mRightClickMenu, (mGraphDesigner.SelectedNodes.Count != 1) ? null : mGraphDesigner.SelectedNodes[0].Task.GetType(), "Replace", ReplaceTasksCallback);
                    }
                    mRightClickMenu.AddItem(new GUIContent(string.Format("Delete Task{0}", (mGraphDesigner.SelectedNodes.Count <= 1) ? string.Empty : "s")), on: false, DeleteNodes);
                }
            }
            if ((!EditorApplication.isPlaying || (bool)(mActiveObject as ExternalBehavior)) && mActiveObject as GameObject != null)
            {
                if (clickedNode != null && !clickedNode.IsEntryDisplay)
                {
                    mRightClickMenu.AddSeparator(string.Empty);
                }
                mRightClickMenu.AddItem(new GUIContent("Add Behavior Tree"), on: false, AddBehavior);
                if (mActiveBehaviorSource != null)
                {
                    mRightClickMenu.AddItem(new GUIContent("Remove Behavior Tree"), on: false, RemoveBehavior);
                    mRightClickMenu.AddItem(new GUIContent("Save As External Behavior Tree"), on: false, SaveAsAsset);
                }
            }
            mShowRightClickMenu = mRightClickMenu.GetItemCount() > 0;
        }

        public void Update()
        {
            if (mTakingScreenshot)
            {
                Repaint();
            }
        }

        public void OnGUI()
        {
            mCurrentMousePosition = Event.current.mousePosition;
            SetupSizes();
            if (!mSizesInitialized)
            {
                mSizesInitialized = true;
                if (!mLockActiveGameObject || mActiveObject == null)
                {
                    UpdateTree(firstLoad: true);
                }
                else
                {
                    ReloadPreviousBehavior();
                }
            }
            Draw();
            HandleEvents();
        }

        public void OnPlaymodeStateChange(PlayModeStateChange change)
        {
            OnPlaymodeStateChange();
        }

        public void OnPlaymodeStateChange()
        {
            if (EditorApplication.isPlaying && !EditorApplication.isPaused)
            {
                if (mBehaviorManager == null)
                {
                    SetBehaviorManager();
                    if (mBehaviorManager == null)
                    {
                        return;
                    }
                }
                if (mBehaviorManager.BreakpointTree != null && mEditorAtBreakpoint)
                {
                    mEditorAtBreakpoint = false;
                    mBehaviorManager.BreakpointTree = null;
                }
            }
            else if (EditorApplication.isPlaying && EditorApplication.isPaused)
            {
                if (!(mBehaviorManager != null) || !(mBehaviorManager.BreakpointTree != null))
                {
                    return;
                }
                if (!mEditorAtBreakpoint)
                {
                    mEditorAtBreakpoint = true;
                    if (BehaviorDesignerPreferences.GetBool(BDPreferences.SelectOnBreakpoint) && !mLockActiveGameObject)
                    {
                        Selection.activeObject = mBehaviorManager.BreakpointTree;
                        LoadBehavior(mBehaviorManager.BreakpointTree.GetBehaviorSource(), mActiveBehaviorSource == mBehaviorManager.BreakpointTree.GetBehaviorSource(), inspectorLoad: false);
                    }
                }
                else
                {
                    mEditorAtBreakpoint = false;
                    mBehaviorManager.BreakpointTree = null;
                }
            }
            else if (!EditorApplication.isPlaying)
            {
                mBehaviorManager = null;
            }
        }

        private void SetBehaviorManager()
        {
            mBehaviorManager = BehaviorManager.instance;
            if (!(mBehaviorManager == null))
            {
                BehaviorManager behaviorManager = mBehaviorManager;
                behaviorManager.OnTaskBreakpoint = (BehaviorManager.BehaviorManagerHandler)Delegate.Combine(behaviorManager.OnTaskBreakpoint, new BehaviorManager.BehaviorManagerHandler(OnTaskBreakpoint));
                mUpdateNodeTaskMap = true;
            }
        }

        public void OnTaskBreakpoint()
        {
            EditorApplication.isPaused = true;
            Repaint();
        }

        private void OnPreferenceChange(BDPreferences pref, object value)
        {
            switch (pref)
            {
                case BDPreferences.CompactMode:
                    mGraphDesigner.GraphDirty();
                    break;
                case BDPreferences.BinarySerialization:
                    SaveBehavior();
                    break;
                case BDPreferences.ErrorChecking:
                    CheckForErrors();
                    break;
                case BDPreferences.ShowSceneIcon:
                case BDPreferences.GizmosViewMode:
                    GizmoManager.UpdateAllGizmos();
                    break;
                case BDPreferences.ZoomSpeedMultiplier:
                    mGraphZoomMultiplier = (float)value;
                    break;
            }
        }

        public void OnInspectorUpdate()
        {
            if (mStepApplication)
            {
                EditorApplication.Step();
                mStepApplication = false;
            }
            if (EditorApplication.isPlaying && !EditorApplication.isPaused && mActiveBehaviorSource != null && mBehaviorManager != null)
            {
                if (mUpdateNodeTaskMap)
                {
                    UpdateNodeTaskMap();
                }
                if (mBehaviorManager.BreakpointTree != null)
                {
                    mBehaviorManager.BreakpointTree = null;
                }
                Repaint();
            }
            if (Application.isPlaying && mBehaviorManager == null)
            {
                SetBehaviorManager();
            }
            if (mBehaviorManager != null && mBehaviorManager.Dirty)
            {
                if (mActiveBehaviorSource != null)
                {
                    LoadBehavior(mActiveBehaviorSource, loadPrevBehavior: true, inspectorLoad: false);
                }
                mBehaviorManager.Dirty = false;
            }
            if (!EditorApplication.isPlaying && mIsPlaying)
            {
                ReloadPreviousBehavior();
            }
            mIsPlaying = EditorApplication.isPlaying;
            UpdateGraphStatus();
            UpdateCheck();
        }

        private void UpdateNodeTaskMap()
        {
            if (!mUpdateNodeTaskMap || !(mBehaviorManager != null))
            {
                return;
            }
            Behavior behavior = mActiveBehaviorSource.Owner as Behavior;
            List<Task> taskList = mBehaviorManager.GetTaskList(behavior);
            if (taskList == null)
            {
                return;
            }
            mNodeDesignerTaskMap = new Dictionary<NodeDesigner, Task>();
            for (int i = 0; i < taskList.Count; i++)
            {
                NodeDesigner nodeDesigner = taskList[i].NodeData.NodeDesigner as NodeDesigner;
                if (nodeDesigner != null && !mNodeDesignerTaskMap.ContainsKey(nodeDesigner))
                {
                    mNodeDesignerTaskMap.Add(nodeDesigner, taskList[i]);
                }
            }
            mUpdateNodeTaskMap = false;
        }

        private bool Draw()
        {
            bool result = false;
            Color color = GUI.color;
            Color backgroundColor = GUI.backgroundColor;
            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
            DrawFileToolbar();
            DrawDebugToolbar();
            DrawPropertiesBox();
            if (DrawGraphArea())
            {
                result = true;
            }
            DrawQuickTaskList();
            DrawFindDialogue();
            DrawPreferencesPane();
            if (mTakingScreenshot)
            {
                GUI.DrawTexture(new Rect(0f, 0f, base.position.width, base.position.height + 22f), BehaviorDesignerUtility.ScreenshotBackgroundTexture, ScaleMode.StretchToFill, alphaBlend: false);
            }
            GUI.color = color;
            GUI.backgroundColor = backgroundColor;
            return result;
        }

        private void DrawFileToolbar()
        {
            GUILayout.BeginArea(mFileToolBarRect, EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(BehaviorDesignerUtility.HistoryBackwardTexture, EditorStyles.toolbarButton) && (mBehaviorSourceHistoryIndex > 0 || (mActiveBehaviorSource == null && mBehaviorSourceHistoryIndex == 0)))
            {
                BehaviorSource behaviorSource = null;
                if (mActiveBehaviorSource == null)
                {
                    mBehaviorSourceHistoryIndex++;
                }
                while (behaviorSource == null && mBehaviorSourceHistory.Count > 0 && mBehaviorSourceHistoryIndex > 0)
                {
                    mBehaviorSourceHistoryIndex--;
                    behaviorSource = BehaviorSourceFromIBehaviorHistory(mBehaviorSourceHistory[mBehaviorSourceHistoryIndex] as IBehavior);
                    if (behaviorSource == null || behaviorSource.Owner == null || behaviorSource.Owner.GetObject() == null)
                    {
                        mBehaviorSourceHistory.RemoveAt(mBehaviorSourceHistoryIndex);
                        behaviorSource = null;
                    }
                }
                if (behaviorSource != null)
                {
                    LoadBehavior(behaviorSource, loadPrevBehavior: false);
                }
            }
            if (GUILayout.Button(BehaviorDesignerUtility.HistoryForwardTexture, EditorStyles.toolbarButton))
            {
                BehaviorSource behaviorSource2 = null;
                if (mBehaviorSourceHistoryIndex < mBehaviorSourceHistory.Count - 1)
                {
                    mBehaviorSourceHistoryIndex++;
                    while (behaviorSource2 == null && mBehaviorSourceHistoryIndex < mBehaviorSourceHistory.Count && mBehaviorSourceHistoryIndex > 0)
                    {
                        behaviorSource2 = BehaviorSourceFromIBehaviorHistory(mBehaviorSourceHistory[mBehaviorSourceHistoryIndex] as IBehavior);
                        if (behaviorSource2 == null || behaviorSource2.Owner == null || behaviorSource2.Owner.GetObject() == null)
                        {
                            mBehaviorSourceHistory.RemoveAt(mBehaviorSourceHistoryIndex);
                            behaviorSource2 = null;
                        }
                    }
                }
                if (behaviorSource2 != null)
                {
                    LoadBehavior(behaviorSource2, loadPrevBehavior: false);
                }
            }
            if (GUILayout.Button("...", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                BuildBreadcrumbMenus(BreadcrumbMenuType.GameObjectBehavior);
                mBreadcrumbGameObjectBehaviorMenu.ShowAsContext();
            }
            string text = ((!(mActiveObject as GameObject != null) && !(mActiveObject as ExternalBehavior != null)) ? "(None Selected)" : mActiveObject.name);
            if (GUILayout.Button(text, EditorStyles.toolbarPopup, GUILayout.Width(140f)))
            {
                BuildBreadcrumbMenus(BreadcrumbMenuType.GameObject);
                mBreadcrumbGameObjectMenu.ShowAsContext();
            }
            string text2 = ((mActiveBehaviorSource == null) ? "(None Selected)" : mActiveBehaviorSource.behaviorName);
            if (GUILayout.Button(text2, EditorStyles.toolbarPopup, GUILayout.Width(140f)) && mActiveBehaviorSource != null)
            {
                BuildBreadcrumbMenus(BreadcrumbMenuType.Behavior);
                mBreadcrumbBehaviorMenu.ShowAsContext();
            }
            if (GUILayout.Button("Referenced Behaviors", EditorStyles.toolbarPopup, GUILayout.Width(140f)) && mActiveBehaviorSource != null)
            {
                List<BehaviorSource> list = mGraphDesigner.FindReferencedBehaviors();
                if (list.Count > 0)
                {
                    list.Sort(new AlphanumComparator<BehaviorSource>());
                    mReferencedBehaviorsMenu = new GenericMenu();
                    for (int i = 0; i < list.Count; i++)
                    {
                        mReferencedBehaviorsMenu.AddItem(new GUIContent(list[i].ToString()), on: false, BehaviorSelectionCallback, list[i]);
                    }
                    mReferencedBehaviorsMenu.ShowAsContext();
                }
            }
            if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                if (mActiveBehaviorSource != null)
                {
                    RemoveBehavior();
                }
                else
                {
                    EditorUtility.DisplayDialog("Unable to Remove Behavior Tree", "No behavior tree selected.", "OK");
                }
            }
            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                if (mActiveObject != null)
                {
                    AddBehavior();
                }
                else
                {
                    EditorUtility.DisplayDialog("Unable to Add Behavior Tree", "No GameObject is selected.", "OK");
                }
            }
            if (GUILayout.Button("Lock", (!mLockActiveGameObject) ? EditorStyles.toolbarButton : BehaviorDesignerUtility.ToolbarButtonSelectionGUIStyle, GUILayout.Width(42f)))
            {
                if (mActiveObject != null)
                {
                    mLockActiveGameObject = !mLockActiveGameObject;
                    if (!mLockActiveGameObject)
                    {
                        UpdateTree(firstLoad: false);
                    }
                }
                else if (mLockActiveGameObject)
                {
                    mLockActiveGameObject = false;
                }
                else
                {
                    EditorUtility.DisplayDialog("Unable to Lock GameObject", "No GameObject is selected.", "OK");
                }
            }
            GUI.enabled = mActiveBehaviorSource == null || mExternalParent == null;
            if (GUILayout.Button("Export", EditorStyles.toolbarButton, GUILayout.Width(46f)))
            {
                if (mActiveBehaviorSource != null)
                {
                    if ((bool)(mActiveBehaviorSource.Owner.GetObject() as Behavior))
                    {
                        SaveAsAsset();
                    }
                    else
                    {
                        SaveAsPrefab();
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Unable to Save Behavior Tree", "Select a behavior tree from within the scene.", "OK");
                }
            }
            GUI.enabled = true;
            if (GUILayout.Button("Find", (!mShowFindDialogue) ? EditorStyles.toolbarButton : BehaviorDesignerUtility.ToolbarButtonSelectionGUIStyle, GUILayout.Width(40f)))
            {
                mShowFindDialogue = !mShowFindDialogue;
                if (mShowFindDialogue && mShowPrefPane)
                {
                    mShowPrefPane = false;
                }
                else if (!mShowFindDialogue)
                {
                    ClearFindResults();
                }
            }
            if (GUILayout.Button("Preferences", (!mShowPrefPane) ? EditorStyles.toolbarButton : BehaviorDesignerUtility.ToolbarButtonSelectionGUIStyle, GUILayout.Width(80f)))
            {
                mShowPrefPane = !mShowPrefPane;
                if (mShowPrefPane && mShowFindDialogue)
                {
                    mShowFindDialogue = false;
                    ClearFindResults();
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawDebugToolbar()
        {
            GUILayout.BeginArea(mDebugToolBarRect, EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(BehaviorDesignerUtility.PlayTexture, (!EditorApplication.isPlaying) ? EditorStyles.toolbarButton : BehaviorDesignerUtility.ToolbarButtonSelectionGUIStyle, GUILayout.Width(40f)))
            {
                EditorApplication.isPlaying = !EditorApplication.isPlaying;
            }
            if (GUILayout.Button(BehaviorDesignerUtility.PauseTexture, (!EditorApplication.isPaused) ? EditorStyles.toolbarButton : BehaviorDesignerUtility.ToolbarButtonSelectionGUIStyle, GUILayout.Width(40f)))
            {
                EditorApplication.isPaused = !EditorApplication.isPaused;
            }
            if (GUILayout.Button(BehaviorDesignerUtility.StepTexture, EditorStyles.toolbarButton, GUILayout.Width(40f)) && EditorApplication.isPlaying)
            {
                mStepApplication = true;
            }
            if (mErrorDetails != null && mErrorDetails.Count > 0 && GUILayout.Button(new GUIContent(mErrorDetails.Count + " Error" + ((mErrorDetails.Count <= 1) ? string.Empty : "s"), BehaviorDesignerUtility.SmallErrorIconTexture), BehaviorDesignerUtility.ToolbarButtonLeftAlignGUIStyle, GUILayout.Width(85f)))
            {
                ErrorWindow.ShowWindow();
            }
            GUILayout.FlexibleSpace();
            Version version = new Version("1.7.4");
            try
            {
                if (version.CompareTo(new Version(LatestVersion)) < 0)
                {
                    GUILayout.Label("Behavior Designer " + LatestVersion + " is now available.", BehaviorDesignerUtility.ToolbarLabelGUIStyle);
                }
            }
            catch (Exception)
            {
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawFindDialogue()
        {
            if (!mShowFindDialogue)
            {
                return;
            }
            GUILayout.BeginArea(mFindDialogueRect, BehaviorDesignerUtility.PreferencesPaneGUIStyle);
            EditorGUILayout.LabelField("Find", BehaviorDesignerUtility.LabelTitleGUIStyle);
            GUIContent gUIContent = new GUIContent("Task");
            Vector2 vector = GUI.skin.label.CalcSize(gUIContent);
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = vector.x + 50f;
            mFindTaskValue = EditorGUILayout.TextField(gUIContent, mFindTaskValue);
            EditorGUIUtility.labelWidth = labelWidth;
            string[] names = null;
            int globalStartIndex = -1;
            int variablesOfType = FieldInspector.GetVariablesOfType(null, mFindSharedVariable != null && mFindSharedVariable.IsGlobal, (mFindSharedVariable == null) ? string.Empty : mFindSharedVariable.Name, mActiveBehaviorSource, out names, ref globalStartIndex, getAll: true, addDynamic: false);
            if (names == null || names.Length == 0)
            {
                names = new string[1] { "(None)" };
            }
            gUIContent.text = "Variable";
            vector = GUI.skin.label.CalcSize(gUIContent);
            labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = vector.x + 30f;
            int num = EditorGUILayout.Popup("Variable", variablesOfType, names, BehaviorDesignerUtility.SharedVariableToolbarPopup);
            EditorGUIUtility.labelWidth = labelWidth;
            if (num != variablesOfType)
            {
                if (num == 0)
                {
                    mFindSharedVariable = null;
                }
                else if (globalStartIndex != -1 && num >= globalStartIndex)
                {
                    mFindSharedVariable = GlobalVariables.Instance.GetVariable(names[num].Substring(8, names[num].Length - 8));
                }
                else
                {
                    mFindSharedVariable = mActiveBehaviorSource.GetVariable(names[num]);
                }
            }
            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                ClearFindResults();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
            if (GUI.changed)
            {
                Find();
            }
        }

        private void Find()
        {
            mGraphDesigner.Find(mFindTaskValue, mFindSharedVariable);
        }

        private void ClearFindResults()
        {
            if (!string.IsNullOrEmpty(mFindTaskValue) || mFindSharedVariable != null)
            {
                mFindTaskValue = string.Empty;
                mFindSharedVariable = null;
            }
        }

        private void DrawQuickTaskList()
        {
            if (mShowQuickTaskList)
            {
                GUILayout.BeginArea(mQuickTaskListRect, BehaviorDesignerUtility.PreferencesPaneGUIStyle);
                mTaskList.DrawQuickTaskList(this, !ViewOnlyMode());
                GUILayout.EndArea();
            }
        }

        private void DrawPreferencesPane()
        {
            if (mShowPrefPane)
            {
                GUILayout.BeginArea(mPreferencesPaneRect, BehaviorDesignerUtility.PreferencesPaneGUIStyle);
                BehaviorDesignerPreferences.DrawPreferencesPane(OnPreferenceChange);
                GUILayout.EndArea();
            }
        }

        private void DrawPropertiesBox()
        {
            GUILayout.BeginArea(mPropertyToolbarRect, EditorStyles.toolbar);
            int num = mBehaviorToolbarSelection;
            mBehaviorToolbarSelection = GUILayout.Toolbar(mBehaviorToolbarSelection, mBehaviorToolbarStrings, EditorStyles.toolbarButton);
            GUILayout.EndArea();
            GUILayout.BeginArea(mPropertyBoxRect, BehaviorDesignerUtility.PropertyBoxGUIStyle);
            if (mBehaviorToolbarSelection == 0)
            {
                if (mActiveBehaviorSource != null)
                {
                    GUILayout.Space(3f);
                    BehaviorSource behaviorSource = ((mExternalParent == null) ? mActiveBehaviorSource : mExternalParent);
                    if (behaviorSource.Owner as Behavior != null)
                    {
                        bool externalModification = false;
                        bool showOptions = false;
                        if (BehaviorInspector.DrawInspectorGUI(behaviorSource.Owner as Behavior, new SerializedObject(behaviorSource.Owner as Behavior), fromInspector: false, ref externalModification, ref showOptions, ref showOptions))
                        {
                            BehaviorDesignerUtility.SetObjectDirty(behaviorSource.Owner.GetObject());
                            if (externalModification)
                            {
                                LoadBehavior(behaviorSource, loadPrevBehavior: false, inspectorLoad: false);
                            }
                        }
                    }
                    else
                    {
                        bool showVariables = false;
                        ExternalBehaviorInspector.DrawInspectorGUI(behaviorSource, fromInspector: false, ref showVariables);
                    }
                }
                else
                {
                    GUILayout.Space(5f);
                    GUILayout.Label("No behavior tree selected. Create a new behavior tree or select one from the hierarchy.", BehaviorDesignerUtility.LabelWrapGUIStyle, GUILayout.Width(285f));
                }
            }
            else if (mBehaviorToolbarSelection == 1)
            {
                mTaskList.DrawTaskList(this, !ViewOnlyMode());
                if (num != 1)
                {
                    mTaskList.FocusSearchField(quickTaskList: false, clearQuickSearchString: false);
                }
            }
            else if (mBehaviorToolbarSelection == 2)
            {
                if (mActiveBehaviorSource != null)
                {
                    BehaviorSource behaviorSource2 = ((mExternalParent == null) ? mActiveBehaviorSource : mExternalParent);
                    if (mVariableInspector.DrawVariables(behaviorSource2))
                    {
                        SaveBehavior();
                    }
                    if (num != 2)
                    {
                        mVariableInspector.FocusNameField();
                    }
                }
                else
                {
                    GUILayout.Space(5f);
                    GUILayout.Label("No behavior tree selected. Create a new behavior tree or select one from the hierarchy.", BehaviorDesignerUtility.LabelWrapGUIStyle, GUILayout.Width(285f));
                }
            }
            else if (mBehaviorToolbarSelection == 3)
            {
                if (mGraphDesigner.SelectedNodes.Count == 1 && !mGraphDesigner.SelectedNodes[0].IsEntryDisplay)
                {
                    Task task = mGraphDesigner.SelectedNodes[0].Task;
                    if (mNodeDesignerTaskMap != null && mNodeDesignerTaskMap.Count > 0)
                    {
                        NodeDesigner nodeDesigner = mGraphDesigner.SelectedNodes[0].Task.NodeData.NodeDesigner as NodeDesigner;
                        if (nodeDesigner != null && mNodeDesignerTaskMap.ContainsKey(nodeDesigner))
                        {
                            task = mNodeDesignerTaskMap[nodeDesigner];
                        }
                    }
                    if (mTaskInspector.DrawTaskInspector(mActiveBehaviorSource, mTaskList, task, !ViewOnlyMode()) && (!EditorApplication.isPlaying || (bool)(mActiveObject as ExternalBehavior)))
                    {
                        SaveBehavior();
                    }
                }
                else
                {
                    GUILayout.Space(5f);
                    if (mGraphDesigner.SelectedNodes.Count > 1)
                    {
                        GUILayout.Label("Only one task can be selected at a time to\n view its properties.", BehaviorDesignerUtility.LabelWrapGUIStyle, GUILayout.Width(285f));
                    }
                    else
                    {
                        GUILayout.Label("Select a task from the tree to\nview its properties.", BehaviorDesignerUtility.LabelWrapGUIStyle, GUILayout.Width(285f));
                    }
                }
            }
            GUILayout.EndArea();
        }

        private bool DrawGraphArea()
        {
            if (Event.current.type != EventType.ScrollWheel && !mTakingScreenshot)
            {
                Vector2 vector = GUI.BeginScrollView(new Rect(mGraphRect.x, mGraphRect.y, mGraphRect.width + 15f, mGraphRect.height + 15f), mGraphScrollPosition, new Rect(0f, 0f, mGraphScrollSize.x, mGraphScrollSize.y), alwaysShowHorizontal: true, alwaysShowVertical: true);
                if (vector != mGraphScrollPosition && Event.current.type != EventType.DragUpdated && Event.current.type != EventType.Ignore)
                {
                    mGraphOffset -= (vector - mGraphScrollPosition) / mGraphZoom;
                    mGraphScrollPosition = vector;
                    mGraphDesigner.GraphDirty();
                }
                GUI.EndScrollView();
            }
            GUI.Box(mGraphRect, string.Empty, BehaviorDesignerUtility.GraphBackgroundGUIStyle);
            DrawGrid();
            EditorZoomArea.Begin(mGraphRect, mGraphZoom);
            if (!GetMousePositionInGraph(out var mousePosition))
            {
                mousePosition = new Vector2(-1f, -1f);
            }
            bool result = false;
            if (mGraphDesigner != null && mGraphDesigner.DrawNodes(mousePosition, mGraphOffset))
            {
                result = true;
            }
            if (mTakingScreenshot && Event.current.type == EventType.Repaint)
            {
                RenderScreenshotTile();
            }
            if (mIsSelecting)
            {
                GUI.Box(GetSelectionArea(), string.Empty, BehaviorDesignerUtility.SelectionGUIStyle);
            }
            EditorZoomArea.End();
            DrawGraphStatus();
            DrawSelectedTaskDescription();
            return result;
        }

        private void DrawGrid()
        {
            if (BehaviorDesignerPreferences.GetBool(BDPreferences.SnapToGrid) && Event.current.type == EventType.Repaint)
            {
                mGridMaterial.SetPass((!EditorGUIUtility.isProSkin) ? 1 : 0);
                GL.PushMatrix();
                GL.Begin(1);
                DrawGridLines(10f * mGraphZoom, new Vector2(mGraphOffset.x % 10f * mGraphZoom, mGraphOffset.y % 10f * mGraphZoom));
                GL.End();
                GL.PopMatrix();
                mGridMaterial.SetPass((!EditorGUIUtility.isProSkin) ? 3 : 2);
                GL.PushMatrix();
                GL.Begin(1);
                DrawGridLines(50f * mGraphZoom, new Vector2(mGraphOffset.x % 50f * mGraphZoom, mGraphOffset.y % 50f * mGraphZoom));
                GL.End();
                GL.PopMatrix();
            }
        }

        private void DrawGridLines(float gridSize, Vector2 offset)
        {
            float num = mGraphRect.x + offset.x;
            if (offset.x < 0f)
            {
                num += gridSize;
            }
            for (float num2 = num; num2 < mGraphRect.x + mGraphRect.width; num2 += gridSize)
            {
                DrawLine(new Vector2(num2, mGraphRect.y), new Vector2(num2, mGraphRect.y + mGraphRect.height));
            }
            float num3 = mGraphRect.y + offset.y;
            if (offset.y < 0f)
            {
                num3 += gridSize;
            }
            for (float num4 = num3; num4 < mGraphRect.y + mGraphRect.height; num4 += gridSize)
            {
                DrawLine(new Vector2(mGraphRect.x, num4), new Vector2(mGraphRect.x + mGraphRect.width, num4));
            }
        }

        private void DrawLine(Vector2 p1, Vector2 p2)
        {
            GL.Vertex(p1);
            GL.Vertex(p2);
        }

        private void DrawGraphStatus()
        {
            if (!mGraphStatus.Equals(string.Empty))
            {
                GUI.Label(new Rect(mGraphRect.x + 5f, mGraphRect.y + 5f, mGraphRect.width, 30f), mGraphStatus, BehaviorDesignerUtility.GraphStatusGUIStyle);
            }
        }

        private void DrawSelectedTaskDescription()
        {
            TaskDescriptionAttribute[] array;
            if (BehaviorDesignerPreferences.GetBool(BDPreferences.ShowTaskDescription) && mGraphDesigner.SelectedNodes.Count == 1 && (array = mGraphDesigner.SelectedNodes[0].Task.GetType().GetCustomAttributes(typeof(TaskDescriptionAttribute), inherit: false) as TaskDescriptionAttribute[]).Length > 0)
            {
                BehaviorDesignerUtility.TaskCommentGUIStyle.CalcMinMaxWidth(new GUIContent(array[0].Description), out var _, out var maxWidth);
                float width = Mathf.Min(400f, maxWidth + 20f);
                float num = Mathf.Min(300f, BehaviorDesignerUtility.TaskCommentGUIStyle.CalcHeight(new GUIContent(array[0].Description), width)) + 3f;
                GUI.Box(new Rect(mGraphRect.x + 5f, mGraphRect.yMax - num - 5f, width, num), string.Empty, BehaviorDesignerUtility.TaskDescriptionGUIStyle);
                GUI.Box(new Rect(mGraphRect.x + 2f, mGraphRect.yMax - num - 5f, width, num), array[0].Description, BehaviorDesignerUtility.TaskCommentGUIStyle);
            }
        }

        private void AddBehavior()
        {
            if (EditorApplication.isPlaying || !(Selection.activeGameObject != null))
            {
                return;
            }
            GameObject activeGameObject = Selection.activeGameObject;
            mActiveObject = Selection.activeObject;
            mGraphDesigner = ScriptableObject.CreateInstance<GraphDesigner>();
            Type typeWithinAssembly = TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.BehaviorTree");
            Behavior behavior = BehaviorUndo.AddComponent(activeGameObject, typeWithinAssembly) as Behavior;
            Behavior[] components = activeGameObject.GetComponents<Behavior>();
            HashSet<string> hashSet = new HashSet<string>();
            string empty = string.Empty;
            for (int i = 0; i < components.Length; i++)
            {
                empty = components[i].GetBehaviorSource().behaviorName;
                int num = 2;
                while (hashSet.Contains(empty))
                {
                    empty = $"{components[i].GetBehaviorSource().behaviorName} {num}";
                    num++;
                }
                components[i].GetBehaviorSource().behaviorName = empty;
                hashSet.Add(components[i].GetBehaviorSource().behaviorName);
            }
            LoadBehavior(behavior.GetBehaviorSource(), loadPrevBehavior: false);
            Repaint();
            if (BehaviorDesignerPreferences.GetBool(BDPreferences.AddGameGUIComponent))
            {
                typeWithinAssembly = TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.BehaviorGameGUI");
                BehaviorUndo.AddComponent(activeGameObject, typeWithinAssembly);
            }
        }

        private void RemoveBehavior()
        {
            if (!EditorApplication.isPlaying && mActiveObject as GameObject != null && (mActiveBehaviorSource.EntryTask == null || (mActiveBehaviorSource.EntryTask != null && EditorUtility.DisplayDialog("Remove Behavior Tree", "Are you sure you want to remove this behavior tree?", "Yes", "No"))))
            {
                GameObject gameObject = mActiveObject as GameObject;
                int num = IndexForBehavior(mActiveBehaviorSource.Owner);
                BehaviorUndo.DestroyObject(mActiveBehaviorSource.Owner.GetObject(), registerScene: true);
                num--;
                if (num == -1 && gameObject.GetComponents<Behavior>().Length > 0)
                {
                    num = 0;
                }
                if (num > -1)
                {
                    LoadBehavior(gameObject.GetComponents<Behavior>()[num].GetBehaviorSource(), loadPrevBehavior: true);
                }
                else
                {
                    ClearGraph();
                }
                ClearBreadcrumbMenu();
                Repaint();
            }
        }

        private int IndexForBehavior(IBehavior behavior)
        {
            if ((bool)(behavior.GetObject() as Behavior))
            {
                Behavior[] components = (behavior.GetObject() as Behavior).gameObject.GetComponents<Behavior>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i].Equals(behavior))
                    {
                        return i;
                    }
                }
                return -1;
            }
            return 0;
        }

        public NodeDesigner AddTask(Type type, bool useMousePosition)
        {
            if ((mActiveObject as GameObject == null && mActiveObject as ExternalBehavior == null) || (EditorApplication.isPlaying && !(mActiveObject as ExternalBehavior)))
            {
                return null;
            }
            Vector2 mousePosition = new Vector2(mGraphRect.width / (2f * mGraphZoom), 150f);
            if (useMousePosition)
            {
                if (mShowQuickTaskList)
                {
                    mousePosition = (mQuickTaskListRect.position - new Vector2(mQuickTaskListRect.width, 0f)) / mGraphZoom;
                }
                else
                {
                    GetMousePositionInGraph(out mousePosition);
                }
            }
            mousePosition -= mGraphOffset;
            mShowQuickTaskList = false;
            GameObject gameObject = mActiveObject as GameObject;
            if (gameObject != null && gameObject.GetComponent<Behavior>() == null)
            {
                AddBehavior();
            }
            BehaviorUndo.RegisterUndo("Add", mActiveBehaviorSource.Owner.GetObject());
            NodeDesigner nodeDesigner;
            if ((nodeDesigner = mGraphDesigner.AddNode(mActiveBehaviorSource, type, mousePosition)) != null)
            {
                if (onAddTask != null)
                {
                    onAddTask(mActiveBehaviorSource, nodeDesigner.Task);
                }
                SaveBehavior();
                return nodeDesigner;
            }
            return null;
        }

        public bool IsReferencingTasks()
        {
            return mTaskInspector.ActiveReferenceTask != null;
        }

        public bool IsReferencingField(FieldInfo fieldInfo)
        {
            return fieldInfo.Equals(mTaskInspector.ActiveReferenceTaskFieldInfo);
        }

        private void DisableReferenceTasks()
        {
            if (IsReferencingTasks())
            {
                ToggleReferenceTasks();
            }
        }

        public void ToggleReferenceTasks()
        {
            ToggleReferenceTasks(null, null);
        }

        public void ToggleReferenceTasks(Task task, FieldInfo fieldInfo)
        {
            bool flag = !IsReferencingTasks();
            mTaskInspector.SetActiveReferencedTasks((!flag) ? null : task, (!flag) ? null : fieldInfo);
            UpdateGraphStatus();
        }

        private void ReferenceTask(NodeDesigner nodeDesigner)
        {
            if (nodeDesigner != null && mTaskInspector.ReferenceTasks(nodeDesigner.Task))
            {
                SaveBehavior();
            }
        }

        public void IdentifyNode(NodeDesigner nodeDesigner)
        {
            mGraphDesigner.IdentifyNode(nodeDesigner);
        }

        private void TakeScreenshot()
        {
            mScreenshotPath = EditorUtility.SaveFilePanel("Save Screenshot", "Assets", mActiveBehaviorSource.behaviorName + "Screenshot.png", "png");
            if (mScreenshotPath.Length != 0 && Application.dataPath.Length < mScreenshotPath.Length)
            {
                mTakingScreenshot = true;
                mScreenshotGraphSize = mGraphDesigner.GraphSize(mGraphOffset);
                mGraphDesigner.GraphDirty();
                if (mScreenshotGraphSize.width == 0f || mScreenshotGraphSize.height == 0f)
                {
                    mScreenshotGraphSize = new Rect(0f, 0f, 100f, 100f);
                }
                mScreenshotStartGraphZoom = mGraphZoom;
                mScreenshotStartGraphOffset = mGraphOffset;
                mGraphZoom = 1f;
                mGraphOffset.x -= mScreenshotGraphSize.xMin - 10f;
                mGraphOffset.y -= mScreenshotGraphSize.yMin - 10f;
                mScreenshotGraphOffset = mGraphOffset;
                mScreenshotGraphSize.Set(mScreenshotGraphSize.xMin - 9f, mScreenshotGraphSize.yMin, mScreenshotGraphSize.width + 18f, mScreenshotGraphSize.height + 18f);
                mScreenshotTexture = new Texture2D((int)mScreenshotGraphSize.width, (int)mScreenshotGraphSize.height, TextureFormat.RGB24, mipChain: false);
                Repaint();
            }
            else if (Path.GetExtension(mScreenshotPath).Equals(".png"))
            {
                Debug.LogError("Error: Unable to save screenshot. The save location must be within the Asset directory.");
            }
        }

        private void RenderScreenshotTile()
        {
            float num = Mathf.Min(mGraphRect.width, mScreenshotGraphSize.width - (mGraphOffset.x - mScreenshotGraphOffset.x));
            float num2 = Mathf.Min(mGraphRect.height, mScreenshotGraphSize.height + (mGraphOffset.y - mScreenshotGraphOffset.y));
            Rect source = new Rect(mGraphRect.x, 39f + mGraphRect.height - num2 - 7f, num, num2);
            mScreenshotTexture.ReadPixels(source, -(int)(mGraphOffset.x - mScreenshotGraphOffset.x), (int)(mScreenshotGraphSize.height - num2 + (mGraphOffset.y - mScreenshotGraphOffset.y)));
            mScreenshotTexture.Apply(updateMipmaps: false);
            if (mScreenshotGraphSize.xMin + num - (mGraphOffset.x - mScreenshotGraphOffset.x) < mScreenshotGraphSize.xMax)
            {
                mGraphOffset.x -= num - 1f;
                mGraphDesigner.GraphDirty();
                Repaint();
            }
            else if (mScreenshotGraphSize.yMin + num2 - (mGraphOffset.y - mScreenshotGraphOffset.y) < mScreenshotGraphSize.yMax)
            {
                mGraphOffset.y -= num2 - 1f;
                mGraphOffset.x = mScreenshotGraphOffset.x;
                mGraphDesigner.GraphDirty();
                Repaint();
            }
            else
            {
                SaveScreenshot();
            }
        }

        private void SaveScreenshot()
        {
            byte[] bytes = mScreenshotTexture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(mScreenshotTexture, allowDestroyingAssets: true);
            File.WriteAllBytes(mScreenshotPath, bytes);
            string path = $"Assets/{mScreenshotPath.Substring(Application.dataPath.Length + 1)}";
            AssetDatabase.ImportAsset(path);
            mTakingScreenshot = false;
            mGraphZoom = mScreenshotStartGraphZoom;
            mGraphOffset = mScreenshotStartGraphOffset;
            mGraphDesigner.GraphDirty();
            Repaint();
        }

        private void HandleEvents()
        {
            if (mTakingScreenshot)
            {
                return;
            }
            if (Event.current.type != EventType.MouseUp && CheckForAutoScroll())
            {
                Repaint();
            }
            else
            {
                if (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout)
                {
                    return;
                }
                switch (Event.current.type)
                {
                    case EventType.MouseMove:
                        if (MouseMove())
                        {
                            Event.current.Use();
                        }
                        break;
                    case EventType.MouseDown:
                        if (mShowQuickTaskList && !mQuickTaskListRect.Contains(mCurrentMousePosition))
                        {
                            mShowQuickTaskList = false;
                        }
                        if (Event.current.button == 0 && Event.current.modifiers != EventModifiers.Control)
                        {
                            if (GetMousePositionInGraph(out var mousePosition))
                            {
                                if (LeftMouseDown(Event.current.clickCount, mousePosition))
                                {
                                    Event.current.Use();
                                }
                            }
                            else if (GetMousePositionInPropertiesPane(out mousePosition) && mBehaviorToolbarSelection == 2 && mVariableInspector.LeftMouseDown(mActiveBehaviorSource, mActiveBehaviorSource, mousePosition))
                            {
                                Event.current.Use();
                                Repaint();
                            }
                        }
                        else if ((Event.current.button == 1 || (Event.current.modifiers == EventModifiers.Control && Event.current.button == 0)) && RightMouseDown())
                        {
                            Event.current.Use();
                        }
                        break;
                    case EventType.MouseDrag:
                        if (Event.current.button == 0)
                        {
                            if (LeftMouseDragged())
                            {
                                Event.current.Use();
                            }
                            else if (Event.current.modifiers == EventModifiers.Alt && MousePan())
                            {
                                Event.current.Use();
                            }
                        }
                        else if (Event.current.button == 2 && MousePan())
                        {
                            Event.current.Use();
                        }
                        break;
                    case EventType.MouseUp:
                        if (Event.current.button == 0 && Event.current.modifiers != EventModifiers.Control)
                        {
                            if (LeftMouseRelease())
                            {
                                Event.current.Use();
                            }
                        }
                        else if ((Event.current.button == 1 || (Event.current.modifiers == EventModifiers.Control && Event.current.button == 0)) && mShowRightClickMenu)
                        {
                            mShowRightClickMenu = false;
                            mRightClickMenu.ShowAsContext();
                            Event.current.Use();
                        }
                        break;
                    case EventType.ScrollWheel:
                        if (BehaviorDesignerPreferences.GetBool(BDPreferences.MouseWhellScrolls) && !mCommandDown)
                        {
                            MousePan();
                        }
                        else if (MouseZoom())
                        {
                            Event.current.Use();
                        }
                        break;
                    case EventType.KeyDown:
                        if (Event.current.keyCode == KeyCode.LeftMeta || Event.current.keyCode == KeyCode.RightMeta)
                        {
                            mCommandDown = true;
                        }
                        break;
                    case EventType.KeyUp:
                        if (Event.current.keyCode == KeyCode.Delete || Event.current.keyCode == KeyCode.Backspace || Event.current.commandName.Equals("Delete"))
                        {
                            if (!PropertiesInspectorHasFocus() && (!EditorApplication.isPlaying || (bool)(mActiveObject as ExternalBehavior)))
                            {
                                DeleteNodes();
                                Event.current.Use();
                            }
                        }
                        else if (Event.current.keyCode == (KeyCode)BehaviorDesignerPreferences.GetInt(BDPreferences.QuickSearchKeyCode) && Event.current.modifiers == EventModifiers.None)
                        {
                            if (!mShowQuickTaskList && GetMousePositionInGraph(out var mousePosition2))
                            {
                                mShowQuickTaskList = true;
                                mousePosition2 *= mGraphZoom;
                                mQuickTaskListRect = new Rect(mousePosition2 + new Vector2(200f, 0f) * 1.5f, new Vector2(200f, 200f));
                                if (mQuickTaskListRect.xMax > mGraphRect.xMax)
                                {
                                    mQuickTaskListRect.x -= mQuickTaskListRect.xMax - mGraphRect.xMax;
                                }
                                if (mQuickTaskListRect.yMax > mGraphRect.yMax)
                                {
                                    mQuickTaskListRect.y -= mQuickTaskListRect.yMax - mGraphRect.yMax;
                                }
                                if (mQuickTaskListRect.yMin < mGraphRect.yMin)
                                {
                                    mQuickTaskListRect.y += mGraphRect.yMin - mQuickTaskListRect.yMin;
                                }
                                mTaskList.FocusSearchField(quickTaskList: true, clearQuickSearchString: true);
                                Event.current.Use();
                                Repaint();
                            }
                        }
                        else if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                        {
                            if (mBehaviorToolbarSelection == 2 && mVariableInspector.HasFocus())
                            {
                                if (mVariableInspector.ClearFocus(addVariable: true, mActiveBehaviorSource))
                                {
                                    SaveBehavior();
                                }
                                Repaint();
                            }
                            else
                            {
                                DisableReferenceTasks();
                                if (mShowQuickTaskList)
                                {
                                    mTaskList.SelectQuickTask(this);
                                    Repaint();
                                }
                            }
                            Event.current.Use();
                        }
                        else if (Event.current.keyCode == KeyCode.Escape)
                        {
                            DisableReferenceTasks();
                            if (mShowQuickTaskList)
                            {
                                mShowQuickTaskList = false;
                                Event.current.Use();
                                Repaint();
                            }
                        }
                        else if (Event.current.keyCode == KeyCode.UpArrow || Event.current.keyCode == KeyCode.DownArrow)
                        {
                            if (mShowQuickTaskList)
                            {
                                mTaskList.MoveSelectedQuickTask(Event.current.keyCode == KeyCode.DownArrow);
                                Event.current.Use();
                                Repaint();
                            }
                        }
                        else if (Event.current.keyCode == KeyCode.A && Event.current.modifiers == EventModifiers.Control)
                        {
                            if (mShowQuickTaskList)
                            {
                                mTaskList.FocusSearchField(quickTaskList: true, clearQuickSearchString: false);
                            }
                            else if (mBehaviorToolbarSelection == 1 && GUIUtility.keyboardControl != 0)
                            {
                                mTaskList.FocusSearchField(quickTaskList: false, clearQuickSearchString: false);
                            }
                        }
                        else if (Event.current.keyCode == KeyCode.LeftMeta || Event.current.keyCode == KeyCode.RightMeta)
                        {
                            mCommandDown = false;
                        }
                        break;
                    case EventType.ValidateCommand:
                        if ((!EditorApplication.isPlaying || (bool)(mActiveObject as ExternalBehavior)) && (Event.current.commandName.Equals("Copy") || Event.current.commandName.Equals("Paste") || Event.current.commandName.Equals("Cut") || Event.current.commandName.Equals("SelectAll") || Event.current.commandName.Equals("Duplicate")) && !PropertiesInspectorHasFocus() && !ViewOnlyMode())
                        {
                            Event.current.Use();
                        }
                        break;
                    case EventType.ExecuteCommand:
                        if (!PropertiesInspectorHasFocus() && (!EditorApplication.isPlaying || (bool)(mActiveObject as ExternalBehavior)) && !ViewOnlyMode())
                        {
                            if (Event.current.commandName.Equals("Copy"))
                            {
                                CopyNodes();
                                Event.current.Use();
                            }
                            else if (Event.current.commandName.Equals("Paste"))
                            {
                                PasteNodes();
                                Event.current.Use();
                            }
                            else if (Event.current.commandName.Equals("Cut"))
                            {
                                CutNodes();
                                Event.current.Use();
                            }
                            else if (Event.current.commandName.Equals("SelectAll"))
                            {
                                mGraphDesigner.SelectAll();
                                Event.current.Use();
                            }
                            else if (Event.current.commandName.Equals("Duplicate"))
                            {
                                DuplicateNodes();
                                Event.current.Use();
                            }
                        }
                        break;
                    case EventType.Repaint:
                    case EventType.Layout:
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                    case EventType.Ignore:
                    case EventType.Used:
                        break;
                }
            }
        }

        private bool CheckForAutoScroll()
        {
            if (!GetMousePositionInGraph(out var _))
            {
                return false;
            }
            if (mGraphScrollRect.Contains(mCurrentMousePosition))
            {
                return false;
            }
            if (mIsDragging || mIsSelecting || mGraphDesigner.ActiveNodeConnection != null)
            {
                Vector2 zero = Vector2.zero;
                if (mCurrentMousePosition.y < mGraphScrollRect.yMin + 15f)
                {
                    zero.y = 3f;
                }
                else if (mCurrentMousePosition.y > mGraphScrollRect.yMax - 15f)
                {
                    zero.y = -3f;
                }
                if (mCurrentMousePosition.x < mGraphScrollRect.xMin + 15f)
                {
                    zero.x = 3f;
                }
                else if (mCurrentMousePosition.x > mGraphScrollRect.xMax - 15f)
                {
                    zero.x = -3f;
                }
                ScrollGraph(zero);
                if (mIsDragging)
                {
                    mGraphDesigner.DragSelectedNodes(-zero / mGraphZoom, Event.current.modifiers != EventModifiers.Alt);
                }
                if (mIsSelecting)
                {
                    mSelectStartPosition += zero / mGraphZoom;
                }
                return true;
            }
            return false;
        }

        private bool MouseMove()
        {
            if (!GetMousePositionInGraph(out var mousePosition))
            {
                return false;
            }
            NodeDesigner nodeDesigner = mGraphDesigner.NodeAt(mousePosition, mGraphOffset);
            if (mGraphDesigner.HoverNode != null && ((nodeDesigner != null && !mGraphDesigner.HoverNode.Equals(nodeDesigner)) || !mGraphDesigner.HoverNode.HoverBarAreaContains(mousePosition, mGraphOffset)))
            {
                mGraphDesigner.ClearHover();
                Repaint();
            }
            if ((bool)nodeDesigner && !nodeDesigner.IsEntryDisplay && !ViewOnlyMode())
            {
                mGraphDesigner.Hover(nodeDesigner);
            }
            return mGraphDesigner.HoverNode != null;
        }

        private bool LeftMouseDown(int clickCount, Vector2 mousePosition)
        {
            if (PropertiesInspectorHasFocus())
            {
                mTaskInspector.ClearFocus();
                mVariableInspector.ClearFocus(addVariable: false, null);
                Repaint();
            }
            NodeDesigner nodeDesigner = mGraphDesigner.NodeAt(mousePosition, mGraphOffset);
            if (Event.current.modifiers == EventModifiers.Alt)
            {
                mNodeClicked = mGraphDesigner.IsSelected(nodeDesigner);
                return false;
            }
            if (IsReferencingTasks())
            {
                if (nodeDesigner == null)
                {
                    DisableReferenceTasks();
                }
                else
                {
                    ReferenceTask(nodeDesigner);
                }
                return true;
            }
            if (nodeDesigner != null)
            {
                if (mGraphDesigner.HoverNode != null && !nodeDesigner.Equals(mGraphDesigner.HoverNode))
                {
                    mGraphDesigner.ClearHover();
                    mGraphDesigner.Hover(nodeDesigner);
                }
                NodeConnection nodeConnection = null;
                if (!ViewOnlyMode() && (nodeConnection = nodeDesigner.NodeConnectionRectContains(mousePosition, mGraphOffset)) != null)
                {
                    if (mGraphDesigner.NodeCanOriginateConnection(nodeDesigner, nodeConnection))
                    {
                        mGraphDesigner.ActiveNodeConnection = nodeConnection;
                    }
                    return true;
                }
                if (nodeDesigner.Contains(mousePosition, mGraphOffset, includeConnections: false))
                {
                    mKeepTasksSelected = false;
                    if (mGraphDesigner.IsSelected(nodeDesigner))
                    {
                        if (Event.current.modifiers == EventModifiers.Control)
                        {
                            mKeepTasksSelected = true;
                            mGraphDesigner.Deselect(nodeDesigner);
                        }
                        else if (Event.current.modifiers == EventModifiers.Shift && nodeDesigner.Task is ParentTask)
                        {
                            nodeDesigner.Task.NodeData.Collapsed = !nodeDesigner.Task.NodeData.Collapsed;
                            mGraphDesigner.DeselectWithParent(nodeDesigner);
                        }
                        else if (clickCount == 2)
                        {
                            if (mBehaviorToolbarSelection != 3 && BehaviorDesignerPreferences.GetBool(BDPreferences.OpenInspectorOnTaskDoubleClick))
                            {
                                mBehaviorToolbarSelection = 3;
                            }
                            else if (nodeDesigner.Task is BehaviorReference)
                            {
                                BehaviorReference behaviorReference = nodeDesigner.Task as BehaviorReference;
                                if (behaviorReference.GetExternalBehaviors() != null && behaviorReference.GetExternalBehaviors().Length > 0 && behaviorReference.GetExternalBehaviors()[0] != null)
                                {
                                    if (mLockActiveGameObject)
                                    {
                                        LoadBehavior(behaviorReference.GetExternalBehaviors()[0].GetBehaviorSource(), loadPrevBehavior: false);
                                    }
                                    else
                                    {
                                        Selection.activeObject = behaviorReference.GetExternalBehaviors()[0];
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (Event.current.modifiers != EventModifiers.Shift && Event.current.modifiers != EventModifiers.Control)
                        {
                            mGraphDesigner.ClearNodeSelection();
                            mGraphDesigner.ClearConnectionSelection();
                            if (BehaviorDesignerPreferences.GetBool(BDPreferences.OpenInspectorOnTaskSelection))
                            {
                                mBehaviorToolbarSelection = 3;
                            }
                        }
                        else
                        {
                            mKeepTasksSelected = true;
                        }
                        mGraphDesigner.Select(nodeDesigner);
                    }
                    mNodeClicked = mGraphDesigner.IsSelected(nodeDesigner);
                    return true;
                }
            }
            if (mGraphDesigner.HoverNode != null)
            {
                bool collapsedButtonClicked = false;
                if (mGraphDesigner.HoverNode.HoverBarButtonClick(mousePosition, mGraphOffset, ref collapsedButtonClicked))
                {
                    SaveBehavior();
                    if (collapsedButtonClicked && mGraphDesigner.HoverNode.Task.NodeData.Collapsed)
                    {
                        mGraphDesigner.DeselectWithParent(mGraphDesigner.HoverNode);
                    }
                    return true;
                }
            }
            List<NodeConnection> nodeConnections = new List<NodeConnection>();
            mGraphDesigner.NodeConnectionsAt(mousePosition, mGraphOffset, ref nodeConnections);
            if (nodeConnections.Count > 0)
            {
                if (Event.current.modifiers != EventModifiers.Shift && Event.current.modifiers != EventModifiers.Control)
                {
                    mGraphDesigner.ClearNodeSelection();
                    mGraphDesigner.ClearConnectionSelection();
                }
                for (int i = 0; i < nodeConnections.Count; i++)
                {
                    if (mGraphDesigner.IsSelected(nodeConnections[i]))
                    {
                        if (Event.current.modifiers == EventModifiers.Control)
                        {
                            mGraphDesigner.Deselect(nodeConnections[i]);
                        }
                    }
                    else
                    {
                        mGraphDesigner.Select(nodeConnections[i]);
                    }
                }
                return true;
            }
            if (Event.current.modifiers != EventModifiers.Shift)
            {
                mGraphDesigner.ClearNodeSelection();
                mGraphDesigner.ClearConnectionSelection();
            }
            mSelectStartPosition = mousePosition;
            mIsSelecting = true;
            mIsDragging = false;
            mDragDelta = Vector2.zero;
            mNodeClicked = false;
            return true;
        }

        private bool LeftMouseDragged()
        {
            if (!GetMousePositionInGraph(out var _))
            {
                return false;
            }
            if (Event.current.modifiers != EventModifiers.Alt)
            {
                if (IsReferencingTasks())
                {
                    return true;
                }
                if (mIsSelecting)
                {
                    mGraphDesigner.DeselectAll(null);
                    List<NodeDesigner> list = mGraphDesigner.NodesAt(GetSelectionArea(), mGraphOffset);
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            mGraphDesigner.Select(list[i]);
                        }
                    }
                    return true;
                }
                if (mGraphDesigner.ActiveNodeConnection != null)
                {
                    return true;
                }
            }
            if (mNodeClicked && !ViewOnlyMode())
            {
                Vector2 vector = Vector2.zero;
                if (BehaviorDesignerPreferences.GetBool(BDPreferences.SnapToGrid))
                {
                    mDragDelta += Event.current.delta;
                    if (Mathf.Abs(mDragDelta.x) > 10f)
                    {
                        float num = Mathf.Abs(mDragDelta.x) % 10f;
                        vector.x = (Mathf.Abs(mDragDelta.x) - num) * Mathf.Sign(mDragDelta.x);
                        mDragDelta.x = num * Mathf.Sign(mDragDelta.x);
                    }
                    if (Mathf.Abs(mDragDelta.y) > 10f)
                    {
                        float num2 = Mathf.Abs(mDragDelta.y) % 10f;
                        vector.y = (Mathf.Abs(mDragDelta.y) - num2) * Mathf.Sign(mDragDelta.y);
                        mDragDelta.y = num2 * Mathf.Sign(mDragDelta.y);
                    }
                }
                else
                {
                    vector = Event.current.delta;
                }
                bool flag = mGraphDesigner.DragSelectedNodes(vector / mGraphZoom, Event.current.modifiers != EventModifiers.Alt);
                if (flag)
                {
                    mKeepTasksSelected = true;
                }
                mIsDragging = true;
                return flag;
            }
            return false;
        }

        private bool LeftMouseRelease()
        {
            mNodeClicked = false;
            if (IsReferencingTasks())
            {
                if (!mTaskInspector.IsActiveTaskArray() && !mTaskInspector.IsActiveTaskNull())
                {
                    DisableReferenceTasks();
                    Repaint();
                }
                if (!GetMousePositionInGraph(out var _))
                {
                    mGraphDesigner.ActiveNodeConnection = null;
                    return false;
                }
                return true;
            }
            if (mIsSelecting)
            {
                mIsSelecting = false;
                return true;
            }
            if (mIsDragging)
            {
                BehaviorUndo.RegisterUndo("Drag", mActiveBehaviorSource.Owner.GetObject());
                SaveBehavior();
                mIsDragging = false;
                mDragDelta = Vector3.zero;
                return true;
            }
            if (mGraphDesigner.ActiveNodeConnection != null)
            {
                if (!GetMousePositionInGraph(out var mousePosition2))
                {
                    mGraphDesigner.ActiveNodeConnection = null;
                    return false;
                }
                NodeDesigner nodeDesigner = mGraphDesigner.NodeAt(mousePosition2, mGraphOffset);
                if (nodeDesigner != null && !nodeDesigner.Equals(mGraphDesigner.ActiveNodeConnection.OriginatingNodeDesigner) && mGraphDesigner.NodeCanAcceptConnection(nodeDesigner, mGraphDesigner.ActiveNodeConnection))
                {
                    mGraphDesigner.ConnectNodes(mActiveBehaviorSource, nodeDesigner);
                    BehaviorUndo.RegisterUndo("Task Connection", mActiveBehaviorSource.Owner.GetObject());
                    SaveBehavior();
                }
                else
                {
                    mGraphDesigner.ActiveNodeConnection = null;
                }
                return true;
            }
            if (Event.current.modifiers != EventModifiers.Shift && !mKeepTasksSelected)
            {
                if (!GetMousePositionInGraph(out var mousePosition3))
                {
                    return false;
                }
                NodeDesigner nodeDesigner2 = mGraphDesigner.NodeAt(mousePosition3, mGraphOffset);
                if (nodeDesigner2 != null && !mGraphDesigner.IsSelected(nodeDesigner2))
                {
                    mGraphDesigner.DeselectAll(nodeDesigner2);
                }
                return true;
            }
            return false;
        }

        private bool RightMouseDown()
        {
            if (IsReferencingTasks())
            {
                DisableReferenceTasks();
                return false;
            }
            if (!GetMousePositionInGraph(out var mousePosition))
            {
                return false;
            }
            NodeDesigner nodeDesigner = mGraphDesigner.NodeAt(mousePosition, mGraphOffset);
            if (nodeDesigner == null || !mGraphDesigner.IsSelected(nodeDesigner))
            {
                mGraphDesigner.ClearNodeSelection();
                mGraphDesigner.ClearConnectionSelection();
                if (nodeDesigner != null)
                {
                    mGraphDesigner.Select(nodeDesigner);
                }
            }
            if (mGraphDesigner.HoverNode != null)
            {
                mGraphDesigner.ClearHover();
            }
            BuildRightClickMenu(nodeDesigner);
            return true;
        }

        private bool MouseZoom()
        {
            if (!GetMousePositionInGraph(out var mousePosition))
            {
                return false;
            }
            float num = (0f - Event.current.delta.y * mGraphZoomMultiplier) / 150f;
            mGraphZoom += num;
            mGraphZoom = Mathf.Clamp(mGraphZoom, 0.2f, 1f);
            GetMousePositionInGraph(out var mousePosition2);
            mGraphOffset += mousePosition2 - mousePosition;
            mGraphScrollPosition += mousePosition2 - mousePosition;
            mGraphDesigner.GraphDirty();
            return true;
        }

        private bool MousePan()
        {
            if (!GetMousePositionInGraph(out var _))
            {
                return false;
            }
            Vector2 delta = Event.current.delta;
            if (Event.current.type == EventType.ScrollWheel)
            {
                delta *= -1.5f;
                if (Event.current.modifiers == EventModifiers.Control)
                {
                    delta.x = delta.y;
                    delta.y = 0f;
                }
            }
            ScrollGraph(delta);
            return true;
        }

        private void ScrollGraph(Vector2 amount)
        {
            mGraphOffset += amount / mGraphZoom;
            mGraphScrollPosition -= amount;
            mGraphDesigner.GraphDirty();
            Repaint();
        }

        private bool PropertiesInspectorHasFocus()
        {
            return mTaskInspector.HasFocus() || mVariableInspector.HasFocus();
        }

        private void AddTaskCallback(object obj)
        {
            AddTask((Type)obj, useMousePosition: true);
        }

        private void ReplaceTasksCallback(object obj)
        {
            if (mGraphDesigner.ReplaceSelectedNodes(mActiveBehaviorSource, (Type)obj))
            {
                SaveBehavior();
            }
        }

        private void BehaviorSelectionCallback(object obj)
        {
            BehaviorSource behaviorSource = obj as BehaviorSource;
            if (behaviorSource.Owner is Behavior)
            {
                mActiveObject = (behaviorSource.Owner as Behavior).gameObject;
            }
            else
            {
                mActiveObject = behaviorSource.Owner as ExternalBehavior;
            }
            if (!mLockActiveGameObject)
            {
                Selection.activeObject = mActiveObject;
            }
            LoadBehavior(behaviorSource, loadPrevBehavior: false);
            UpdateGraphStatus();
            if (EditorApplication.isPaused)
            {
                mUpdateNodeTaskMap = true;
                UpdateNodeTaskMap();
            }
        }

        private void ToggleEnableState(object obj)
        {
            NodeDesigner nodeDesigner = obj as NodeDesigner;
            nodeDesigner.ToggleEnableState();
            SaveBehavior();
            Repaint();
        }

        private void ToggleCollapseState(object obj)
        {
            NodeDesigner nodeDesigner = obj as NodeDesigner;
            if (nodeDesigner.ToggleCollapseState())
            {
                mGraphDesigner.DeselectWithParent(nodeDesigner);
            }
            SaveBehavior();
            Repaint();
        }

        private void ToggleBreakpoint(object obj)
        {
            NodeDesigner nodeDesigner = obj as NodeDesigner;
            nodeDesigner.ToggleBreakpoint();
            SaveBehavior();
            Repaint();
        }

        private void OpenInFileEditor(object obj)
        {
            NodeDesigner nodeDesigner = obj as NodeDesigner;
            TaskInspector.OpenInFileEditor(nodeDesigner.Task);
        }

        private void SelectInProject(object obj)
        {
            NodeDesigner nodeDesigner = obj as NodeDesigner;
            TaskInspector.SelectInProject(nodeDesigner.Task);
        }

        private void CopyNodes()
        {
            mCopiedTasks = mGraphDesigner.Copy(mGraphOffset, mGraphZoom);
        }

        private void PasteNodes()
        {
            if (!(mActiveObject == null) && (!EditorApplication.isPlaying || (bool)(mActiveObject as ExternalBehavior)))
            {
                GameObject gameObject = mActiveObject as GameObject;
                if (gameObject != null && gameObject.GetComponent<Behavior>() == null)
                {
                    AddBehavior();
                }
                if (mCopiedTasks != null && mCopiedTasks.Count > 0)
                {
                    BehaviorUndo.RegisterUndo("Paste", mActiveBehaviorSource.Owner.GetObject());
                }
                mGraphDesigner.Paste(mActiveBehaviorSource, new Vector2(mGraphRect.width / (2f * mGraphZoom) - mGraphOffset.x, 150f - mGraphOffset.y), mCopiedTasks, mGraphOffset, mGraphZoom);
                SaveBehavior();
            }
        }

        private void CutNodes()
        {
            mCopiedTasks = mGraphDesigner.Copy(mGraphOffset, mGraphZoom);
            if (mCopiedTasks != null && mCopiedTasks.Count > 0)
            {
                BehaviorUndo.RegisterUndo("Cut", mActiveBehaviorSource.Owner.GetObject());
            }
            mGraphDesigner.Delete(mActiveBehaviorSource, null);
            SaveBehavior();
        }

        private void DuplicateNodes()
        {
            List<TaskSerializer> list = mGraphDesigner.Copy(mGraphOffset, mGraphZoom);
            if (list != null && list.Count > 0)
            {
                BehaviorUndo.RegisterUndo("Duplicate", mActiveBehaviorSource.Owner.GetObject());
            }
            mGraphDesigner.Paste(mActiveBehaviorSource, new Vector2(mGraphRect.width / (2f * mGraphZoom) - mGraphOffset.x, 150f - mGraphOffset.y), list, mGraphOffset, mGraphZoom);
            SaveBehavior();
        }

        private void DeleteNodes()
        {
            if (!ViewOnlyMode())
            {
                mGraphDesigner.Delete(mActiveBehaviorSource, onRemoveTask);
                SaveBehavior();
            }
        }

        public void RemoveSharedVariableReferences(SharedVariable sharedVariable)
        {
            if (mGraphDesigner.RemoveSharedVariableReferences(sharedVariable))
            {
                SaveBehavior();
                Repaint();
            }
        }

        private void OnUndoRedo()
        {
            if (mActiveBehaviorSource != null)
            {
                LoadBehavior(mActiveBehaviorSource, loadPrevBehavior: true, inspectorLoad: false);
            }
        }

        private void SetupSizes()
        {
            float width = base.position.width;
            float num = base.position.height + 22f;
            if (mPrevScreenWidth != width || mPrevScreenHeight != num || mPropertiesPanelOnLeft != BehaviorDesignerPreferences.GetBool(BDPreferences.PropertiesPanelOnLeft))
            {
                if (BehaviorDesignerPreferences.GetBool(BDPreferences.PropertiesPanelOnLeft))
                {
                    mFileToolBarRect = new Rect(300f, 0f, width - 300f, 18f);
                    mPropertyToolbarRect = new Rect(0f, 0f, 300f, 18f);
                    mPropertyBoxRect = new Rect(0f, mPropertyToolbarRect.height, 300f, num - mPropertyToolbarRect.height - 21f);
                    mGraphRect = new Rect(300f, 18f, width - 300f - 15f, num - 36f - 21f - 15f);
                    mFindDialogueRect = new Rect(300f + mGraphRect.width - 300f, 18 + (EditorGUIUtility.isProSkin ? 1 : 2), 300f, 88f);
                    mPreferencesPaneRect = new Rect(300f + mGraphRect.width - 290f, 18 + (EditorGUIUtility.isProSkin ? 1 : 2), 290f, 414f);
                }
                else
                {
                    mFileToolBarRect = new Rect(0f, 0f, width - 300f, 18f);
                    mPropertyToolbarRect = new Rect(width - 300f, 0f, 300f, 18f);
                    mPropertyBoxRect = new Rect(width - 300f, mPropertyToolbarRect.height, 300f, num - mPropertyToolbarRect.height - 21f);
                    mGraphRect = new Rect(0f, 18f, width - 300f - 15f, num - 36f - 21f - 15f);
                    mFindDialogueRect = new Rect(mGraphRect.width - 300f, 18 + (EditorGUIUtility.isProSkin ? 1 : 2), 300f, 88f);
                    mPreferencesPaneRect = new Rect(mGraphRect.width - 290f, 18 + (EditorGUIUtility.isProSkin ? 1 : 2), 290f, 414f);
                }
                mDebugToolBarRect = new Rect(mGraphRect.x, num - 18f - 21f, mGraphRect.width + 15f, 18f);
                mGraphScrollRect.Set(mGraphRect.xMin + 15f, mGraphRect.yMin + 15f, mGraphRect.width - 30f, mGraphRect.height - 30f);
                if (mGraphScrollPosition == new Vector2(-1f, -1f))
                {
                    mGraphScrollPosition = (mGraphScrollSize - new Vector2(mGraphRect.width, mGraphRect.height)) / 2f - 2f * new Vector2(15f, 15f);
                }
                mPrevScreenWidth = width;
                mPrevScreenHeight = num;
                mPropertiesPanelOnLeft = BehaviorDesignerPreferences.GetBool(BDPreferences.PropertiesPanelOnLeft);
            }
        }

        private bool GetMousePositionInGraph(out Vector2 mousePosition)
        {
            mousePosition = mCurrentMousePosition;
            if (!mGraphRect.Contains(mousePosition))
            {
                return false;
            }
            if (mShowPrefPane && mPreferencesPaneRect.Contains(mousePosition))
            {
                return false;
            }
            if (mShowFindDialogue && mFindDialogueRect.Contains(mousePosition))
            {
                return false;
            }
            mousePosition -= new Vector2(mGraphRect.xMin, mGraphRect.yMin);
            mousePosition /= mGraphZoom;
            return true;
        }

        private bool GetMousePositionInPropertiesPane(out Vector2 mousePosition)
        {
            mousePosition = mCurrentMousePosition;
            if (!mPropertyBoxRect.Contains(mousePosition))
            {
                return false;
            }
            mousePosition.x -= mPropertyBoxRect.xMin;
            mousePosition.y -= mPropertyBoxRect.yMin;
            return true;
        }

        private Rect GetSelectionArea()
        {
            if (GetMousePositionInGraph(out var mousePosition))
            {
                float num = ((!(mSelectStartPosition.x < mousePosition.x)) ? mousePosition.x : mSelectStartPosition.x);
                float num2 = ((!(mSelectStartPosition.x > mousePosition.x)) ? mousePosition.x : mSelectStartPosition.x);
                float num3 = ((!(mSelectStartPosition.y < mousePosition.y)) ? mousePosition.y : mSelectStartPosition.y);
                float num4 = ((!(mSelectStartPosition.y > mousePosition.y)) ? mousePosition.y : mSelectStartPosition.y);
                mSelectionArea = new Rect(num, num3, num2 - num, num4 - num3);
            }
            return mSelectionArea;
        }

        public bool ViewOnlyMode()
        {
            if (Application.isPlaying)
            {
                return false;
            }
            if (mActiveBehaviorSource == null || mActiveBehaviorSource.Owner == null || mActiveBehaviorSource.Owner.Equals(null))
            {
                return false;
            }
            Behavior behavior = mActiveBehaviorSource.Owner.GetObject() as Behavior;
            if (behavior != null && !BehaviorDesignerPreferences.GetBool(BDPreferences.EditablePrefabInstances) && (PrefabUtility.GetPrefabAssetType(mActiveBehaviorSource.Owner.GetObject()) == PrefabAssetType.Regular || PrefabUtility.GetPrefabAssetType(mActiveBehaviorSource.Owner.GetObject()) == PrefabAssetType.Variant))
            {
                return true;
            }
            return false;
        }

        private BehaviorSource BehaviorSourceFromIBehaviorHistory(IBehavior behavior)
        {
            if (behavior == null)
            {
                return null;
            }
            if (behavior.GetObject() is GameObject)
            {
                Behavior[] components = (behavior.GetObject() as GameObject).GetComponents<Behavior>();
                for (int i = 0; i < components.Count(); i++)
                {
                    if (components[i].GetBehaviorSource().BehaviorID == behavior.GetBehaviorSource().BehaviorID)
                    {
                        return components[i].GetBehaviorSource();
                    }
                }
                return null;
            }
            return behavior.GetBehaviorSource();
        }

        public void SaveBehavior()
        {
            if (mActiveBehaviorSource != null && !ViewOnlyMode() && (!EditorApplication.isPlaying || (bool)(mActiveObject as ExternalBehavior)))
            {
                mGraphDesigner.Save(mActiveBehaviorSource);
                CheckForErrors();
            }
        }

        private void CheckForErrors()
        {
            if (mErrorDetails != null)
            {
                for (int i = 0; i < mErrorDetails.Count; i++)
                {
                    if (mErrorDetails[i].NodeDesigner != null)
                    {
                        mErrorDetails[i].NodeDesigner.HasError = false;
                    }
                }
            }
            if (BehaviorDesignerPreferences.GetBool(BDPreferences.ErrorChecking))
            {
                BehaviorSource behaviorSource = ((mExternalParent == null) ? mActiveBehaviorSource : mExternalParent);
                mErrorDetails = ErrorCheck.CheckForErrors(behaviorSource);
                if (mErrorDetails != null)
                {
                    for (int j = 0; j < mErrorDetails.Count; j++)
                    {
                        if (!(mErrorDetails[j].NodeDesigner == null))
                        {
                            mErrorDetails[j].NodeDesigner.HasError = true;
                        }
                    }
                }
            }
            else
            {
                mErrorDetails = null;
            }
            if (ErrorWindow.instance != null)
            {
                ErrorWindow.instance.ErrorDetails = mErrorDetails;
                ErrorWindow.instance.Repaint();
            }
        }

        public bool ContainsError(Task task, string fieldName)
        {
            if (mErrorDetails == null)
            {
                return false;
            }
            for (int i = 0; i < mErrorDetails.Count; i++)
            {
                if (task == null)
                {
                    if (!(mErrorDetails[i].NodeDesigner != null) && mErrorDetails[i].FieldName == fieldName)
                    {
                        return true;
                    }
                }
                else if (!(mErrorDetails[i].NodeDesigner == null) && mErrorDetails[i].NodeDesigner.Task == task && mErrorDetails[i].FieldName == fieldName)
                {
                    return true;
                }
            }
            return false;
        }

        private bool UpdateCheck()
        {
            if (mUpdateCheckRequest != null && mUpdateCheckRequest.isDone)
            {
                if (!string.IsNullOrEmpty(mUpdateCheckRequest.error))
                {
                    mUpdateCheckRequest = null;
                    return false;
                }
                if (!"1.7.4".ToString().Equals(mUpdateCheckRequest.downloadHandler.text))
                {
                    LatestVersion = mUpdateCheckRequest.downloadHandler.text;
                }
                mUpdateCheckRequest = null;
            }
            if (BehaviorDesignerPreferences.GetBool(BDPreferences.UpdateCheck) && DateTime.Compare(LastUpdateCheck.AddDays(1.0), DateTime.UtcNow) < 0)
            {
                string uri = string.Format("https://opsive.com/asset/UpdateCheck.php?asset=BehaviorDesigner&version={0}&unityversion={1}&devplatform={2}&targetplatform={3}", "1.7.4", Application.unityVersion, Application.platform, EditorUserBuildSettings.activeBuildTarget);
                mUpdateCheckRequest = UnityWebRequest.Get(uri);
                mUpdateCheckRequest.SendWebRequest();
                LastUpdateCheck = DateTime.UtcNow;
            }
            return mUpdateCheckRequest != null;
        }

        private void SaveAsAsset()
        {
            if (mActiveBehaviorSource == null)
            {
                return;
            }
            string text = EditorUtility.SaveFilePanel("Save Behavior Tree", "Assets", mActiveBehaviorSource.behaviorName + ".asset", "asset");
            if (text.Length != 0 && Application.dataPath.Length < text.Length)
            {
                Type typeWithinAssembly = TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.ExternalBehaviorTree");
                if (BehaviorDesignerPreferences.GetBool(BDPreferences.BinarySerialization))
                {
                    BinarySerialization.Save(mActiveBehaviorSource);
                }
                else
                {
                    JSONSerialization.Save(mActiveBehaviorSource);
                }
                ExternalBehavior externalBehavior = ScriptableObject.CreateInstance(typeWithinAssembly) as ExternalBehavior;
                BehaviorSource behaviorSource = new BehaviorSource(externalBehavior);
                behaviorSource.behaviorName = mActiveBehaviorSource.behaviorName;
                behaviorSource.behaviorDescription = mActiveBehaviorSource.behaviorDescription;
                behaviorSource.TaskData = mActiveBehaviorSource.TaskData;
                externalBehavior.SetBehaviorSource(behaviorSource);
                text = $"Assets/{text.Substring(Application.dataPath.Length + 1)}";
                AssetDatabase.DeleteAsset(text);
                AssetDatabase.CreateAsset(externalBehavior, text);
                AssetDatabase.ImportAsset(text);
                Selection.activeObject = externalBehavior;
            }
            else if (Path.GetExtension(text).Equals(".asset"))
            {
                Debug.LogError("Error: Unable to save external behavior tree. The save location must be within the Asset directory.");
            }
        }

        private void SaveAsPrefab()
        {
            if (mActiveBehaviorSource == null)
            {
                return;
            }
            string text = EditorUtility.SaveFilePanel("Save Behavior Tree", "Assets", mActiveBehaviorSource.behaviorName + ".prefab", "prefab");
            if (text.Length != 0 && Application.dataPath.Length < text.Length)
            {
                GameObject gameObject = new GameObject();
                Type type = Type.GetType("BehaviorDesigner.Runtime.BehaviorTree, Assembly-CSharp");
                if (type == null)
                {
                    type = Type.GetType("BehaviorDesigner.Runtime.BehaviorTree, Assembly-CSharp-firstpass");
                }
                Behavior behavior = gameObject.AddComponent(type) as Behavior;
                BehaviorSource behaviorSource = new BehaviorSource(behavior);
                behaviorSource.behaviorName = mActiveBehaviorSource.behaviorName;
                behaviorSource.behaviorDescription = mActiveBehaviorSource.behaviorDescription;
                behaviorSource.TaskData = mActiveBehaviorSource.TaskData;
                behavior.SetBehaviorSource(behaviorSource);
                text = $"Assets/{text.Substring(Application.dataPath.Length + 1)}";
                AssetDatabase.DeleteAsset(text);
                GameObject activeObject = PrefabUtility.SaveAsPrefabAsset(gameObject, text);
                UnityEngine.Object.DestroyImmediate(gameObject, allowDestroyingAssets: true);
                AssetDatabase.ImportAsset(text);
                Selection.activeObject = activeObject;
            }
            else if (Path.GetExtension(text).Equals(".prefab"))
            {
                Debug.LogError("Error: Unable to save prefab. The save location must be within the Asset directory.");
            }
        }

        public void LoadBehavior(BehaviorSource behaviorSource, bool loadPrevBehavior)
        {
            LoadBehavior(behaviorSource, loadPrevBehavior, inspectorLoad: false);
        }

        public void LoadBehavior(BehaviorSource behaviorSource, bool loadPrevBehavior, bool inspectorLoad)
        {
            if (behaviorSource == null || object.ReferenceEquals(behaviorSource.Owner, null) || behaviorSource.Owner.Equals(null))
            {
                return;
            }
            if (inspectorLoad && !mSizesInitialized)
            {
                mActiveBehaviorID = behaviorSource.Owner.GetInstanceID();
                mPrevActiveObject = Selection.activeObject;
                mLoadedFromInspector = true;
            }
            else
            {
                if (!mSizesInitialized)
                {
                    return;
                }
                if (!loadPrevBehavior)
                {
                    DisableReferenceTasks();
                    mVariableInspector.ResetSelectedVariableIndex();
                }
                mExternalParent = null;
                mActiveBehaviorSource = behaviorSource;
                if (behaviorSource.Owner is Behavior)
                {
                    mActiveObject = (behaviorSource.Owner as Behavior).gameObject;
                    ExternalBehavior externalBehavior = (behaviorSource.Owner as Behavior).ExternalBehavior;
                    if (externalBehavior != null && !EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        mActiveBehaviorSource = externalBehavior.BehaviorSource;
                        mActiveBehaviorSource.Owner = externalBehavior;
                        mExternalParent = behaviorSource;
                        behaviorSource.CheckForSerialization(force: true);
                        mActiveBehaviorSource.CheckForSerialization(force: true);
                        if (VariableInspector.SyncVariables(behaviorSource, mActiveBehaviorSource.GetAllVariables()))
                        {
                            if (BehaviorDesignerPreferences.GetBool(BDPreferences.BinarySerialization))
                            {
                                BinarySerialization.Save(behaviorSource);
                            }
                            else
                            {
                                JSONSerialization.Save(behaviorSource);
                            }
                        }
                    }
                }
                else
                {
                    mActiveObject = behaviorSource.Owner.GetObject();
                }
                mActiveBehaviorSource.BehaviorID = mActiveBehaviorSource.Owner.GetInstanceID();
                mActiveBehaviorID = mActiveBehaviorSource.BehaviorID;
                mPrevActiveObject = Selection.activeObject;
                if (mBehaviorSourceHistory.Count == 0 || mBehaviorSourceHistoryIndex >= mBehaviorSourceHistory.Count || mBehaviorSourceHistory[mBehaviorSourceHistoryIndex] == null || ((mBehaviorSourceHistory[mBehaviorSourceHistoryIndex] as IBehavior).GetBehaviorSource() != null && !mActiveBehaviorSource.BehaviorID.Equals((mBehaviorSourceHistory[mBehaviorSourceHistoryIndex] as IBehavior).GetBehaviorSource().BehaviorID)))
                {
                    for (int num = mBehaviorSourceHistory.Count - 1; num > mBehaviorSourceHistoryIndex; num--)
                    {
                        mBehaviorSourceHistory.RemoveAt(num);
                    }
                    mBehaviorSourceHistory.Add(mActiveBehaviorSource.Owner.GetObject());
                    mBehaviorSourceHistoryIndex++;
                }
                Vector2 nodePosition = new Vector2(mGraphRect.width / (2f * mGraphZoom), 150f);
                nodePosition -= mGraphOffset;
                if (mGraphDesigner.Load(mActiveBehaviorSource, loadPrevBehavior && !mLoadedFromInspector, nodePosition) && mGraphDesigner.HasEntryNode() && (!loadPrevBehavior || mLoadedFromInspector))
                {
                    mGraphOffset = new Vector2(mGraphRect.width / (2f * mGraphZoom), 50f) - mGraphDesigner.EntryNodeOffset();
                    mGraphScrollPosition = (mGraphScrollSize - new Vector2(mGraphRect.width, mGraphRect.height)) / 2f - 2f * new Vector2(15f, 15f);
                }
                mLoadedFromInspector = false;
                if (!mLockActiveGameObject)
                {
                    Selection.activeObject = mActiveObject;
                }
                if (EditorApplication.isPlaying && mActiveBehaviorSource != null)
                {
                    mRightClickMenu = null;
                    mUpdateNodeTaskMap = true;
                    UpdateNodeTaskMap();
                }
                CheckForErrors();
                UpdateGraphStatus();
                ClearBreadcrumbMenu();
                Find();
                Repaint();
            }
        }

        public void ClearGraph()
        {
            mGraphDesigner.Clear(saveSelectedNodes: true);
            mActiveBehaviorSource = null;
            CheckForErrors();
            UpdateGraphStatus();
            Repaint();
        }
    }
}