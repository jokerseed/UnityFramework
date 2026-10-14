using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BehaviorDesigner.Editor
{
    public static class BehaviorDesignerUtility
    {
        public const string Version = "1.7.4";

        public const int ToolBarHeight = 18;

        public const int PropertyBoxWidth = 300;

        public const int ScrollBarSize = 15;

        public const int EditorWindowTabHeight = 21;

        public const int PreferencesPaneWidth = 290;

        public const int PreferencesPaneHeight = 414;

        public const int FindDialogueWidth = 300;

        public const int FindDialogueHeight = 88;

        public const int QuickTaskListWidth = 200;

        public const int QuickTaskListHeight = 200;

        public const float GraphZoomMax = 1f;

        public const float GraphZoomMin = 0.2f;

        public const float GraphZoomSensitivity = 150f;

        public const float GraphAutoScrollEdgeDistance = 15f;

        public const float GraphAutoScrollEdgeSpeed = 3f;

        public const int LineSelectionThreshold = 7;

        public const int TaskBackgroundShadowSize = 3;

        public const int TitleHeight = 20;

        public const int TitleCompactHeight = 28;

        public const int IconAreaHeight = 52;

        public const int IconSize = 44;

        public const int IconBorderSize = 46;

        public const int CompactAreaHeight = 22;

        public const int ConnectionWidth = 42;

        public const int TopConnectionHeight = 14;

        public const int BottomConnectionHeight = 16;

        public const int TaskConnectionCollapsedWidth = 26;

        public const int TaskConnectionCollapsedHeight = 6;

        public const int MinWidth = 100;

        public const int MaxWidth = 220;

        public const int MaxCommentHeight = 100;

        public const int TextPadding = 20;

        public const float NodeFadeDuration = 0.5f;

        public const int IdentifyUpdateFadeTime = 500;

        public const int MaxIdentifyUpdateCount = 2000;

        public const float InterruptTaskHighlightDuration = 0.75f;

        public const int TaskPropertiesLabelWidth = 150;

        public const int MaxTaskDescriptionBoxWidth = 400;

        public const int MaxTaskDescriptionBoxHeight = 300;

        public const int MinorGridTickSpacing = 10;

        public const int MajorGridTickSpacing = 50;

        public const float UpdateCheckInterval = 1f;

        private static GUIStyle graphStatusGUIStyle = null;

        private static GUIStyle taskFoldoutGUIStyle = null;

        private static GUIStyle taskTitleGUIStyle = null;

        private static GUIStyle[] taskGUIStyle = new GUIStyle[9];

        private static GUIStyle[] taskCompactGUIStyle = new GUIStyle[9];

        private static GUIStyle[] taskSelectedGUIStyle = new GUIStyle[9];

        private static GUIStyle[] taskSelectedCompactGUIStyle = new GUIStyle[9];

        private static GUIStyle taskRunningGUIStyle = null;

        private static GUIStyle taskRunningCompactGUIStyle = null;

        private static GUIStyle taskRunningSelectedGUIStyle = null;

        private static GUIStyle taskRunningSelectedCompactGUIStyle = null;

        private static GUIStyle taskIdentifyGUIStyle = null;

        private static GUIStyle taskIdentifyCompactGUIStyle = null;

        private static GUIStyle taskIdentifySelectedGUIStyle = null;

        private static GUIStyle taskIdentifySelectedCompactGUIStyle = null;

        private static GUIStyle taskHighlightGUIStyle = null;

        private static GUIStyle taskHighlightCompactGUIStyle = null;

        private static GUIStyle taskCommentGUIStyle = null;

        private static GUIStyle taskCommentLeftAlignGUIStyle = null;

        private static GUIStyle taskCommentRightAlignGUIStyle = null;

        private static GUIStyle taskDescriptionGUIStyle = null;

        private static GUIStyle graphBackgroundGUIStyle = null;

        private static GUIStyle selectionGUIStyle = null;

        private static GUIStyle sharedVariableToolbarPopup = null;

        private static GUIStyle labelWrapGUIStyle = null;

        private static GUIStyle labelTitleGUIStyle = null;

        private static GUIStyle boldLabelGUIStyle = null;

        private static GUIStyle toolbarButtonLeftAlignGUIStyle = null;

        private static GUIStyle toolbarLabelGUIStyle = null;

        private static GUIStyle taskInspectorCommentGUIStyle = null;

        private static GUIStyle taskInspectorGUIStyle = null;

        private static GUIStyle toolbarButtonSelectionGUIStyle = null;

        private static GUIStyle propertyBoxGUIStyle = null;

        private static GUIStyle preferencesPaneGUIStyle = null;

        private static GUIStyle plainButtonGUIStyle = null;

        private static GUIStyle transparentButtonGUIStyle = null;

        private static GUIStyle transparentButtonOffsetGUIStyle = null;

        private static GUIStyle buttonGUIStyle = null;

        private static GUIStyle plainTextureGUIStyle = null;

        private static GUIStyle selectedBackgroundGUIStyle = null;

        private static GUIStyle errorListDarkBackground = null;

        private static GUIStyle errorListLightBackground = null;

        private static GUIStyle welcomeScreenIntroGUIStyle = null;

        private static GUIStyle welcomeScreenTextHeaderGUIStyle = null;

        private static GUIStyle welcomeScreenTextDescriptionGUIStyle = null;

        private static Texture2D[] taskBorderTexture = new Texture2D[9];

        private static Texture2D taskBorderRunningTexture = null;

        private static Texture2D taskBorderIdentifyTexture = null;

        private static Texture2D[] taskConnectionTopTexture = new Texture2D[9];

        private static Texture2D[] taskConnectionBottomTexture = new Texture2D[9];

        private static Texture2D taskConnectionRunningTopTexture = null;

        private static Texture2D taskConnectionRunningBottomTexture = null;

        private static Texture2D taskConnectionIdentifyTopTexture = null;

        private static Texture2D taskConnectionIdentifyBottomTexture = null;

        private static Texture2D taskConnectionCollapsedTexture = null;

        private static Texture2D contentSeparatorTexture = null;

        private static Texture2D docTexture = null;

        private static Texture2D gearTexture = null;

        private static Texture2D[] colorSelectorTexture = new Texture2D[9];

        private static Texture2D variableButtonTexture = null;

        private static Texture2D variableButtonSelectedTexture = null;

        private static Texture2D variableWatchButtonTexture = null;

        private static Texture2D variableWatchButtonSelectedTexture = null;

        private static Texture2D referencedTexture = null;

        private static Texture2D conditionalAbortSelfTexture = null;

        private static Texture2D conditionalAbortLowerPriorityTexture = null;

        private static Texture2D conditionalAbortBothTexture = null;

        private static Texture2D deleteButtonTexture = null;

        private static Texture2D variableDeleteButtonTexture = null;

        private static Texture2D downArrowButtonTexture = null;

        private static Texture2D upArrowButtonTexture = null;

        private static Texture2D variableMapButtonTexture = null;

        private static Texture2D identifyButtonTexture = null;

        private static Texture2D breakpointTexture = null;

        private static Texture2D errorIconTexture = null;

        private static Texture2D smallErrorIconTexture = null;

        private static Texture2D enableTaskTexture = null;

        private static Texture2D disableTaskTexture = null;

        private static Texture2D expandTaskTexture = null;

        private static Texture2D collapseTaskTexture = null;

        private static Texture2D executionSuccessTexture = null;

        private static Texture2D executionFailureTexture = null;

        private static Texture2D executionSuccessRepeatTexture = null;

        private static Texture2D executionFailureRepeatTexture = null;

        public static Texture2D historyBackwardTexture = null;

        public static Texture2D historyForwardTexture = null;

        private static Texture2D playTexture = null;

        private static Texture2D pauseTexture = null;

        private static Texture2D stepTexture = null;

        private static Texture2D screenshotBackgroundTexture = null;

        private static Regex camelCaseRegex = new Regex("(?<=[A-Z])(?=[A-Z][a-z])|(?<=[^A-Z])(?=[A-Z])|(?<=[A-Za-z])(?=[^A-Za-z])", RegexOptions.IgnorePatternWhitespace);

        private static Dictionary<string, string> camelCaseSplit = new Dictionary<string, string>();

        [NonSerialized]
        private static Dictionary<Type, Dictionary<FieldInfo, bool>> attributeFieldCache = new Dictionary<Type, Dictionary<FieldInfo, bool>>();

        private static Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

        private static Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D>();

        public static GUIStyle GraphStatusGUIStyle
        {
            get
            {
                if (graphStatusGUIStyle == null)
                {
                    InitGraphStatusGUIStyle();
                }
                return graphStatusGUIStyle;
            }
        }

        public static GUIStyle TaskFoldoutGUIStyle
        {
            get
            {
                if (taskFoldoutGUIStyle == null)
                {
                    InitTaskFoldoutGUIStyle();
                }
                return taskFoldoutGUIStyle;
            }
        }

        public static GUIStyle TaskTitleGUIStyle
        {
            get
            {
                if (taskTitleGUIStyle == null)
                {
                    InitTaskTitleGUIStyle();
                }
                return taskTitleGUIStyle;
            }
        }

        public static GUIStyle TaskRunningGUIStyle
        {
            get
            {
                if (taskRunningGUIStyle == null)
                {
                    InitTaskRunningGUIStyle();
                }
                return taskRunningGUIStyle;
            }
        }

        public static GUIStyle TaskRunningCompactGUIStyle
        {
            get
            {
                if (taskRunningCompactGUIStyle == null)
                {
                    InitTaskRunningCompactGUIStyle();
                }
                return taskRunningCompactGUIStyle;
            }
        }

        public static GUIStyle TaskRunningSelectedGUIStyle
        {
            get
            {
                if (taskRunningSelectedGUIStyle == null)
                {
                    InitTaskRunningSelectedGUIStyle();
                }
                return taskRunningSelectedGUIStyle;
            }
        }

        public static GUIStyle TaskRunningSelectedCompactGUIStyle
        {
            get
            {
                if (taskRunningSelectedCompactGUIStyle == null)
                {
                    InitTaskRunningSelectedCompactGUIStyle();
                }
                return taskRunningSelectedCompactGUIStyle;
            }
        }

        public static GUIStyle TaskIdentifyGUIStyle
        {
            get
            {
                if (taskIdentifyGUIStyle == null)
                {
                    InitTaskIdentifyGUIStyle();
                }
                return taskIdentifyGUIStyle;
            }
        }

        public static GUIStyle TaskIdentifyCompactGUIStyle
        {
            get
            {
                if (taskIdentifyCompactGUIStyle == null)
                {
                    InitTaskIdentifyCompactGUIStyle();
                }
                return taskIdentifyCompactGUIStyle;
            }
        }

        public static GUIStyle TaskIdentifySelectedGUIStyle
        {
            get
            {
                if (taskIdentifySelectedGUIStyle == null)
                {
                    InitTaskIdentifySelectedGUIStyle();
                }
                return taskIdentifySelectedGUIStyle;
            }
        }

        public static GUIStyle TaskIdentifySelectedCompactGUIStyle
        {
            get
            {
                if (taskIdentifySelectedCompactGUIStyle == null)
                {
                    InitTaskIdentifySelectedCompactGUIStyle();
                }
                return taskIdentifySelectedCompactGUIStyle;
            }
        }

        public static GUIStyle TaskHighlightGUIStyle
        {
            get
            {
                if (taskHighlightGUIStyle == null)
                {
                    InitTaskHighlightGUIStyle();
                }
                return taskHighlightGUIStyle;
            }
        }

        public static GUIStyle TaskHighlightCompactGUIStyle
        {
            get
            {
                if (taskHighlightCompactGUIStyle == null)
                {
                    InitTaskHighlightCompactGUIStyle();
                }
                return taskHighlightCompactGUIStyle;
            }
        }

        public static GUIStyle TaskCommentGUIStyle
        {
            get
            {
                if (taskCommentGUIStyle == null)
                {
                    InitTaskCommentGUIStyle();
                }
                return taskCommentGUIStyle;
            }
        }

        public static GUIStyle TaskCommentLeftAlignGUIStyle
        {
            get
            {
                if (taskCommentLeftAlignGUIStyle == null)
                {
                    InitTaskCommentLeftAlignGUIStyle();
                }
                return taskCommentLeftAlignGUIStyle;
            }
        }

        public static GUIStyle TaskCommentRightAlignGUIStyle
        {
            get
            {
                if (taskCommentRightAlignGUIStyle == null)
                {
                    InitTaskCommentRightAlignGUIStyle();
                }
                return taskCommentRightAlignGUIStyle;
            }
        }

        public static GUIStyle TaskDescriptionGUIStyle
        {
            get
            {
                if (taskDescriptionGUIStyle == null)
                {
                    InitTaskDescriptionGUIStyle();
                }
                return taskDescriptionGUIStyle;
            }
        }

        public static GUIStyle GraphBackgroundGUIStyle
        {
            get
            {
                if (graphBackgroundGUIStyle == null)
                {
                    InitGraphBackgroundGUIStyle();
                }
                return graphBackgroundGUIStyle;
            }
        }

        public static GUIStyle SelectionGUIStyle
        {
            get
            {
                if (selectionGUIStyle == null)
                {
                    InitSelectionGUIStyle();
                }
                return selectionGUIStyle;
            }
        }

        public static GUIStyle SharedVariableToolbarPopup
        {
            get
            {
                if (sharedVariableToolbarPopup == null)
                {
                    InitSharedVariableToolbarPopup();
                }
                return sharedVariableToolbarPopup;
            }
        }

        public static GUIStyle LabelWrapGUIStyle
        {
            get
            {
                if (labelWrapGUIStyle == null)
                {
                    InitLabelWrapGUIStyle();
                }
                return labelWrapGUIStyle;
            }
        }

        public static GUIStyle LabelTitleGUIStyle
        {
            get
            {
                if (labelTitleGUIStyle == null)
                {
                    InitLabelTitleGUIStyle();
                }
                return labelTitleGUIStyle;
            }
        }

        public static GUIStyle BoldLabelGUIStyle
        {
            get
            {
                if (boldLabelGUIStyle == null)
                {
                    InitBoldLabelGUIStyle();
                }
                return boldLabelGUIStyle;
            }
        }

        public static GUIStyle ToolbarButtonLeftAlignGUIStyle
        {
            get
            {
                if (toolbarButtonLeftAlignGUIStyle == null)
                {
                    InitToolbarButtonLeftAlignGUIStyle();
                }
                return toolbarButtonLeftAlignGUIStyle;
            }
        }

        public static GUIStyle ToolbarLabelGUIStyle
        {
            get
            {
                if (toolbarLabelGUIStyle == null)
                {
                    InitToolbarLabelGUIStyle();
                }
                return toolbarLabelGUIStyle;
            }
        }

        public static GUIStyle TaskInspectorCommentGUIStyle
        {
            get
            {
                if (taskInspectorCommentGUIStyle == null)
                {
                    InitTaskInspectorCommentGUIStyle();
                }
                return taskInspectorCommentGUIStyle;
            }
        }

        public static GUIStyle TaskInspectorGUIStyle
        {
            get
            {
                if (taskInspectorGUIStyle == null)
                {
                    InitTaskInspectorGUIStyle();
                }
                return taskInspectorGUIStyle;
            }
        }

        public static GUIStyle ToolbarButtonSelectionGUIStyle
        {
            get
            {
                if (toolbarButtonSelectionGUIStyle == null)
                {
                    InitToolbarButtonSelectionGUIStyle();
                }
                return toolbarButtonSelectionGUIStyle;
            }
        }

        public static GUIStyle PropertyBoxGUIStyle
        {
            get
            {
                if (propertyBoxGUIStyle == null)
                {
                    InitPropertyBoxGUIStyle();
                }
                return propertyBoxGUIStyle;
            }
        }

        public static GUIStyle PreferencesPaneGUIStyle
        {
            get
            {
                if (preferencesPaneGUIStyle == null)
                {
                    InitPreferencesPaneGUIStyle();
                }
                return preferencesPaneGUIStyle;
            }
        }

        public static GUIStyle PlainButtonGUIStyle
        {
            get
            {
                if (plainButtonGUIStyle == null)
                {
                    InitPlainButtonGUIStyle();
                }
                return plainButtonGUIStyle;
            }
        }

        public static GUIStyle TransparentButtonGUIStyle
        {
            get
            {
                if (transparentButtonGUIStyle == null)
                {
                    InitTransparentButtonGUIStyle();
                }
                return transparentButtonGUIStyle;
            }
        }

        public static GUIStyle TransparentButtonOffsetGUIStyle
        {
            get
            {
                if (transparentButtonOffsetGUIStyle == null)
                {
                    InitTransparentButtonOffsetGUIStyle();
                }
                return transparentButtonOffsetGUIStyle;
            }
        }

        public static GUIStyle ButtonGUIStyle
        {
            get
            {
                if (buttonGUIStyle == null)
                {
                    InitButtonGUIStyle();
                }
                return buttonGUIStyle;
            }
        }

        public static GUIStyle PlainTextureGUIStyle
        {
            get
            {
                if (plainTextureGUIStyle == null)
                {
                    InitPlainTextureGUIStyle();
                }
                return plainTextureGUIStyle;
            }
        }

        public static GUIStyle SelectedBackgroundGUIStyle
        {
            get
            {
                if (selectedBackgroundGUIStyle == null)
                {
                    InitSelectedBackgroundGUIStyle();
                }
                return selectedBackgroundGUIStyle;
            }
        }

        public static GUIStyle ErrorListDarkBackground
        {
            get
            {
                if (errorListDarkBackground == null)
                {
                    InitErrorListDarkBackground();
                }
                return errorListDarkBackground;
            }
        }

        public static GUIStyle ErrorListLightBackground
        {
            get
            {
                if (errorListLightBackground == null)
                {
                    InitErrorListLightBackground();
                }
                return errorListLightBackground;
            }
        }

        public static GUIStyle WelcomeScreenIntroGUIStyle
        {
            get
            {
                if (welcomeScreenIntroGUIStyle == null)
                {
                    InitWelcomeScreenIntroGUIStyle();
                }
                return welcomeScreenIntroGUIStyle;
            }
        }

        public static GUIStyle WelcomeScreenTextHeaderGUIStyle
        {
            get
            {
                if (welcomeScreenTextHeaderGUIStyle == null)
                {
                    InitWelcomeScreenTextHeaderGUIStyle();
                }
                return welcomeScreenTextHeaderGUIStyle;
            }
        }

        public static GUIStyle WelcomeScreenTextDescriptionGUIStyle
        {
            get
            {
                if (welcomeScreenTextDescriptionGUIStyle == null)
                {
                    InitWelcomeScreenTextDescriptionGUIStyle();
                }
                return welcomeScreenTextDescriptionGUIStyle;
            }
        }

        public static Texture2D TaskBorderRunningTexture
        {
            get
            {
                if (taskBorderRunningTexture == null)
                {
                    InitTaskBorderRunningTexture();
                }
                return taskBorderRunningTexture;
            }
        }

        public static Texture2D TaskBorderIdentifyTexture
        {
            get
            {
                if (taskBorderIdentifyTexture == null)
                {
                    InitTaskBorderIdentifyTexture();
                }
                return taskBorderIdentifyTexture;
            }
        }

        public static Texture2D TaskConnectionRunningTopTexture
        {
            get
            {
                if (taskConnectionRunningTopTexture == null)
                {
                    InitTaskConnectionRunningTopTexture();
                }
                return taskConnectionRunningTopTexture;
            }
        }

        public static Texture2D TaskConnectionRunningBottomTexture
        {
            get
            {
                if (taskConnectionRunningBottomTexture == null)
                {
                    InitTaskConnectionRunningBottomTexture();
                }
                return taskConnectionRunningBottomTexture;
            }
        }

        public static Texture2D TaskConnectionIdentifyTopTexture
        {
            get
            {
                if (taskConnectionIdentifyTopTexture == null)
                {
                    InitTaskConnectionIdentifyTopTexture();
                }
                return taskConnectionIdentifyTopTexture;
            }
        }

        public static Texture2D TaskConnectionIdentifyBottomTexture
        {
            get
            {
                if (taskConnectionIdentifyBottomTexture == null)
                {
                    InitTaskConnectionIdentifyBottomTexture();
                }
                return taskConnectionIdentifyBottomTexture;
            }
        }

        public static Texture2D TaskConnectionCollapsedTexture
        {
            get
            {
                if (taskConnectionCollapsedTexture == null)
                {
                    InitTaskConnectionCollapsedTexture();
                }
                return taskConnectionCollapsedTexture;
            }
        }

        public static Texture2D ContentSeparatorTexture
        {
            get
            {
                if (contentSeparatorTexture == null)
                {
                    InitContentSeparatorTexture();
                }
                return contentSeparatorTexture;
            }
        }

        public static Texture2D DocTexture
        {
            get
            {
                if (docTexture == null)
                {
                    InitDocTexture();
                }
                return docTexture;
            }
        }

        public static Texture2D GearTexture
        {
            get
            {
                if (gearTexture == null)
                {
                    InitGearTexture();
                }
                return gearTexture;
            }
        }

        public static Texture2D VariableButtonTexture
        {
            get
            {
                if (variableButtonTexture == null)
                {
                    InitVariableButtonTexture();
                }
                return variableButtonTexture;
            }
        }

        public static Texture2D VariableButtonSelectedTexture
        {
            get
            {
                if (variableButtonSelectedTexture == null)
                {
                    InitVariableButtonSelectedTexture();
                }
                return variableButtonSelectedTexture;
            }
        }

        public static Texture2D VariableWatchButtonTexture
        {
            get
            {
                if (variableWatchButtonTexture == null)
                {
                    InitVariableWatchButtonTexture();
                }
                return variableWatchButtonTexture;
            }
        }

        public static Texture2D VariableWatchButtonSelectedTexture
        {
            get
            {
                if (variableWatchButtonSelectedTexture == null)
                {
                    InitVariableWatchButtonSelectedTexture();
                }
                return variableWatchButtonSelectedTexture;
            }
        }

        public static Texture2D ReferencedTexture
        {
            get
            {
                if (referencedTexture == null)
                {
                    InitReferencedTexture();
                }
                return referencedTexture;
            }
        }

        public static Texture2D ConditionalAbortSelfTexture
        {
            get
            {
                if (conditionalAbortSelfTexture == null)
                {
                    InitConditionalAbortSelfTexture();
                }
                return conditionalAbortSelfTexture;
            }
        }

        public static Texture2D ConditionalAbortLowerPriorityTexture
        {
            get
            {
                if (conditionalAbortLowerPriorityTexture == null)
                {
                    InitConditionalAbortLowerPriorityTexture();
                }
                return conditionalAbortLowerPriorityTexture;
            }
        }

        public static Texture2D ConditionalAbortBothTexture
        {
            get
            {
                if (conditionalAbortBothTexture == null)
                {
                    InitConditionalAbortBothTexture();
                }
                return conditionalAbortBothTexture;
            }
        }

        public static Texture2D DeleteButtonTexture
        {
            get
            {
                if (deleteButtonTexture == null)
                {
                    InitDeleteButtonTexture();
                }
                return deleteButtonTexture;
            }
        }

        public static Texture2D VariableDeleteButtonTexture
        {
            get
            {
                if (variableDeleteButtonTexture == null)
                {
                    InitVariableDeleteButtonTexture();
                }
                return variableDeleteButtonTexture;
            }
        }

        public static Texture2D DownArrowButtonTexture
        {
            get
            {
                if (downArrowButtonTexture == null)
                {
                    InitDownArrowButtonTexture();
                }
                return downArrowButtonTexture;
            }
        }

        public static Texture2D UpArrowButtonTexture
        {
            get
            {
                if (upArrowButtonTexture == null)
                {
                    InitUpArrowButtonTexture();
                }
                return upArrowButtonTexture;
            }
        }

        public static Texture2D VariableMapButtonTexture
        {
            get
            {
                if (variableMapButtonTexture == null)
                {
                    InitVariableMapButtonTexture();
                }
                return variableMapButtonTexture;
            }
        }

        public static Texture2D IdentifyButtonTexture
        {
            get
            {
                if (identifyButtonTexture == null)
                {
                    InitIdentifyButtonTexture();
                }
                return identifyButtonTexture;
            }
        }

        public static Texture2D BreakpointTexture
        {
            get
            {
                if (breakpointTexture == null)
                {
                    InitBreakpointTexture();
                }
                return breakpointTexture;
            }
        }

        public static Texture2D ErrorIconTexture
        {
            get
            {
                if (errorIconTexture == null)
                {
                    InitErrorIconTexture();
                }
                return errorIconTexture;
            }
        }

        public static Texture2D SmallErrorIconTexture
        {
            get
            {
                if (smallErrorIconTexture == null)
                {
                    InitSmallErrorIconTexture();
                }
                return smallErrorIconTexture;
            }
        }

        public static Texture2D EnableTaskTexture
        {
            get
            {
                if (enableTaskTexture == null)
                {
                    InitEnableTaskTexture();
                }
                return enableTaskTexture;
            }
        }

        public static Texture2D DisableTaskTexture
        {
            get
            {
                if (disableTaskTexture == null)
                {
                    InitDisableTaskTexture();
                }
                return disableTaskTexture;
            }
        }

        public static Texture2D ExpandTaskTexture
        {
            get
            {
                if (expandTaskTexture == null)
                {
                    InitExpandTaskTexture();
                }
                return expandTaskTexture;
            }
        }

        public static Texture2D CollapseTaskTexture
        {
            get
            {
                if (collapseTaskTexture == null)
                {
                    InitCollapseTaskTexture();
                }
                return collapseTaskTexture;
            }
        }

        public static Texture2D ExecutionSuccessTexture
        {
            get
            {
                if (executionSuccessTexture == null)
                {
                    InitExecutionSuccessTexture();
                }
                return executionSuccessTexture;
            }
        }

        public static Texture2D ExecutionFailureTexture
        {
            get
            {
                if (executionFailureTexture == null)
                {
                    InitExecutionFailureTexture();
                }
                return executionFailureTexture;
            }
        }

        public static Texture2D ExecutionSuccessRepeatTexture
        {
            get
            {
                if (executionSuccessRepeatTexture == null)
                {
                    InitExecutionSuccessRepeatTexture();
                }
                return executionSuccessRepeatTexture;
            }
        }

        public static Texture2D ExecutionFailureRepeatTexture
        {
            get
            {
                if (executionFailureRepeatTexture == null)
                {
                    InitExecutionFailureRepeatTexture();
                }
                return executionFailureRepeatTexture;
            }
        }

        public static Texture2D HistoryBackwardTexture
        {
            get
            {
                if (historyBackwardTexture == null)
                {
                    InitHistoryBackwardTexture();
                }
                return historyBackwardTexture;
            }
        }

        public static Texture2D HistoryForwardTexture
        {
            get
            {
                if (historyForwardTexture == null)
                {
                    InitHistoryForwardTexture();
                }
                return historyForwardTexture;
            }
        }

        public static Texture2D PlayTexture
        {
            get
            {
                if (playTexture == null)
                {
                    InitPlayTexture();
                }
                return playTexture;
            }
        }

        public static Texture2D PauseTexture
        {
            get
            {
                if (pauseTexture == null)
                {
                    InitPauseTexture();
                }
                return pauseTexture;
            }
        }

        public static Texture2D StepTexture
        {
            get
            {
                if (stepTexture == null)
                {
                    InitStepTexture();
                }
                return stepTexture;
            }
        }

        public static Texture2D ScreenshotBackgroundTexture
        {
            get
            {
                if (screenshotBackgroundTexture == null)
                {
                    InitScreenshotBackgroundTexture();
                }
                return screenshotBackgroundTexture;
            }
        }

        public static GUIStyle GetTaskGUIStyle(int colorIndex)
        {
            if (taskGUIStyle[colorIndex] == null)
            {
                InitTaskGUIStyle(colorIndex);
            }
            return taskGUIStyle[colorIndex];
        }

        public static GUIStyle GetTaskCompactGUIStyle(int colorIndex)
        {
            if (taskCompactGUIStyle[colorIndex] == null)
            {
                InitTaskCompactGUIStyle(colorIndex);
            }
            return taskCompactGUIStyle[colorIndex];
        }

        public static GUIStyle GetTaskSelectedGUIStyle(int colorIndex)
        {
            if (taskSelectedGUIStyle[colorIndex] == null)
            {
                InitTaskSelectedGUIStyle(colorIndex);
            }
            return taskSelectedGUIStyle[colorIndex];
        }

        public static GUIStyle GetTaskSelectedCompactGUIStyle(int colorIndex)
        {
            if (taskSelectedCompactGUIStyle[colorIndex] == null)
            {
                InitTaskSelectedCompactGUIStyle(colorIndex);
            }
            return taskSelectedCompactGUIStyle[colorIndex];
        }

        public static Texture2D GetTaskBorderTexture(int colorIndex)
        {
            if (taskBorderTexture[colorIndex] == null)
            {
                InitTaskBorderTexture(colorIndex);
            }
            return taskBorderTexture[colorIndex];
        }

        public static Texture2D GetTaskConnectionTopTexture(int colorIndex)
        {
            if (taskConnectionTopTexture[colorIndex] == null)
            {
                InitTaskConnectionTopTexture(colorIndex);
            }
            return taskConnectionTopTexture[colorIndex];
        }

        public static Texture2D GetTaskConnectionBottomTexture(int colorIndex)
        {
            if (taskConnectionBottomTexture[colorIndex] == null)
            {
                InitTaskConnectionBottomTexture(colorIndex);
            }
            return taskConnectionBottomTexture[colorIndex];
        }

        public static Texture2D ColorSelectorTexture(int colorIndex)
        {
            if (colorSelectorTexture[colorIndex] == null)
            {
                InitColorSelectorTexture(colorIndex);
            }
            return colorSelectorTexture[colorIndex];
        }

        public static string SplitCamelCase(string s)
        {
            if (s.Equals(string.Empty))
            {
                return s;
            }
            if (camelCaseSplit.ContainsKey(s))
            {
                return camelCaseSplit[s];
            }
            string key = s;
            s = s.Replace("_uScript", "uScript");
            s = s.Replace("_PlayMaker", "PlayMaker");
            if (s.Length > 2 && s.Substring(0, 2).CompareTo("m_") == 0)
            {
                s = s.Substring(2);
            }
            else if (s.Length > 1 && s[0].CompareTo('_') == 0)
            {
                s = s.Substring(1);
            }
            s = camelCaseRegex.Replace(s, " ");
            s = s.Replace("_", " ");
            s = s.Replace("u Script", " uScript");
            s = s.Replace("Play Maker", "PlayMaker");
            s = (char.ToUpper(s[0]) + s.Substring(1)).Trim();
            camelCaseSplit.Add(key, s);
            return s;
        }

        public static bool HasAttribute(FieldInfo field, Type attributeType)
        {
            Dictionary<FieldInfo, bool> dictionary = null;
            if (attributeFieldCache.ContainsKey(attributeType))
            {
                dictionary = attributeFieldCache[attributeType];
            }
            if (dictionary == null)
            {
                dictionary = new Dictionary<FieldInfo, bool>();
            }
            if (dictionary.ContainsKey(field))
            {
                return dictionary[field];
            }
            bool flag = field.GetCustomAttributes(attributeType, inherit: false).Length > 0;
            dictionary.Add(field, flag);
            if (!attributeFieldCache.ContainsKey(attributeType))
            {
                attributeFieldCache.Add(attributeType, dictionary);
            }
            return flag;
        }

        public static List<Task> GetAllTasks(BehaviorSource behaviorSource)
        {
            List<Task> taskList = new List<Task>();
            if (behaviorSource.RootTask != null)
            {
                GetAllTasks(behaviorSource.RootTask, ref taskList);
            }
            if (behaviorSource.DetachedTasks != null)
            {
                for (int i = 0; i < behaviorSource.DetachedTasks.Count; i++)
                {
                    GetAllTasks(behaviorSource.DetachedTasks[i], ref taskList);
                }
            }
            return taskList;
        }

        private static void GetAllTasks(Task task, ref List<Task> taskList)
        {
            taskList.Add(task);
            if (task is ParentTask parentTask && parentTask.Children != null)
            {
                for (int i = 0; i < parentTask.Children.Count; i++)
                {
                    GetAllTasks(parentTask.Children[i], ref taskList);
                }
            }
        }

        public static bool AnyNullTasks(BehaviorSource behaviorSource)
        {
            if (behaviorSource.RootTask != null && AnyNullTasks(behaviorSource.RootTask))
            {
                return true;
            }
            if (behaviorSource.DetachedTasks != null)
            {
                for (int i = 0; i < behaviorSource.DetachedTasks.Count; i++)
                {
                    if (AnyNullTasks(behaviorSource.DetachedTasks[i]))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool AnyNullTasks(Task task)
        {
            if (task == null)
            {
                return true;
            }
            if (task is ParentTask parentTask && parentTask.Children != null)
            {
                for (int i = 0; i < parentTask.Children.Count; i++)
                {
                    if (AnyNullTasks(parentTask.Children[i]))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool HasRootTask(string serialization)
        {
            if (string.IsNullOrEmpty(serialization))
            {
                return false;
            }
            if (MiniJSON.Deserialize(serialization) is Dictionary<string, object> dictionary && dictionary.ContainsKey("RootTask"))
            {
                return true;
            }
            return false;
        }

        public static string GetEditorBaseDirectory(UnityEngine.Object obj = null)
        {
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            string text = Uri.UnescapeDataString(new UriBuilder(codeBase).Path);
            return Path.GetDirectoryName(text.Substring(Application.dataPath.Length - 6));
        }

        public static Texture2D LoadTexture(string imageName, bool useSkinColor = true, UnityEngine.Object obj = null)
        {
            if (textureCache.ContainsKey(imageName))
            {
                return textureCache[imageName];
            }
            Texture2D texture2D = null;
            string name = string.Format("{0}{1}", (!useSkinColor) ? string.Empty : ((!EditorGUIUtility.isProSkin) ? "Light" : "Dark"), imageName);
            //Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            name = "Assets/Editor/Behavior Designer/"+ name;
            texture2D = AssetDatabase.LoadAssetAtPath<Texture2D>(name);
            if (texture2D == null)
            {
                name = string.Format("{0}{1}", (!useSkinColor) ? string.Empty : ((!EditorGUIUtility.isProSkin) ? "Light" : "Dark"), imageName);
                name = "Assets/Editor/Behavior Designer/" + name;
                texture2D = AssetDatabase.LoadAssetAtPath<Texture2D>(name);
            }
            //if (manifestResourceStream == null)
            //{
            //    name = string.Format("BehaviorDesignerEditor.Resources.{0}{1}", (!useSkinColor) ? string.Empty : ((!EditorGUIUtility.isProSkin) ? "Light" : "Dark"), imageName);
            //    manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            //}
            //if (manifestResourceStream != null)
            //{
            //    texture2D = new Texture2D(0, 0, TextureFormat.RGBA32, mipChain: false);
            //    texture2D.LoadImage(ReadToEnd(manifestResourceStream));
            //    manifestResourceStream.Close();
            //}
            texture2D.hideFlags = HideFlags.HideAndDontSave;
            textureCache.Add(imageName, texture2D);
            return texture2D;
        }

        private static Texture2D LoadTaskTexture(string imageName, bool useSkinColor = true, ScriptableObject obj = null)
        {
            if (textureCache.ContainsKey(imageName))
            {
                return textureCache[imageName];
            }
            Texture2D texture2D = null;
            string name = string.Format("{0}{1}", (!useSkinColor) ? string.Empty : ((!EditorGUIUtility.isProSkin) ? "Light" : "Dark"), imageName);
            name = "Assets/Editor/Behavior Designer/" + name;
            texture2D = AssetDatabase.LoadAssetAtPath<Texture2D>(name);
            if(texture2D == null)
            {
                name = string.Format("{0}{1}", (!useSkinColor) ? string.Empty : ((!EditorGUIUtility.isProSkin) ? "Light" : "Dark"), imageName);
                name = "Assets/Editor/Behavior Designer/" + name;
                texture2D = AssetDatabase.LoadAssetAtPath<Texture2D>(name);
            }
            //Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            //if (manifestResourceStream == null)
            //{
            //    name = string.Format("BehaviorDesignerEditor.Resources.{0}{1}", (!useSkinColor) ? string.Empty : ((!EditorGUIUtility.isProSkin) ? "Light" : "Dark"), imageName);
            //    manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            //}
            //if (manifestResourceStream != null)
            //{
            //    texture2D = new Texture2D(0, 0, TextureFormat.RGBA32, mipChain: false);
            //    texture2D.LoadImage(ReadToEnd(manifestResourceStream));
            //    manifestResourceStream.Close();
            //}
            //if (texture2D == null)
            //{
            //    Debug.Log(string.Format("{0}/Images/Task Backgrounds/{1}{2}", GetEditorBaseDirectory(obj), (!useSkinColor) ? string.Empty : ((!EditorGUIUtility.isProSkin) ? "Light" : "Dark"), imageName));
            //}
            texture2D.hideFlags = HideFlags.HideAndDontSave;
            textureCache.Add(imageName, texture2D);
            return texture2D;
        }

        public static Texture2D LoadIcon(string iconName, ScriptableObject obj = null)
        {
            if (iconCache.ContainsKey(iconName))
            {
                return iconCache[iconName];
            }
            Texture2D texture2D = null;
            string name = iconName.Replace("{SkinColor}", (!EditorGUIUtility.isProSkin) ? "Light" : "Dark");
            name = "Assets/Editor/Behavior Designer/" + name;
            texture2D = AssetDatabase.LoadAssetAtPath<Texture2D>(name);
            if (texture2D == null)
            {
                name = string.Format("{0}", iconName.Replace("{SkinColor}", (!EditorGUIUtility.isProSkin) ? "Light" : "Dark"));
                name = "Assets/Editor/Behavior Designer/" + name;
                texture2D = AssetDatabase.LoadAssetAtPath<Texture2D>(name);
            }
                //Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
                //if (manifestResourceStream == null)
                //{
                //    name = string.Format("BehaviorDesignerEditor.Resources.{0}", iconName.Replace("{SkinColor}", (!EditorGUIUtility.isProSkin) ? "Light" : "Dark"));
                //    manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
                //}
                //if (manifestResourceStream != null)
                //{
                //    texture2D = new Texture2D(0, 0, TextureFormat.RGBA32, mipChain: false);
                //    texture2D.LoadImage(ReadToEnd(manifestResourceStream));
                //    manifestResourceStream.Close();
                //}
            if (texture2D == null)
            {
                name = iconName.Replace("{SkinColor}", (!EditorGUIUtility.isProSkin) ? "Light" : "Dark");
                name = "Assets/Editor/Behavior Designer/" + name;
                texture2D = AssetDatabase.LoadAssetAtPath<Texture2D>(name);
                //texture2D = AssetDatabase.LoadAssetAtPath(iconName.Replace("{SkinColor}", (!EditorGUIUtility.isProSkin) ? "Light" : "Dark"), typeof(Texture2D)) as Texture2D;
            }
            if (texture2D != null)
            {
                texture2D.hideFlags = HideFlags.HideAndDontSave;
            }
            iconCache.Add(iconName, texture2D);
            return texture2D;
        }

        private static byte[] ReadToEnd(Stream stream)
        {
            byte[] array = new byte[16384];
            using MemoryStream memoryStream = new MemoryStream();
            int count;
            while ((count = stream.Read(array, 0, array.Length)) > 0)
            {
                memoryStream.Write(array, 0, count);
            }
            return memoryStream.ToArray();
        }

        public static void DrawContentSeperator(int yOffset)
        {
            DrawContentSeperator(yOffset, 0);
        }

        public static void DrawContentSeperator(int yOffset, int widthExtension)
        {
            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.x = -5f;
            lastRect.y += lastRect.height + (float)yOffset;
            lastRect.height = 2f;
            lastRect.width += 10 + widthExtension;
            GUI.DrawTexture(lastRect, ContentSeparatorTexture);
        }

        public static float RoundToNearest(float num, float baseNum)
        {
            return (float)(int)Math.Round(num / baseNum, MidpointRounding.AwayFromZero) * baseNum;
        }

        private static void InitGraphStatusGUIStyle()
        {
            graphStatusGUIStyle = new GUIStyle(GUI.skin.label);
            graphStatusGUIStyle.alignment = TextAnchor.MiddleLeft;
            graphStatusGUIStyle.fontSize = 20;
            graphStatusGUIStyle.fontStyle = FontStyle.Bold;
            if (EditorGUIUtility.isProSkin)
            {
                graphStatusGUIStyle.normal.textColor = new Color(0.7058f, 0.7058f, 0.7058f);
            }
            else
            {
                graphStatusGUIStyle.normal.textColor = new Color(0.8058f, 0.8058f, 0.8058f);
            }
        }

        private static void InitTaskFoldoutGUIStyle()
        {
            taskFoldoutGUIStyle = new GUIStyle(EditorStyles.foldout);
            taskFoldoutGUIStyle.alignment = TextAnchor.MiddleLeft;
            taskFoldoutGUIStyle.fontSize = 13;
            taskFoldoutGUIStyle.fontStyle = FontStyle.Bold;
        }

        private static void InitTaskTitleGUIStyle()
        {
            taskTitleGUIStyle = new GUIStyle(GUI.skin.label);
            taskTitleGUIStyle.alignment = TextAnchor.UpperCenter;
            taskTitleGUIStyle.fontSize = 12;
            taskTitleGUIStyle.fontStyle = FontStyle.Normal;
        }

        private static void InitTaskGUIStyle(int colorIndex)
        {
            taskGUIStyle[colorIndex] = InitTaskGUIStyle(LoadTaskTexture("Task" + ColorIndexToColorString(colorIndex) + ".png"), new RectOffset(5, 3, 3, 5));
        }

        private static void InitTaskCompactGUIStyle(int colorIndex)
        {
            taskCompactGUIStyle[colorIndex] = InitTaskGUIStyle(LoadTaskTexture("TaskCompact" + ColorIndexToColorString(colorIndex) + ".png"), new RectOffset(5, 4, 4, 5));
        }

        private static void InitTaskSelectedGUIStyle(int colorIndex)
        {
            taskSelectedGUIStyle[colorIndex] = InitTaskGUIStyle(LoadTaskTexture("TaskSelected" + ColorIndexToColorString(colorIndex) + ".png"), new RectOffset(5, 4, 4, 4));
        }

        private static void InitTaskSelectedCompactGUIStyle(int colorIndex)
        {
            taskSelectedCompactGUIStyle[colorIndex] = InitTaskGUIStyle(LoadTaskTexture("TaskSelectedCompact" + ColorIndexToColorString(colorIndex) + ".png"), new RectOffset(5, 4, 4, 4));
        }

        private static string ColorIndexToColorString(int index)
        {
            return index switch
            {
                0 => string.Empty,
                1 => "Red",
                2 => "Pink",
                3 => "Brown",
                4 => "RedOrange",
                5 => "Turquoise",
                6 => "Cyan",
                7 => "Blue",
                8 => "Purple",
                _ => string.Empty,
            };
        }

        private static void InitTaskRunningGUIStyle()
        {
            taskRunningGUIStyle = InitTaskGUIStyle(LoadTaskTexture("TaskRunning.png"), new RectOffset(5, 3, 3, 5));
        }

        private static void InitTaskRunningCompactGUIStyle()
        {
            taskRunningCompactGUIStyle = InitTaskGUIStyle(LoadTaskTexture("TaskRunningCompact.png"), new RectOffset(5, 4, 4, 5));
        }

        private static void InitTaskRunningSelectedGUIStyle()
        {
            taskRunningSelectedGUIStyle = InitTaskGUIStyle(LoadTaskTexture("TaskRunningSelected.png"), new RectOffset(5, 4, 4, 4));
        }

        private static void InitTaskRunningSelectedCompactGUIStyle()
        {
            taskRunningSelectedCompactGUIStyle = InitTaskGUIStyle(LoadTaskTexture("TaskRunningSelectedCompact.png"), new RectOffset(5, 4, 4, 4));
        }

        private static void InitTaskIdentifyGUIStyle()
        {
            taskIdentifyGUIStyle = InitTaskGUIStyle(LoadTaskTexture("TaskIdentify.png"), new RectOffset(5, 3, 3, 5));
        }

        private static void InitTaskIdentifyCompactGUIStyle()
        {
            taskIdentifyCompactGUIStyle = InitTaskGUIStyle(LoadTaskTexture("TaskIdentifyCompact.png"), new RectOffset(5, 4, 4, 5));
        }

        private static void InitTaskIdentifySelectedGUIStyle()
        {
            taskIdentifySelectedGUIStyle = InitTaskGUIStyle(LoadTaskTexture("TaskIdentifySelected.png"), new RectOffset(5, 4, 4, 4));
        }

        private static void InitTaskIdentifySelectedCompactGUIStyle()
        {
            taskIdentifySelectedCompactGUIStyle = InitTaskGUIStyle(LoadTaskTexture("TaskIdentifySelectedCompact.png"), new RectOffset(5, 4, 4, 4));
        }

        private static void InitTaskHighlightGUIStyle()
        {
            taskHighlightGUIStyle = InitTaskGUIStyle(LoadTaskTexture("TaskHighlight.png"), new RectOffset(5, 4, 4, 4));
        }

        private static void InitTaskHighlightCompactGUIStyle()
        {
            taskHighlightCompactGUIStyle = InitTaskGUIStyle(LoadTaskTexture("TaskHighlightCompact.png"), new RectOffset(5, 4, 4, 4));
        }

        private static GUIStyle InitTaskGUIStyle(Texture2D texture, RectOffset overflow)
        {
            GUIStyle gUIStyle = new GUIStyle();
            gUIStyle.border = new RectOffset(10, 10, 10, 10);
            gUIStyle.overflow = overflow;
            gUIStyle.normal.background = texture;
            gUIStyle.active.background = texture;
            gUIStyle.hover.background = texture;
            gUIStyle.focused.background = texture;
            gUIStyle.normal.textColor = Color.white;
            gUIStyle.active.textColor = Color.white;
            gUIStyle.hover.textColor = Color.white;
            gUIStyle.focused.textColor = Color.white;
            gUIStyle.stretchHeight = true;
            gUIStyle.stretchWidth = true;
            return gUIStyle;
        }

        private static void InitTaskCommentGUIStyle()
        {
            taskCommentGUIStyle = new GUIStyle(GUI.skin.label);
            taskCommentGUIStyle.alignment = TextAnchor.UpperCenter;
            taskCommentGUIStyle.fontSize = 12;
            taskCommentGUIStyle.fontStyle = FontStyle.Normal;
            taskCommentGUIStyle.wordWrap = true;
        }

        private static void InitTaskCommentLeftAlignGUIStyle()
        {
            taskCommentLeftAlignGUIStyle = new GUIStyle(GUI.skin.label);
            taskCommentLeftAlignGUIStyle.alignment = TextAnchor.UpperLeft;
            taskCommentLeftAlignGUIStyle.fontSize = 12;
            taskCommentLeftAlignGUIStyle.fontStyle = FontStyle.Normal;
            taskCommentLeftAlignGUIStyle.wordWrap = false;
        }

        private static void InitTaskCommentRightAlignGUIStyle()
        {
            taskCommentRightAlignGUIStyle = new GUIStyle(GUI.skin.label);
            taskCommentRightAlignGUIStyle.alignment = TextAnchor.UpperRight;
            taskCommentRightAlignGUIStyle.fontSize = 12;
            taskCommentRightAlignGUIStyle.fontStyle = FontStyle.Normal;
            taskCommentRightAlignGUIStyle.wordWrap = false;
        }

        private static void InitTaskDescriptionGUIStyle()
        {
            Texture2D texture2D = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            if (EditorGUIUtility.isProSkin)
            {
                texture2D.SetPixel(1, 1, new Color(0.1647f, 0.1647f, 0.1647f));
            }
            else
            {
                texture2D.SetPixel(1, 1, new Color(0.75f, 0.75f, 0.75f));
            }
            texture2D.hideFlags = HideFlags.HideAndDontSave;
            texture2D.Apply();
            taskDescriptionGUIStyle = new GUIStyle();
            taskDescriptionGUIStyle.normal.background = texture2D;
            taskDescriptionGUIStyle.active.background = texture2D;
            taskDescriptionGUIStyle.hover.background = texture2D;
            taskDescriptionGUIStyle.focused.background = texture2D;
        }

        private static void InitGraphBackgroundGUIStyle()
        {
            Texture2D texture2D = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            if (EditorGUIUtility.isProSkin)
            {
                texture2D.SetPixel(1, 1, new Color(0.1647f, 0.1647f, 0.1647f));
            }
            else
            {
                texture2D.SetPixel(1, 1, new Color(0.3647f, 0.3647f, 0.3647f));
            }
            texture2D.hideFlags = HideFlags.HideAndDontSave;
            texture2D.Apply();
            graphBackgroundGUIStyle = new GUIStyle();
            graphBackgroundGUIStyle.normal.background = texture2D;
            graphBackgroundGUIStyle.active.background = texture2D;
            graphBackgroundGUIStyle.hover.background = texture2D;
            graphBackgroundGUIStyle.focused.background = texture2D;
        }

        private static void InitSelectionGUIStyle()
        {
            Texture2D texture2D = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            Color color = ((!EditorGUIUtility.isProSkin) ? new Color(0.243f, 0.5686f, 0.839f, 0.5f) : new Color(0.188f, 0.4588f, 0.6862f, 0.5f));
            texture2D.SetPixel(1, 1, color);
            texture2D.hideFlags = HideFlags.HideAndDontSave;
            texture2D.Apply();
            selectionGUIStyle = new GUIStyle();
            selectionGUIStyle.normal.background = texture2D;
            selectionGUIStyle.active.background = texture2D;
            selectionGUIStyle.hover.background = texture2D;
            selectionGUIStyle.focused.background = texture2D;
            selectionGUIStyle.normal.textColor = Color.white;
            selectionGUIStyle.active.textColor = Color.white;
            selectionGUIStyle.hover.textColor = Color.white;
            selectionGUIStyle.focused.textColor = Color.white;
        }

        private static void InitSharedVariableToolbarPopup()
        {
            sharedVariableToolbarPopup = new GUIStyle(EditorStyles.toolbarPopup);
            sharedVariableToolbarPopup.margin = new RectOffset(4, 4, 0, 0);
        }

        private static void InitLabelWrapGUIStyle()
        {
            labelWrapGUIStyle = new GUIStyle(GUI.skin.label);
            labelWrapGUIStyle.wordWrap = true;
            labelWrapGUIStyle.alignment = TextAnchor.MiddleCenter;
        }

        private static void InitLabelTitleGUIStyle()
        {
            labelTitleGUIStyle = new GUIStyle(GUI.skin.label);
            labelTitleGUIStyle.wordWrap = true;
            labelTitleGUIStyle.alignment = TextAnchor.MiddleCenter;
            labelTitleGUIStyle.fontSize = 14;
        }

        private static void InitBoldLabelGUIStyle()
        {
            boldLabelGUIStyle = new GUIStyle(GUI.skin.label);
            boldLabelGUIStyle.fontStyle = FontStyle.Bold;
        }

        private static void InitToolbarButtonLeftAlignGUIStyle()
        {
            toolbarButtonLeftAlignGUIStyle = new GUIStyle(EditorStyles.toolbarButton);
            toolbarButtonLeftAlignGUIStyle.alignment = TextAnchor.MiddleLeft;
        }

        private static void InitToolbarLabelGUIStyle()
        {
            toolbarLabelGUIStyle = new GUIStyle(EditorStyles.label);
            toolbarLabelGUIStyle.normal.textColor = ((!EditorGUIUtility.isProSkin) ? new Color(0f, 0.5f, 0f) : new Color(0f, 0.7f, 0f));
        }

        private static void InitTaskInspectorCommentGUIStyle()
        {
            taskInspectorCommentGUIStyle = new GUIStyle(GUI.skin.textArea);
            taskInspectorCommentGUIStyle.wordWrap = true;
        }

        private static void InitTaskInspectorGUIStyle()
        {
            taskInspectorGUIStyle = new GUIStyle(GUI.skin.label);
            taskInspectorGUIStyle.alignment = TextAnchor.MiddleLeft;
            taskInspectorGUIStyle.fontSize = 11;
            taskInspectorGUIStyle.fontStyle = FontStyle.Normal;
        }

        private static void InitToolbarButtonSelectionGUIStyle()
        {
            toolbarButtonSelectionGUIStyle = new GUIStyle(EditorStyles.toolbarButton);
            toolbarButtonSelectionGUIStyle.normal.background = toolbarButtonSelectionGUIStyle.active.background;
        }

        private static void InitPreferencesPaneGUIStyle()
        {
            preferencesPaneGUIStyle = new GUIStyle();
            Texture2D texture2D = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            Color color = ((!EditorGUIUtility.isProSkin) ? new Color(0.706f, 0.706f, 0.706f) : new Color(0.2f, 0.2f, 0.2f, 1f));
            texture2D.SetPixel(1, 1, color);
            texture2D.hideFlags = HideFlags.HideAndDontSave;
            texture2D.Apply();
            preferencesPaneGUIStyle.normal.background = texture2D;
        }

        private static void InitPropertyBoxGUIStyle()
        {
            propertyBoxGUIStyle = new GUIStyle();
            propertyBoxGUIStyle.padding = new RectOffset(2, 2, 0, 0);
        }

        private static void InitPlainButtonGUIStyle()
        {
            plainButtonGUIStyle = new GUIStyle(GUI.skin.button);
            plainButtonGUIStyle.border = new RectOffset(0, 0, 0, 0);
            plainButtonGUIStyle.margin = new RectOffset(0, 0, 2, 2);
            plainButtonGUIStyle.padding = new RectOffset(0, 0, 1, 0);
            plainButtonGUIStyle.normal.background = null;
            plainButtonGUIStyle.active.background = null;
            plainButtonGUIStyle.hover.background = null;
            plainButtonGUIStyle.focused.background = null;
            plainButtonGUIStyle.normal.textColor = Color.white;
            plainButtonGUIStyle.active.textColor = Color.white;
            plainButtonGUIStyle.hover.textColor = Color.white;
            plainButtonGUIStyle.focused.textColor = Color.white;
        }

        private static void InitTransparentButtonGUIStyle()
        {
            transparentButtonGUIStyle = new GUIStyle(GUI.skin.button);
            transparentButtonGUIStyle.border = new RectOffset(0, 0, 0, 0);
            transparentButtonGUIStyle.margin = new RectOffset(4, 4, 2, 2);
            transparentButtonGUIStyle.padding = new RectOffset(2, 2, 1, 0);
            transparentButtonGUIStyle.normal.background = null;
            transparentButtonGUIStyle.active.background = null;
            transparentButtonGUIStyle.hover.background = null;
            transparentButtonGUIStyle.focused.background = null;
            transparentButtonGUIStyle.normal.textColor = Color.white;
            transparentButtonGUIStyle.active.textColor = Color.white;
            transparentButtonGUIStyle.hover.textColor = Color.white;
            transparentButtonGUIStyle.focused.textColor = Color.white;
        }

        private static void InitTransparentButtonOffsetGUIStyle()
        {
            transparentButtonOffsetGUIStyle = new GUIStyle(GUI.skin.button);
            transparentButtonOffsetGUIStyle.border = new RectOffset(0, 0, 0, 0);
            transparentButtonOffsetGUIStyle.margin = new RectOffset(4, 4, 4, 2);
            transparentButtonOffsetGUIStyle.padding = new RectOffset(2, 2, 1, 0);
            transparentButtonOffsetGUIStyle.normal.background = null;
            transparentButtonOffsetGUIStyle.active.background = null;
            transparentButtonOffsetGUIStyle.hover.background = null;
            transparentButtonOffsetGUIStyle.focused.background = null;
            transparentButtonOffsetGUIStyle.normal.textColor = Color.white;
            transparentButtonOffsetGUIStyle.active.textColor = Color.white;
            transparentButtonOffsetGUIStyle.hover.textColor = Color.white;
            transparentButtonOffsetGUIStyle.focused.textColor = Color.white;
        }

        private static void InitButtonGUIStyle()
        {
            buttonGUIStyle = new GUIStyle(GUI.skin.button);
            buttonGUIStyle.margin = new RectOffset(0, 0, 2, 2);
            buttonGUIStyle.padding = new RectOffset(0, 0, 1, 1);
        }

        private static void InitPlainTextureGUIStyle()
        {
            plainTextureGUIStyle = new GUIStyle();
            plainTextureGUIStyle.border = new RectOffset(0, 0, 0, 0);
            plainTextureGUIStyle.margin = new RectOffset(0, 0, 0, 0);
            plainTextureGUIStyle.padding = new RectOffset(0, 0, 0, 0);
            plainTextureGUIStyle.normal.background = null;
            plainTextureGUIStyle.active.background = null;
            plainTextureGUIStyle.hover.background = null;
            plainTextureGUIStyle.focused.background = null;
        }

        private static void InitSelectedBackgroundGUIStyle()
        {
            Texture2D texture2D = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            Color color = ((!EditorGUIUtility.isProSkin) ? new Color(0.243f, 0.5686f, 0.839f, 0.5f) : new Color(0.188f, 0.4588f, 0.6862f, 0.5f));
            texture2D.SetPixel(1, 1, color);
            texture2D.hideFlags = HideFlags.HideAndDontSave;
            texture2D.Apply();
            selectedBackgroundGUIStyle = new GUIStyle();
            selectedBackgroundGUIStyle.border = new RectOffset(0, 0, 0, 0);
            selectedBackgroundGUIStyle.margin = new RectOffset(0, 0, -2, 2);
            selectedBackgroundGUIStyle.normal.background = texture2D;
            selectedBackgroundGUIStyle.active.background = texture2D;
            selectedBackgroundGUIStyle.hover.background = texture2D;
            selectedBackgroundGUIStyle.focused.background = texture2D;
        }

        private static void InitErrorListDarkBackground()
        {
            Texture2D texture2D = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            Color color = ((!EditorGUIUtility.isProSkin) ? new Color(0.706f, 0.706f, 0.706f) : new Color(0.2f, 0.2f, 0.2f, 1f));
            texture2D.SetPixel(1, 1, color);
            texture2D.hideFlags = HideFlags.HideAndDontSave;
            texture2D.Apply();
            errorListDarkBackground = new GUIStyle();
            errorListDarkBackground.padding = new RectOffset(2, 0, 2, 0);
            errorListDarkBackground.normal.background = texture2D;
            errorListDarkBackground.active.background = texture2D;
            errorListDarkBackground.hover.background = texture2D;
            errorListDarkBackground.focused.background = texture2D;
            errorListDarkBackground.normal.textColor = ((!EditorGUIUtility.isProSkin) ? new Color(0.206f, 0.206f, 0.206f) : new Color(0.706f, 0.706f, 0.706f));
            errorListDarkBackground.alignment = TextAnchor.UpperLeft;
            errorListDarkBackground.wordWrap = true;
        }

        private static void InitErrorListLightBackground()
        {
            errorListLightBackground = new GUIStyle();
            errorListLightBackground.padding = new RectOffset(2, 0, 2, 0);
            errorListLightBackground.normal.textColor = ((!EditorGUIUtility.isProSkin) ? new Color(0.106f, 0.106f, 0.106f) : new Color(0.706f, 0.706f, 0.706f));
            errorListLightBackground.alignment = TextAnchor.UpperLeft;
            errorListLightBackground.wordWrap = true;
        }

        private static void InitWelcomeScreenIntroGUIStyle()
        {
            welcomeScreenIntroGUIStyle = new GUIStyle(GUI.skin.label);
            welcomeScreenIntroGUIStyle.fontSize = 16;
            welcomeScreenIntroGUIStyle.fontStyle = FontStyle.Bold;
            welcomeScreenIntroGUIStyle.normal.textColor = new Color(0.706f, 0.706f, 0.706f);
        }

        private static void InitWelcomeScreenTextHeaderGUIStyle()
        {
            welcomeScreenTextHeaderGUIStyle = new GUIStyle(GUI.skin.label);
            welcomeScreenTextHeaderGUIStyle.alignment = TextAnchor.MiddleLeft;
            welcomeScreenTextHeaderGUIStyle.fontSize = 14;
            welcomeScreenTextHeaderGUIStyle.fontStyle = FontStyle.Bold;
        }

        private static void InitWelcomeScreenTextDescriptionGUIStyle()
        {
            welcomeScreenTextDescriptionGUIStyle = new GUIStyle(GUI.skin.label);
            welcomeScreenTextDescriptionGUIStyle.wordWrap = true;
        }

        private static void InitTaskBorderTexture(int colorIndex)
        {
            taskBorderTexture[colorIndex] = LoadTaskTexture("TaskBorder" + ColorIndexToColorString(colorIndex) + ".png");
        }

        private static void InitTaskBorderRunningTexture()
        {
            taskBorderRunningTexture = LoadTaskTexture("TaskBorderRunning.png");
        }

        private static void InitTaskBorderIdentifyTexture()
        {
            taskBorderIdentifyTexture = LoadTaskTexture("TaskBorderIdentify.png");
        }

        private static void InitTaskConnectionTopTexture(int colorIndex)
        {
            taskConnectionTopTexture[colorIndex] = LoadTaskTexture("TaskConnectionTop" + ColorIndexToColorString(colorIndex) + ".png");
        }

        private static void InitTaskConnectionBottomTexture(int colorIndex)
        {
            taskConnectionBottomTexture[colorIndex] = LoadTaskTexture("TaskConnectionBottom" + ColorIndexToColorString(colorIndex) + ".png");
        }

        private static void InitTaskConnectionRunningTopTexture()
        {
            taskConnectionRunningTopTexture = LoadTaskTexture("TaskConnectionRunningTop.png");
        }

        private static void InitTaskConnectionRunningBottomTexture()
        {
            taskConnectionRunningBottomTexture = LoadTaskTexture("TaskConnectionRunningBottom.png");
        }

        private static void InitTaskConnectionIdentifyTopTexture()
        {
            taskConnectionIdentifyTopTexture = LoadTaskTexture("TaskConnectionIdentifyTop.png");
        }

        private static void InitTaskConnectionIdentifyBottomTexture()
        {
            taskConnectionIdentifyBottomTexture = LoadTaskTexture("TaskConnectionIdentifyBottom.png");
        }

        private static void InitTaskConnectionCollapsedTexture()
        {
            taskConnectionCollapsedTexture = LoadTaskTexture("TaskConnectionCollapsed.png");
        }

        private static void InitContentSeparatorTexture()
        {
            contentSeparatorTexture = LoadTexture("ContentSeparator.png");
        }

        private static void InitDocTexture()
        {
            docTexture = LoadTexture("DocIcon.png");
        }

        private static void InitGearTexture()
        {
            gearTexture = LoadTexture("GearIcon.png");
        }

        private static void InitColorSelectorTexture(int colorIndex)
        {
            colorSelectorTexture[colorIndex] = LoadTexture("ColorSelector" + ColorIndexToColorString(colorIndex) + ".png");
        }

        private static void InitVariableButtonTexture()
        {
            variableButtonTexture = LoadTexture("VariableButton.png");
        }

        private static void InitVariableButtonSelectedTexture()
        {
            variableButtonSelectedTexture = LoadTexture("VariableButtonSelected.png");
        }

        private static void InitVariableWatchButtonTexture()
        {
            variableWatchButtonTexture = LoadTexture("VariableWatchButton.png");
        }

        private static void InitVariableWatchButtonSelectedTexture()
        {
            variableWatchButtonSelectedTexture = LoadTexture("VariableWatchButtonSelected.png");
        }

        private static void InitReferencedTexture()
        {
            referencedTexture = LoadTexture("LinkedIcon.png");
        }

        private static void InitConditionalAbortSelfTexture()
        {
            conditionalAbortSelfTexture = LoadTexture("ConditionalAbortSelfIcon.png");
        }

        private static void InitConditionalAbortLowerPriorityTexture()
        {
            conditionalAbortLowerPriorityTexture = LoadTexture("ConditionalAbortLowerPriorityIcon.png");
        }

        private static void InitConditionalAbortBothTexture()
        {
            conditionalAbortBothTexture = LoadTexture("ConditionalAbortBothIcon.png");
        }

        private static void InitDeleteButtonTexture()
        {
            deleteButtonTexture = LoadTexture("DeleteButton.png");
        }

        private static void InitVariableDeleteButtonTexture()
        {
            variableDeleteButtonTexture = LoadTexture("VariableDeleteButton.png");
        }

        private static void InitDownArrowButtonTexture()
        {
            downArrowButtonTexture = LoadTexture("DownArrowButton.png");
        }

        private static void InitUpArrowButtonTexture()
        {
            upArrowButtonTexture = LoadTexture("UpArrowButton.png");
        }

        private static void InitVariableMapButtonTexture()
        {
            variableMapButtonTexture = LoadTexture("VariableMapButton.png");
        }

        private static void InitIdentifyButtonTexture()
        {
            identifyButtonTexture = LoadTexture("IdentifyButton.png");
        }

        private static void InitBreakpointTexture()
        {
            breakpointTexture = LoadTexture("BreakpointIcon.png", useSkinColor: false);
        }

        private static void InitErrorIconTexture()
        {
            errorIconTexture = LoadTexture("ErrorIcon.png");
        }

        private static void InitSmallErrorIconTexture()
        {
            smallErrorIconTexture = LoadTexture("SmallErrorIcon.png");
        }

        private static void InitEnableTaskTexture()
        {
            enableTaskTexture = LoadTexture("TaskEnableIcon.png", useSkinColor: false);
        }

        private static void InitDisableTaskTexture()
        {
            disableTaskTexture = LoadTexture("TaskDisableIcon.png", useSkinColor: false);
        }

        private static void InitExpandTaskTexture()
        {
            expandTaskTexture = LoadTexture("TaskExpandIcon.png", useSkinColor: false);
        }

        private static void InitCollapseTaskTexture()
        {
            collapseTaskTexture = LoadTexture("TaskCollapseIcon.png", useSkinColor: false);
        }

        private static void InitExecutionSuccessTexture()
        {
            executionSuccessTexture = LoadTexture("ExecutionSuccess.png", useSkinColor: false);
        }

        private static void InitExecutionFailureTexture()
        {
            executionFailureTexture = LoadTexture("ExecutionFailure.png", useSkinColor: false);
        }

        private static void InitExecutionSuccessRepeatTexture()
        {
            executionSuccessRepeatTexture = LoadTexture("ExecutionSuccessRepeat.png", useSkinColor: false);
        }

        private static void InitExecutionFailureRepeatTexture()
        {
            executionFailureRepeatTexture = LoadTexture("ExecutionFailureRepeat.png", useSkinColor: false);
        }

        private static void InitHistoryBackwardTexture()
        {
            historyBackwardTexture = LoadTexture("HistoryBackward.png");
        }

        private static void InitHistoryForwardTexture()
        {
            historyForwardTexture = LoadTexture("HistoryForward.png");
        }

        private static void InitPlayTexture()
        {
            playTexture = LoadTexture("Play.png");
        }

        private static void InitPauseTexture()
        {
            pauseTexture = LoadTexture("Pause.png");
        }

        private static void InitStepTexture()
        {
            stepTexture = LoadTexture("Step.png");
        }

        private static void InitScreenshotBackgroundTexture()
        {
            screenshotBackgroundTexture = new Texture2D(1, 1, TextureFormat.RGB24, mipChain: false);
            if (EditorGUIUtility.isProSkin)
            {
                screenshotBackgroundTexture.SetPixel(1, 1, new Color(0.1647f, 0.1647f, 0.1647f));
            }
            else
            {
                screenshotBackgroundTexture.SetPixel(1, 1, new Color(0.3647f, 0.3647f, 0.3647f));
            }
            screenshotBackgroundTexture.Apply();
        }

        public static void SetObjectDirty(UnityEngine.Object obj)
        {
            EditorUtility.SetDirty(obj);
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
            if (!EditorApplication.isPlaying && !EditorUtility.IsPersistent(obj))
            {
                if (obj is Component)
                {
                    EditorSceneManager.MarkSceneDirty((obj as Component).gameObject.scene);
                }
                else if (obj is GameObject)
                {
                    EditorSceneManager.MarkSceneDirty((obj as GameObject).scene);
                }
                else if (!EditorUtility.IsPersistent(obj))
                {
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }
            }
        }
    }
}