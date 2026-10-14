using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    public class WelcomeScreen : EditorWindow
    {
        private Texture m_WelcomeScreenImage;

        private Texture m_SamplesImage;

        private Texture m_DocImage;

        private Texture m_VideoImage;

        private Texture m_ForumImage;

        private Texture m_ContactImage;

        private Rect m_WelcomeScreenImageRect = new Rect(0f, 0f, 340f, 44f);

        private Rect m_WelcomeIntroRect = new Rect(46f, 12f, 306f, 40f);

        private Rect m_SamplesImageRect = new Rect(15f, 58f, 50f, 50f);

        private Rect m_DocImageRect = new Rect(15f, 124f, 53f, 50f);

        private Rect m_VideoImageRect = new Rect(15f, 190f, 50f, 50f);

        private Rect m_ForumImageRect = new Rect(15f, 256f, 50f, 50f);

        private Rect m_ContactImageRect = new Rect(15f, 322f, 50f, 50f);

        private Rect m_VersionRect = new Rect(5f, 385f, 125f, 20f);

        private Rect m_ToggleButtonRect = new Rect(220f, 385f, 125f, 20f);

        private Rect m_SamplesHeaderRect = new Rect(70f, 57f, 250f, 20f);

        private Rect m_DocHeaderRect = new Rect(70f, 123f, 250f, 20f);

        private Rect m_VideoHeaderRect = new Rect(70f, 189f, 250f, 20f);

        private Rect m_ForumHeaderRect = new Rect(70f, 258f, 250f, 20f);

        private Rect m_ContactHeaderRect = new Rect(70f, 324f, 250f, 20f);

        private Rect m_SamplesDescriptionRect = new Rect(70f, 77f, 250f, 30f);

        private Rect m_DocDescriptionRect = new Rect(70f, 143f, 250f, 30f);

        private Rect m_VideoDescriptionRect = new Rect(70f, 209f, 250f, 30f);

        private Rect m_ForumDescriptionRect = new Rect(70f, 278f, 250f, 30f);

        private Rect m_ContactDescriptionRect = new Rect(70f, 344f, 250f, 30f);

        [MenuItem("Tools/Behavior Designer/Welcome Screen", false, 3)]
        public static void ShowWindow()
        {
            WelcomeScreen window = EditorWindow.GetWindow<WelcomeScreen>(utility: true, "Welcome to Behavior Designer");
            Vector2 vector2 = (window.maxSize = new Vector2(340f, 410f));
            window.minSize = vector2;
        }

        public void OnEnable()
        {
            m_WelcomeScreenImage = BehaviorDesignerUtility.LoadTexture("WelcomeScreenHeader.png", useSkinColor: false, this);
            m_SamplesImage = BehaviorDesignerUtility.LoadIcon("WelcomeScreenSamplesIcon.png", this);
            m_DocImage = BehaviorDesignerUtility.LoadIcon("WelcomeScreenDocumentationIcon.png", this);
            m_VideoImage = BehaviorDesignerUtility.LoadIcon("WelcomeScreenVideosIcon.png", this);
            m_ForumImage = BehaviorDesignerUtility.LoadIcon("WelcomeScreenForumIcon.png", this);
            m_ContactImage = BehaviorDesignerUtility.LoadIcon("WelcomeScreenContactIcon.png", this);
        }

        public void OnGUI()
        {
            GUI.DrawTexture(m_WelcomeScreenImageRect, m_WelcomeScreenImage);
            GUI.Label(m_WelcomeIntroRect, "Welcome To Behavior Designer", BehaviorDesignerUtility.WelcomeScreenIntroGUIStyle);
            GUI.DrawTexture(m_SamplesImageRect, m_SamplesImage);
            GUI.Label(m_SamplesHeaderRect, "Samples", BehaviorDesignerUtility.WelcomeScreenTextHeaderGUIStyle);
            GUI.Label(m_SamplesDescriptionRect, "Download sample projects to get a feel for Behavior Designer.", BehaviorDesignerUtility.WelcomeScreenTextDescriptionGUIStyle);
            GUI.DrawTexture(m_DocImageRect, m_DocImage);
            GUI.Label(m_DocHeaderRect, "Documentation", BehaviorDesignerUtility.WelcomeScreenTextHeaderGUIStyle);
            GUI.Label(m_DocDescriptionRect, "Browser our extensive online documentation.", BehaviorDesignerUtility.WelcomeScreenTextDescriptionGUIStyle);
            GUI.DrawTexture(m_VideoImageRect, m_VideoImage);
            GUI.Label(m_VideoHeaderRect, "Videos", BehaviorDesignerUtility.WelcomeScreenTextHeaderGUIStyle);
            GUI.Label(m_VideoDescriptionRect, "Watch our tutorial videos which cover a wide variety of topics.", BehaviorDesignerUtility.WelcomeScreenTextDescriptionGUIStyle);
            GUI.DrawTexture(m_ForumImageRect, m_ForumImage);
            GUI.Label(m_ForumHeaderRect, "Forums", BehaviorDesignerUtility.WelcomeScreenTextHeaderGUIStyle);
            GUI.Label(m_ForumDescriptionRect, "Join the forums!", BehaviorDesignerUtility.WelcomeScreenTextDescriptionGUIStyle);
            GUI.DrawTexture(m_ContactImageRect, m_ContactImage);
            GUI.Label(m_ContactHeaderRect, "Contact", BehaviorDesignerUtility.WelcomeScreenTextHeaderGUIStyle);
            GUI.Label(m_ContactDescriptionRect, "We are here to help.", BehaviorDesignerUtility.WelcomeScreenTextDescriptionGUIStyle);
            GUI.Label(m_VersionRect, "Version 1.7.4");
            bool flag = GUI.Toggle(m_ToggleButtonRect, BehaviorDesignerPreferences.GetBool(BDPreferences.ShowWelcomeScreen), "Show at Startup");
            if (flag != BehaviorDesignerPreferences.GetBool(BDPreferences.ShowWelcomeScreen))
            {
                BehaviorDesignerPreferences.SetBool(BDPreferences.ShowWelcomeScreen, flag);
            }
            EditorGUIUtility.AddCursorRect(m_SamplesImageRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_SamplesHeaderRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_SamplesDescriptionRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_DocImageRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_DocHeaderRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_DocDescriptionRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_VideoImageRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_VideoHeaderRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_VideoDescriptionRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ForumImageRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ForumHeaderRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ForumDescriptionRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ContactImageRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ContactHeaderRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ContactDescriptionRect, MouseCursor.Link);
            if (Event.current.type == EventType.MouseUp)
            {
                Vector2 mousePosition = Event.current.mousePosition;
                if (m_SamplesImageRect.Contains(mousePosition) || m_SamplesHeaderRect.Contains(mousePosition) || m_SamplesDescriptionRect.Contains(mousePosition))
                {
                    Application.OpenURL("https://opsive.com/downloads/?pid=803");
                }
                else if (m_DocImageRect.Contains(mousePosition) || m_DocHeaderRect.Contains(mousePosition) || m_DocDescriptionRect.Contains(mousePosition))
                {
                    Application.OpenURL("https://opsive.com/support/documentation/behavior-designer");
                }
                else if (m_VideoImageRect.Contains(mousePosition) || m_VideoHeaderRect.Contains(mousePosition) || m_VideoDescriptionRect.Contains(mousePosition))
                {
                    Application.OpenURL("https://opsive.com/videos/?pid=803");
                }
                else if (m_ForumImageRect.Contains(mousePosition) || m_ForumHeaderRect.Contains(mousePosition) || m_ForumDescriptionRect.Contains(mousePosition))
                {
                    Application.OpenURL("https://opsive.com/forum");
                }
                else if (m_ContactImageRect.Contains(mousePosition) || m_ContactHeaderRect.Contains(mousePosition) || m_ContactDescriptionRect.Contains(mousePosition))
                {
                    Application.OpenURL("https://opsive.com/support/");
                }
            }
        }
    }
}