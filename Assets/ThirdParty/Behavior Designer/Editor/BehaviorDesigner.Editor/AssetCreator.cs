using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    public class AssetCreator : EditorWindow
    {
        public enum AssetClassType
        {
            Action,
            Conditional,
            SharedVariable
        }

        private AssetClassType m_ClassType;

        private string m_AssetName;

        private AssetClassType ClassType
        {
            set
            {
                m_ClassType = value;
                switch (m_ClassType)
                {
                    case AssetClassType.Action:
                        m_AssetName = "NewAction";
                        break;
                    case AssetClassType.Conditional:
                        m_AssetName = "NewConditional";
                        break;
                    case AssetClassType.SharedVariable:
                        m_AssetName = "SharedNewVariable";
                        break;
                }
            }
        }

        public static void ShowWindow(AssetClassType classType)
        {
            AssetCreator window = EditorWindow.GetWindow<AssetCreator>(utility: true, "Asset Name");
            Vector2 vector2 = (window.maxSize = new Vector2(300f, 55f));
            window.minSize = vector2;
            window.ClassType = classType;
        }

        private void OnGUI()
        {
            m_AssetName = EditorGUILayout.TextField("Name", m_AssetName);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("OK"))
            {
                CreateScript(m_AssetName, m_ClassType);
                Close();
            }
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }

        public static void CreateAsset(Type type, string name)
        {
            ScriptableObject asset = ScriptableObject.CreateInstance(type);
            string text = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (text == string.Empty)
            {
                text = "Assets";
            }
            else if (Path.GetExtension(text) != string.Empty)
            {
                text = text.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), string.Empty);
            }
            string path = AssetDatabase.GenerateUniqueAssetPath(text + "/" + name + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
        }

        private static void CreateScript(string name, AssetClassType classType)
        {
            string text = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (text == string.Empty)
            {
                text = "Assets";
            }
            else if (Path.GetExtension(text) != string.Empty)
            {
                text = text.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), string.Empty);
            }
            string path = AssetDatabase.GenerateUniqueAssetPath(text + "/" + name + ".cs");
            StreamWriter streamWriter = new StreamWriter(path, append: false);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            string value = string.Empty;
            switch (classType)
            {
                case AssetClassType.Action:
                    value = ActionTaskContents(fileNameWithoutExtension);
                    break;
                case AssetClassType.Conditional:
                    value = ConditionalTaskContents(fileNameWithoutExtension);
                    break;
                case AssetClassType.SharedVariable:
                    value = SharedVariableContents(fileNameWithoutExtension);
                    break;
            }
            streamWriter.Write(value);
            streamWriter.Close();
            AssetDatabase.Refresh();
        }

        private static string ActionTaskContents(string name)
        {
            return "using UnityEngine;\nusing BehaviorDesigner.Runtime;\nusing BehaviorDesigner.Runtime.Tasks;\n\npublic class " + name + " : Action\n{\n\tpublic override void OnStart()\n\t{\n\t\t\n\t}\n\n\tpublic override TaskStatus OnUpdate()\n\t{\n\t\treturn TaskStatus.Success;\n\t}\n}";
        }

        private static string ConditionalTaskContents(string name)
        {
            return "using UnityEngine;\nusing BehaviorDesigner.Runtime;\nusing BehaviorDesigner.Runtime.Tasks;\n\npublic class " + name + " : Conditional\n{\n\tpublic override TaskStatus OnUpdate()\n\t{\n\t\treturn TaskStatus.Success;\n\t}\n}";
        }

        private static string SharedVariableContents(string name)
        {
            string text = name.Remove(0, 6);
            return "using UnityEngine;\nusing BehaviorDesigner.Runtime;\n\n[System.Serializable]\npublic class " + text + "\n{\n\n}\n\n[System.Serializable]\npublic class " + name + " : SharedVariable<" + text + ">\n{\n\tpublic override string ToString() { return mValue == null ? \"null\" : mValue.ToString(); }\n\tpublic static implicit operator " + name + "(" + text + " value) { return new " + name + " { mValue = value }; }\n}";
        }
    }
}