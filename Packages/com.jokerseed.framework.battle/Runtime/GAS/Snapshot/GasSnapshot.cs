using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Snapshot;
using Framework.GAS.Abilities;
using Framework.GAS.Effects;

namespace Framework.GAS.Snapshot
{
    /// <summary>
    /// 一场战斗里全部 ASC 的快照。
    /// 字节流包含：属性基础值与当前值、修改器汇总、标签计数、活跃效果（剩余时间、周期、层数、冷却）、
    /// 已授予技能的句柄与 <c>_nextSpecHandle</c>、激活实例及其内置任务进度、护盾加成。
    /// 技能定义上的工厂委托不进字节流，保存在本对象里；必须用同一次 <see cref="Save"/> 的实例来 <see cref="Restore"/>。
    /// 自定义技能若在激活期间挂了任务，需要重写 <see cref="GameplayAbility.SaveActiveTasks"/>。
    /// 未知的 GameplayEffect Execution 会在保存时抛出，避免恢复后少跳周期伤害。
    /// </summary>
    public sealed class GasSnapshot : ISnapshotable
    {
        /// <summary>快照魔数，小端字节为 GAS1。</summary>
        public const int Magic = 0x31534147;

        /// <summary>
        /// 当前快照版本。版本 4 去掉了头部三个进程级计数（Spec RuntimeId、效果句柄、技能实例 ID），
        /// 这些计数只按 ASC 保存。版本 3 及更早的字节不能再读。
        /// </summary>
        public const int Version = 4;

        readonly IList<AbilitySystemComponent> _components;
        readonly BattleContext _battle;
        readonly Dictionary<string, GameplayAbilityDef> _abilityDefs = new Dictionary<string, GameplayAbilityDef>();
        readonly List<AbilitySystemComponent> _sorted = new List<AbilitySystemComponent>();

        /// <summary>绑定要一起保存的 ASC。列表在 Restore 时仍须包含这些实例。</summary>
        /// <param name="components">ASC 列表；不可为 null，元素不可为 null。</param>
        /// <param name="battle">恢复进行中技能时使用的战斗上下文；不可为 null。</param>
        /// <exception cref="ArgumentNullException">参数为 null。</exception>
        public GasSnapshot(IList<AbilitySystemComponent> components, BattleContext battle)
        {
            _components = components ?? throw new ArgumentNullException(nameof(components));
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">列表含 null，或同一技能 ID 对应两份定义。</exception>
        /// <exception cref="NotSupportedException">有技能任务或 Execution 尚不能保存。</exception>
        public void Save(SnapshotWriter writer)
        {
            _abilityDefs.Clear();
            SortComponents();
            writer.WriteInt(Magic);
            writer.WriteInt(Version);
            writer.WriteInt(_sorted.Count);
            for (var i = 0; i < _sorted.Count; i++)
            {
                writer.WriteUInt(_sorted[i].ActorId.Value);
                _sorted[i].SaveState(writer, _abilityDefs);
            }
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">魔数、版本、人数或修改器汇总不匹配。</exception>
        public void Restore(SnapshotReader reader)
        {
            var magic = reader.ReadInt();
            if (magic != Magic)
            {
                throw new InvalidOperationException("GAS snapshot magic mismatch.");
            }

            var version = reader.ReadInt();
            if (version != Version)
            {
                throw new InvalidOperationException("Unsupported GAS snapshot version " + version + ".");
            }

            var count = reader.ReadInt();
            if (count != _components.Count)
            {
                throw new InvalidOperationException(
                    "GAS snapshot actor count mismatch. snapshot=" + count + " bound=" + _components.Count);
            }

            for (var i = 0; i < count; i++)
            {
                var actorId = reader.ReadUInt();
                var asc = Find(actorId);
                if (asc == null)
                {
                    throw new InvalidOperationException("GAS snapshot actor " + actorId + " is not in the bound list.");
                }

                asc.RestoreState(reader, _abilityDefs, _battle, FindAsc);
            }
        }

        void SortComponents()
        {
            _sorted.Clear();
            for (var i = 0; i < _components.Count; i++)
            {
                var asc = _components[i];
                if (asc == null)
                {
                    throw new InvalidOperationException("GAS snapshot component list contains null.");
                }

                _sorted.Add(asc);
            }

            _sorted.Sort(CompareActor);
        }

        AbilitySystemComponent Find(uint actorId)
        {
            for (var i = 0; i < _components.Count; i++)
            {
                var asc = _components[i];
                if (asc != null && asc.ActorId.Value == actorId)
                {
                    return asc;
                }
            }

            return null;
        }

        AbilitySystemComponent FindAsc(ActorId actorId) => Find(actorId.Value);

        static int CompareActor(AbilitySystemComponent left, AbilitySystemComponent right) =>
            left.ActorId.Value.CompareTo(right.ActorId.Value);
    }
}
