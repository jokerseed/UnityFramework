using Framework.Res;

namespace Game
{
    /// <summary>示例工程的资源寻址；通用根路径与拼接规则见 <see cref="ResourceAddresses"/>。</summary>
    public static class GameAddresses
    {
        /// <summary>首页 Prefab（Assets/Bundles/Prefabs/UI/Main.prefab）。</summary>
        public const string MainPrefab = "bundles/prefabs/ui/main.unity3d";

        /// <summary>启动场景（Assets/Bundles/Scenes/Launch.unity）。</summary>
        public const string LaunchScene = "bundles/scenes/launch.unity3d";

        /// <summary>战斗场景（Assets/Bundles/Scenes/Battle.unity）。</summary>
        public const string BattleScene = "bundles/scenes/battle.unity3d";

        /// <summary>男剑士模型 Prefab（Assets/Bundles/Prefabs/Model/Male_Sword_01.prefab）。</summary>
        public const string MaleSword01Prefab = "bundles/prefabs/model/male_sword_01.unity3d";

        /// <summary>斧骑士模型 Prefab（Assets/Bundles/Prefabs/Model/AxeKnight.prefab）。</summary>
        public const string AxeKnightPrefab = "bundles/prefabs/model/axeknight.unity3d";
    }
}
