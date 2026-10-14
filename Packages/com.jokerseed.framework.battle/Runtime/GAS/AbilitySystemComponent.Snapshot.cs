using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Snapshot;
using Framework.FixedMath;
using Framework.GAS.Abilities;
using Framework.GAS.Attributes;
using Framework.GAS.Effects;
using Framework.GAS.Tags;

namespace Framework.GAS
{
    public sealed partial class AbilitySystemComponent
    {
        const int MaxEffectDefDepth = 8;

        /// <summary>
        /// 写入本 ASC 的可回滚状态。技能定义对象记入 <paramref name="abilityDefs"/>，不进字节流。
        /// </summary>
        /// <param name="writer">写入器。</param>
        /// <param name="abilityDefs">技能 ID 到定义的目录；不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="abilityDefs"/> 为 null。</exception>
        internal void SaveState(SnapshotWriter writer, Dictionary<string, GameplayAbilityDef> abilityDefs)
        {
            if (abilityDefs == null)
            {
                throw new ArgumentNullException(nameof(abilityDefs));
            }

            writer.WriteInt(_nextSpecHandle);
            writer.WriteInt(_nextActiveInstanceId);
            writer.WriteInt(_nextActiveEffectHandle);
            writer.WriteInt(_nextEffectRuntimeId);
            writer.WriteFp(_appliedShieldBonus);
            WriteAttributes(writer);
            WriteTagCounts(writer);
            WriteGrantedSpecs(writer, abilityDefs);
            WriteActiveEffects(writer, abilityDefs);
            WriteActiveInstances(writer);

            var modifiers = new AttributeModifierAggregator();
            AccumulateModifiers(modifiers);
            modifiers.WriteTo(writer);
        }

        /// <summary>
        /// 用字节流覆盖本 ASC 的模拟状态，并按活跃效果重算修改器。
        /// 不重新执行 Activate，也不重放施加时的 Cue。
        /// </summary>
        /// <param name="reader">读取器。</param>
        /// <param name="abilityDefs">同一次 <see cref="SaveState"/> 填入的技能目录。</param>
        /// <param name="battle">恢复进行中技能任务时使用的战斗上下文。</param>
        /// <param name="ascLookup">按 ActorId 找回来源 ASC；不可为 null。</param>
        /// <exception cref="ArgumentNullException">目录或查找函数为 null。</exception>
        /// <exception cref="InvalidOperationException">Actor、技能定义或修改器汇总不匹配。</exception>
        internal void RestoreState(
            SnapshotReader reader,
            Dictionary<string, GameplayAbilityDef> abilityDefs,
            BattleContext battle,
            Func<ActorId, AbilitySystemComponent> ascLookup)
        {
            if (abilityDefs == null)
            {
                throw new ArgumentNullException(nameof(abilityDefs));
            }

            if (ascLookup == null)
            {
                throw new ArgumentNullException(nameof(ascLookup));
            }

            ClearRuntimeForSnapshot();
            _nextSpecHandle = reader.ReadInt();
            _nextActiveInstanceId = reader.ReadInt();
            _nextActiveEffectHandle = reader.ReadInt();
            _nextEffectRuntimeId = reader.ReadInt();
            _appliedShieldBonus = reader.ReadFp();
            ReadAttributes(reader);
            ReadTagCounts(reader);
            ReadGrantedSpecs(reader, abilityDefs);
            ReadActiveEffects(reader, abilityDefs, ascLookup);
            ReadActiveInstances(reader, battle);
            RecalculateAttributes(NullEventBus.Instance);
            _aggregator.ReadExpected(reader);
        }

        void ClearRuntimeForSnapshot()
        {
            for (var i = 0; i < _activeInstances.Count; i++)
            {
                _activeInstances[i].ClearTasks();
            }

            _activeInstances.Clear();
            _activeEffects.Clear();
            _grantedSpecs.Clear();
            _specsByAbilityId.Clear();
        }

        void WriteAttributes(SnapshotWriter writer)
        {
            var names = new List<string>();
            foreach (var pair in Attributes.GetAllAttributes())
            {
                names.Add(pair.Key);
            }

            names.Sort(CompareString);
            writer.WriteInt(names.Count);
            for (var i = 0; i < names.Count; i++)
            {
                var attribute = Attributes.GetOrCreate(names[i]);
                writer.WriteString(names[i]);
                writer.WriteFp(attribute.BaseValue);
                writer.WriteFp(attribute.CurrentValue);
            }
        }

        void ReadAttributes(SnapshotReader reader)
        {
            Attributes.Clear();
            var count = reader.ReadInt();
            for (var i = 0; i < count; i++)
            {
                var name = reader.ReadString();
                var baseValue = reader.ReadFp();
                var current = reader.ReadFp();
                Attributes.GetOrCreate(name).RestoreValues(baseValue, current);
            }
        }

        void WriteTagCounts(SnapshotWriter writer)
        {
            var tags = new List<KeyValuePair<string, int>>();
            Tags.CopyCounts(tags);
            tags.Sort(CompareTagCount);
            writer.WriteInt(tags.Count);
            for (var i = 0; i < tags.Count; i++)
            {
                writer.WriteString(tags[i].Key);
                writer.WriteInt(tags[i].Value);
            }
        }

        void ReadTagCounts(SnapshotReader reader)
        {
            var count = reader.ReadInt();
            var tags = new List<KeyValuePair<string, int>>(count);
            for (var i = 0; i < count; i++)
            {
                tags.Add(new KeyValuePair<string, int>(reader.ReadString(), reader.ReadInt()));
            }

            Tags.ReplaceCounts(tags);
        }

        void WriteGrantedSpecs(SnapshotWriter writer, Dictionary<string, GameplayAbilityDef> abilityDefs)
        {
            var handles = new List<GameplayAbilitySpecHandle>(_grantedSpecs.Count);
            foreach (var pair in _grantedSpecs)
            {
                handles.Add(pair.Key);
            }

            handles.Sort(CompareHandle);
            writer.WriteInt(handles.Count);
            for (var i = 0; i < handles.Count; i++)
            {
                var spec = _grantedSpecs[handles[i]];
                RememberAbilityDef(abilityDefs, spec.Def);
                writer.WriteInt(spec.Handle.Value);
                writer.WriteString(spec.Def.AbilityId);
                writer.WriteInt(spec.Level);
                writer.WriteInt(spec.InputId);
            }
        }

        void ReadGrantedSpecs(SnapshotReader reader, Dictionary<string, GameplayAbilityDef> abilityDefs)
        {
            var count = reader.ReadInt();
            for (var i = 0; i < count; i++)
            {
                var handle = new GameplayAbilitySpecHandle(reader.ReadInt());
                var abilityId = reader.ReadString();
                var level = reader.ReadInt();
                var inputId = reader.ReadInt();
                if (!abilityDefs.TryGetValue(abilityId, out var def))
                {
                    throw new InvalidOperationException(
                        "Missing ability def '" + abilityId + "'. Restore the GasSnapshot instance that saved it.");
                }

                var spec = new GameplayAbilitySpec(handle, def, def.CreateAbility(), level, inputId);
                _grantedSpecs[handle] = spec;
                if (!_specsByAbilityId.TryGetValue(abilityId, out var list))
                {
                    list = new List<GameplayAbilitySpecHandle>(2);
                    _specsByAbilityId[abilityId] = list;
                }

                list.Add(handle);
            }
        }

        void WriteActiveEffects(SnapshotWriter writer, Dictionary<string, GameplayAbilityDef> abilityDefs)
        {
            writer.WriteInt(_activeEffects.Count);
            for (var i = 0; i < _activeEffects.Count; i++)
            {
                var effect = _activeEffects[i];
                writer.WriteInt(effect.Handle.Value);
                writer.WriteInt(effect.Spec.RuntimeId);
                writer.WriteUInt(effect.Source.Value);
                writer.WriteBool(effect.SourceAsc != null);
                writer.WriteFp(effect.RemainingTime);
                writer.WriteFp(effect.PeriodTimer);
                writer.WriteInt(effect.StackCount);
                WriteEffectDef(writer, new GameplayEffectDef(effect.Spec), abilityDefs, 0);
                WriteFpMap(writer, effect.Spec.SetByCaller);
                writer.WriteInt(effect.GrantedAbilityHandles.Count);
                for (var h = 0; h < effect.GrantedAbilityHandles.Count; h++)
                {
                    writer.WriteInt(effect.GrantedAbilityHandles[h].Value);
                }
            }
        }

        void ReadActiveEffects(
            SnapshotReader reader,
            Dictionary<string, GameplayAbilityDef> abilityDefs,
            Func<ActorId, AbilitySystemComponent> ascLookup)
        {
            var count = reader.ReadInt();
            for (var i = 0; i < count; i++)
            {
                var handle = reader.ReadInt();
                var runtimeId = reader.ReadInt();
                var source = new ActorId(reader.ReadUInt());
                var hadSourceAsc = reader.ReadBool();
                var remaining = reader.ReadFp();
                var periodTimer = reader.ReadFp();
                var stacks = reader.ReadInt();
                var def = ReadEffectDef(reader, abilityDefs, 0);
                var setByCaller = ReadFpMap(reader);
                var spec = new GameplayEffectSpec(def, setByCaller, runtimeId);
                var active = new ActiveGameplayEffect(spec, source, handle)
                {
                    RemainingTime = remaining,
                    PeriodTimer = periodTimer,
                    StackCount = stacks,
                    SnapshotHadSourceAsc = hadSourceAsc
                };
                var granted = reader.ReadInt();
                for (var h = 0; h < granted; h++)
                {
                    active.GrantedAbilityHandles.Add(new GameplayAbilitySpecHandle(reader.ReadInt()));
                }

                if (hadSourceAsc)
                {
                    var sourceAsc = ascLookup(source);
                    if (sourceAsc == null)
                    {
                        throw new InvalidOperationException(
                            "GAS snapshot cannot rebind effect source actor " + source.Value);
                    }

                    active.SourceAsc = sourceAsc;
                }

                _activeEffects.Add(active);
            }
        }

        void WriteActiveInstances(SnapshotWriter writer)
        {
            writer.WriteInt(_activeInstances.Count);
            for (var i = 0; i < _activeInstances.Count; i++)
            {
                var instance = _activeInstances[i];
                writer.WriteInt(instance.InstanceId);
                writer.WriteInt(instance.Spec.Handle.Value);
                writer.WriteInt((int)instance.State);
                var context = instance.Context;
                writer.WriteFp(context.Origin.x);
                writer.WriteFp(context.Origin.y);
                writer.WriteFp(context.Origin.z);
                writer.WriteFp(context.Direction.x);
                writer.WriteFp(context.Direction.y);
                writer.WriteFp(context.Direction.z);
                writer.WriteUInt(context.PrimaryTarget.Value);
                writer.WriteFp(context.Range);
                var info = instance.ActivationInfo;
                writer.WriteUInt(info.Instigator.Value);
                writer.WriteUInt(info.TriggerTarget.Value);
                writer.WriteString(info.TriggerTag.Name);
                WriteFpMap(writer, info.SetByCaller);
                instance.Spec.Ability.SaveActiveTasks(instance, writer);
            }
        }

        void ReadActiveInstances(SnapshotReader reader, BattleContext battle)
        {
            var count = reader.ReadInt();
            for (var i = 0; i < count; i++)
            {
                var instanceId = reader.ReadInt();
                var handle = new GameplayAbilitySpecHandle(reader.ReadInt());
                var state = (ActiveAbilityState)reader.ReadInt();
                var origin = new TSVector(reader.ReadFp(), reader.ReadFp(), reader.ReadFp());
                var direction = new TSVector(reader.ReadFp(), reader.ReadFp(), reader.ReadFp());
                var target = new ActorId(reader.ReadUInt());
                var range = reader.ReadFp();
                var context = new AbilityActivationContext(origin, direction, target, range, true);
                var info = new AbilityActivationInfo
                {
                    Instigator = new ActorId(reader.ReadUInt()),
                    TriggerTarget = new ActorId(reader.ReadUInt()),
                    TriggerTag = new GameplayTag(reader.ReadString())
                };
                var setByCaller = ReadFpMap(reader);
                if (setByCaller != null)
                {
                    foreach (var pair in setByCaller)
                    {
                        info.SetSetByCaller(pair.Key, pair.Value);
                    }
                }

                if (!_grantedSpecs.TryGetValue(handle, out var spec))
                {
                    throw new InvalidOperationException(
                        "Active ability spec handle " + handle.Value + " is missing after restore.");
                }

                var instance = new ActiveAbilityInstance(spec, context, info, instanceId)
                {
                    State = state
                };
                spec.Ability.RestoreActiveTasks(this, instance, battle, reader);
                _activeInstances.Add(instance);
            }
        }

        static void WriteEffectDef(
            SnapshotWriter writer,
            GameplayEffectDef def,
            Dictionary<string, GameplayAbilityDef> abilityDefs,
            int depth)
        {
            if (depth > MaxEffectDefDepth)
            {
                throw new InvalidOperationException("GameplayEffect snapshot nesting is too deep.");
            }

            if (def == null)
            {
                writer.WriteBool(false);
                return;
            }

            writer.WriteBool(true);
            writer.WriteString(def.EffectId);
            writer.WriteInt((int)def.DurationPolicy);
            writer.WriteInt((int)def.StackingPolicy);
            writer.WriteFp(def.Duration);
            writer.WriteFp(def.Period);
            writer.WriteInt(def.MaxStacks);
            WriteModifiers(writer, def.Modifiers);
            WriteTagList(writer, def.GrantedTags);
            WriteTagList(writer, def.ApplicationRequiredTags);
            WriteTagList(writer, def.ApplicationBlockedTags);
            WriteTagList(writer, def.ImmunityTags);
            WriteStringList(writer, def.CueTagsOnApply);
            WriteStringList(writer, def.CueTagsOnRemove);
            WriteFpMap(writer, def.CostAttributes);
            var granted = def.GrantedAbilityDefs;
            var grantedCount = granted?.Count ?? 0;
            writer.WriteInt(grantedCount);
            for (var i = 0; i < grantedCount; i++)
            {
                RememberAbilityDef(abilityDefs, granted[i]);
                writer.WriteString(granted[i].AbilityId);
            }

            WriteExecutions(writer, def.Executions, abilityDefs, depth);
        }

        static GameplayEffectDef ReadEffectDef(
            SnapshotReader reader,
            Dictionary<string, GameplayAbilityDef> abilityDefs,
            int depth)
        {
            if (depth > MaxEffectDefDepth)
            {
                throw new InvalidOperationException("GameplayEffect snapshot nesting is too deep.");
            }

            if (!reader.ReadBool())
            {
                return null;
            }

            var effectId = reader.ReadString();
            var durationPolicy = (EffectDurationPolicy)reader.ReadInt();
            var stacking = (EffectStackingPolicy)reader.ReadInt();
            var duration = reader.ReadFp();
            var period = reader.ReadFp();
            var maxStacks = reader.ReadInt();
            var modifiers = ReadModifiers(reader);
            var grantedTags = ReadTagList(reader);
            var required = ReadTagList(reader);
            var blocked = ReadTagList(reader);
            var immunity = ReadTagList(reader);
            var cueOnApply = ReadStringList(reader);
            var cueOnRemove = ReadStringList(reader);
            var cost = ReadFpMap(reader) ?? new Dictionary<string, FP>();
            var grantedCount = reader.ReadInt();
            var grantedAbilities = new List<GameplayAbilityDef>(grantedCount);
            for (var i = 0; i < grantedCount; i++)
            {
                var abilityId = reader.ReadString();
                if (!abilityDefs.TryGetValue(abilityId, out var abilityDef))
                {
                    throw new InvalidOperationException(
                        "Effect '" + effectId + "' grants missing ability '" + abilityId + "'.");
                }

                grantedAbilities.Add(abilityDef);
            }

            var executions = ReadExecutions(reader, abilityDefs, depth);
            return new GameplayEffectDef(
                effectId,
                durationPolicy,
                duration,
                modifiers,
                grantedTags,
                stacking,
                required,
                blocked,
                immunity,
                grantedAbilities,
                executions,
                period,
                cueOnApply,
                cueOnRemove,
                cost,
                maxStacks);
        }

        static void WriteExecutions(
            SnapshotWriter writer,
            IReadOnlyList<GameplayEffectExecution> executions,
            Dictionary<string, GameplayAbilityDef> abilityDefs,
            int depth)
        {
            var count = executions?.Count ?? 0;
            writer.WriteInt(count);
            for (var i = 0; i < count; i++)
            {
                var execution = executions[i];
                if (execution is DamageExecution damage)
                {
                    writer.WriteInt(1);
                    writer.WriteFp(damage.AttackScale);
                    writer.WriteString(damage.SnapshotSetByCallerKey);
                    writer.WriteInt((int)damage.SnapshotDamageType);
                    continue;
                }

                if (execution is HealExecution heal)
                {
                    writer.WriteInt(2);
                    WriteMagnitude(writer, heal.SnapshotMagnitude);
                    continue;
                }

                if (execution is ApplyGameplayEffectExecution apply)
                {
                    writer.WriteInt(3);
                    WriteEffectDef(writer, apply.SnapshotEffectDef, abilityDefs, depth + 1);
                    continue;
                }

                var name = execution == null ? "null" : execution.GetType().FullName;
                throw new NotSupportedException("Cannot snapshot gameplay effect execution " + name + ".");
            }
        }

        static List<GameplayEffectExecution> ReadExecutions(
            SnapshotReader reader,
            Dictionary<string, GameplayAbilityDef> abilityDefs,
            int depth)
        {
            var count = reader.ReadInt();
            var list = new List<GameplayEffectExecution>(count);
            for (var i = 0; i < count; i++)
            {
                var kind = reader.ReadInt();
                if (kind == 1)
                {
                    var scale = reader.ReadFp();
                    var key = reader.ReadString();
                    var damageType = (BattleDamageType)reader.ReadInt();
                    list.Add(new DamageExecution(scale, key, damageType));
                    continue;
                }

                if (kind == 2)
                {
                    list.Add(new HealExecution(ReadMagnitude(reader)));
                    continue;
                }

                if (kind == 3)
                {
                    list.Add(new ApplyGameplayEffectExecution(ReadEffectDef(reader, abilityDefs, depth + 1)));
                    continue;
                }

                throw new InvalidOperationException("Unknown gameplay effect execution kind " + kind + ".");
            }

            return list;
        }

        static void WriteModifiers(SnapshotWriter writer, IReadOnlyList<EffectModifier> modifiers)
        {
            var count = modifiers?.Count ?? 0;
            writer.WriteInt(count);
            for (var i = 0; i < count; i++)
            {
                var modifier = modifiers[i];
                writer.WriteString(modifier.AttributeName);
                writer.WriteInt((int)modifier.Operation);
                WriteMagnitude(writer, modifier.Magnitude);
            }
        }

        static List<EffectModifier> ReadModifiers(SnapshotReader reader)
        {
            var count = reader.ReadInt();
            var list = new List<EffectModifier>(count);
            for (var i = 0; i < count; i++)
            {
                var name = reader.ReadString();
                var operation = (EffectModifierOperation)reader.ReadInt();
                list.Add(new EffectModifier(name, operation, ReadMagnitude(reader)));
            }

            return list;
        }

        static void WriteMagnitude(SnapshotWriter writer, ModifierMagnitude magnitude)
        {
            writer.WriteInt((int)magnitude.Type);
            writer.WriteFp(magnitude.Value);
            writer.WriteString(magnitude.AttributeName);
            writer.WriteString(magnitude.SetByCallerKey);
        }

        static ModifierMagnitude ReadMagnitude(SnapshotReader reader)
        {
            var type = (ModifierMagnitudeType)reader.ReadInt();
            var value = reader.ReadFp();
            var attributeName = reader.ReadString();
            var key = reader.ReadString();
            switch (type)
            {
                case ModifierMagnitudeType.AttributeBased:
                    return ModifierMagnitude.FromAttribute(attributeName, value);
                case ModifierMagnitudeType.SetByCaller:
                    return ModifierMagnitude.FromSetByCaller(key);
                default:
                    return ModifierMagnitude.Constant(value);
            }
        }

        static void WriteTagList(SnapshotWriter writer, IReadOnlyList<GameplayTag> tags)
        {
            var count = tags?.Count ?? 0;
            writer.WriteInt(count);
            for (var i = 0; i < count; i++)
            {
                writer.WriteString(tags[i].Name);
            }
        }

        static List<GameplayTag> ReadTagList(SnapshotReader reader)
        {
            var count = reader.ReadInt();
            var list = new List<GameplayTag>(count);
            for (var i = 0; i < count; i++)
            {
                list.Add(new GameplayTag(reader.ReadString()));
            }

            return list;
        }

        static void WriteStringList(SnapshotWriter writer, IReadOnlyList<string> values)
        {
            var count = values?.Count ?? 0;
            writer.WriteInt(count);
            for (var i = 0; i < count; i++)
            {
                writer.WriteString(values[i]);
            }
        }

        static List<string> ReadStringList(SnapshotReader reader)
        {
            var count = reader.ReadInt();
            var list = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                list.Add(reader.ReadString());
            }

            return list;
        }

        static void WriteFpMap(SnapshotWriter writer, IReadOnlyDictionary<string, FP> map)
        {
            if (map == null)
            {
                writer.WriteInt(-1);
                return;
            }

            var keys = new List<string>(map.Count);
            foreach (var key in map.Keys)
            {
                keys.Add(key);
            }

            keys.Sort(CompareString);
            writer.WriteInt(keys.Count);
            for (var i = 0; i < keys.Count; i++)
            {
                writer.WriteString(keys[i]);
                writer.WriteFp(map[keys[i]]);
            }
        }

        static Dictionary<string, FP> ReadFpMap(SnapshotReader reader)
        {
            var count = reader.ReadInt();
            if (count < 0)
            {
                return null;
            }

            var map = new Dictionary<string, FP>(count);
            for (var i = 0; i < count; i++)
            {
                map[reader.ReadString()] = reader.ReadFp();
            }

            return map;
        }

        static void RememberAbilityDef(Dictionary<string, GameplayAbilityDef> abilityDefs, GameplayAbilityDef def)
        {
            if (def == null)
            {
                throw new InvalidOperationException("Cannot snapshot a null ability def.");
            }

            if (abilityDefs.TryGetValue(def.AbilityId, out var existing))
            {
                if (!ReferenceEquals(existing, def))
                {
                    throw new InvalidOperationException(
                        "Ability id '" + def.AbilityId + "' is bound to two different defs in one GAS snapshot.");
                }

                return;
            }

            abilityDefs.Add(def.AbilityId, def);
        }

        static int CompareString(string left, string right) => string.CompareOrdinal(left, right);

        static int CompareTagCount(KeyValuePair<string, int> left, KeyValuePair<string, int> right) =>
            string.CompareOrdinal(left.Key, right.Key);

        static int CompareHandle(GameplayAbilitySpecHandle left, GameplayAbilitySpecHandle right) =>
            left.Value.CompareTo(right.Value);
    }
}
