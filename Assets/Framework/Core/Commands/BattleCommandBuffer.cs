using System.Collections.Generic;

namespace Framework.Core.Commands
{
    /// <summary>
    /// 每 Tick 批量刷新的命令缓冲。模拟热路径不走 EventBus，保证顺序与零订阅开销。
    /// </summary>
    public sealed class BattleCommandBuffer
    {
        readonly List<SpawnProjectileCommand> _spawnProjectiles = new List<SpawnProjectileCommand>(32);
        readonly List<ApplyDamageCommand> _applyDamage = new List<ApplyDamageCommand>(32);

        /// <summary>获取当前帧待处理的投射物生成命令列表（只读）。</summary>
        public IReadOnlyList<SpawnProjectileCommand> SpawnProjectiles => _spawnProjectiles;

        /// <summary>获取当前帧待处理的伤害应用命令列表（只读）。</summary>
        public IReadOnlyList<ApplyDamageCommand> ApplyDamage => _applyDamage;

        /// <summary>将一条投射物生成命令加入缓冲。</summary>
        /// <param name="command">要入队的命令（通过 in 传递，避免拷贝）。</param>
        public void EnqueueSpawnProjectile(in SpawnProjectileCommand command) => _spawnProjectiles.Add(command);

        /// <summary>将一条伤害应用命令加入缓冲。</summary>
        /// <param name="command">要入队的命令（通过 in 传递，避免拷贝）。</param>
        public void EnqueueApplyDamage(in ApplyDamageCommand command) => _applyDamage.Add(command);

        /// <summary>清空投射物生成命令缓冲；通常在 Flush Spawn 阶段结束后调用。</summary>
        public void ClearSpawnProjectiles() => _spawnProjectiles.Clear();

        /// <summary>清空伤害应用命令缓冲；通常在 Flush Damage 阶段结束后调用。</summary>
        public void ClearApplyDamage() => _applyDamage.Clear();

        /// <summary>清空所有命令缓冲。</summary>
        public void ClearAll()
        {
            ClearSpawnProjectiles();
            ClearApplyDamage();
        }
    }
}
