using System;
using BehaviorDesigner.Runtime;
using UnityEditor;

namespace BehaviorDesigner.Editor
{
    public class AssetCreationMenus
    {
        [MenuItem("Assets/Create/Behavior Designer/C# Action Task")]
        public static void CreateCSharpActionTask()
        {
            AssetCreator.ShowWindow(AssetCreator.AssetClassType.Action);
        }

        [MenuItem("Assets/Create/Behavior Designer/C# Conditional Task")]
        public static void CreateCSharpConditionalTask()
        {
            AssetCreator.ShowWindow(AssetCreator.AssetClassType.Conditional);
        }

        [MenuItem("Assets/Create/Behavior Designer/Shared Variable")]
        public static void CreateSharedVariable()
        {
            AssetCreator.ShowWindow(AssetCreator.AssetClassType.SharedVariable);
        }

        [MenuItem("Assets/Create/Behavior Designer/External Behavior Tree")]
        public static void CreateExternalBehaviorTree()
        {
            Type typeWithinAssembly = TaskUtility.GetTypeWithinAssembly("BehaviorDesigner.Runtime.ExternalBehaviorTree");
            AssetCreator.CreateAsset(typeWithinAssembly, "NewExternalBehavior");
        }
    }
}