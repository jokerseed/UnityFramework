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

        public IReadOnlyList<SpawnProjectileCommand> SpawnProjectiles => _spawnProjectiles;
        public IReadOnlyList<ApplyDamageCommand> ApplyDamage => _applyDamage;

        public void EnqueueSpawnProjectile(in SpawnProjectileCommand command) => _spawnProjectiles.Add(command);

        public void EnqueueApplyDamage(in ApplyDamageCommand command) => _applyDamage.Add(command);

        public void ClearSpawnProjectiles() => _spawnProjectiles.Clear();

        public void ClearApplyDamage() => _applyDamage.Clear();

        public void ClearAll()
        {
            ClearSpawnProjectiles();
            ClearApplyDamage();
        }
    }
}
