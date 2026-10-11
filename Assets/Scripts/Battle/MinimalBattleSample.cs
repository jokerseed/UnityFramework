using Framework.GamePlay;
using Framework.Logging;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 无视图的最小战斗样本。挂到空场景的任意物体上，进入 Play 后调用 <see cref="MinimalBattle.Run"/>。
    /// 不加载 Luban，不创建角色模型。结果写到日志：步数、末帧校验和、双方生命原值。
    /// </summary>
    public sealed class MinimalBattleSample : MonoBehaviour
    {
        /// <summary>逻辑步数。</summary>
        public int steps = MinimalBattle.DefaultSteps;

        /// <summary>随机种子。</summary>
        public int seed = MinimalBattle.Seed;

        void Start()
        {
            var report = MinimalBattle.Run(steps, seed);
            var last = report.Checksums.Length == 0 ? 0 : report.Checksums[report.Checksums.Length - 1];
            GameLog.Info(
                LogCategories.GamePlay,
                "MinimalBattle steps=" + report.Checksums.Length +
                " lastChecksum=" + last +
                " hero=" + report.HeroHealthRaw + "/" + report.HeroHealthAtStartRaw +
                " enemy=" + report.EnemyHealthRaw + "/" + report.EnemyHealthAtStartRaw);
        }
    }
}
