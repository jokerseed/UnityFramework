# Framework.GAS

Gameplay Ability System 层，战斗规则的权威数据源。对标 UE GAS **单机 gameplay runtime**（无网络/编辑器）。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.GAS` |
| 命名空间 | `Framework.GAS` |
| 依赖 | `Framework.Core`、`Framework.Events` |

## 核心能力

| 能力 | 类型 |
|------|------|
| Spec + 生命周期 | `GameplayAbilityDef` / `GameplayAbilitySpec` / `ActiveAbilityInstance` / `Commit` / `End` / `Cancel` |
| GameplayEffect | `GameplayEffectDef` / `GameplayEffectSpec` / `GameplayEffectHandle` / Remove / Periodic |
| Modifier | `ModifierMagnitude`（Constant / AttributeBased / SetByCaller）/ `Override` |
| Execution | `DamageExecution` / `HealExecution` / `ApplyGameplayEffectExecution` |
| AbilityTask | `WaitDelayTask` / `ApplyGameplayEffectToTargetTask` / `WaitGameplayEventTask` |
| Targeting | `GameplayTargetData` / `TargetDataFilter`（GamePlay 空间查询） |
| GameplayEvent | `GameplayEventData` / `HandleGameplayEvent` |
| Cue | `IGameplayCueManager` / `EventBusGameplayCueManager`（Execute / Add / Remove） |
| 控制 | 眩晕打断禁手、沉默禁手、定身禁移动（Tag 常量） |
| 冷却 | 可驱散的冷却 GE；`CooldownId` 共享组 |
| 叠层 | `MaxStacks` + `AggregateBySource` 按来源 |
| 伤害管线 | 免疫 → 护盾 → 护甲/魔抗 → 易伤 → 暴击 |
| 驱散 | `DispelEffects(tagQuery, maxCount)` |
| 资源属性 | Health / Mana / Shield 当前值不被 Modifier 全量重算覆盖 |
| 空闲跳过 | 无 Active 技能且无持续效果时 `ASC.Tick` 立即返回 |
| 打断 | `CancelAbilitiesWithTag` + Def `CancelAbilitiesWithTags` |
| 近战扇形 | `MeleeSweepAbility` / `MeleeSweepTask`（前摇、窗口、后摇，每目标只结算一次） |
| 闪避 | `DashAbility`：自身 IFrame + 朝向冲刺 |
| 霸体 / 倒地 | 眩晕不打断霸体；倒地无视霸体并禁手禁移 |

## Tick 顺序（GamePlay 驱动）

```
若已死亡则跳过
ActiveAbility Task → Effects（Duration / Periodic，Source 为施法者 ASC）
```

## 典型用法

```csharp
var asc = new AbilitySystemComponent(actorId);
asc.InitializeCombatAttributes(100f, 100f);

var handle = asc.GiveAbility(new GameplayAbilityDef(new ProjectileAbility(...)));
var result = asc.TryActivateAbility(handle, context, battle, out var instance);

// 同技能可再 Give 一份 Spec；按 ID 激活取第一份
asc.CancelAbility(instance.InstanceId, eventBus);
asc.DispelEffects(new GameplayTag("Effect.Debuff"), maxCount: 3, eventBus);
```

## 行为验证示例

### Spec 生命周期

```csharp
var handle = asc.GiveAbility(new GameplayAbilityDef(ability));
asc.TryActivateAbility(handle, context, battle, out var instance);
asc.CancelAbility(instance.InstanceId, bus);
// GameplayAbility.End(owner, instance) 被调用
```

### Periodic DOT

```csharp
var dot = new GameplayEffectDef(
    "DOT.Poison", EffectDurationPolicy.Duration, 6f,
    executions: new[] { new DamageExecution(setByCallerKey: "Damage") },
    period: 2f);
dot.ToRuntimeSpec(new Dictionary<string, float> { ["Damage"] = 10f });
asc.ApplyEffect(dot, sourceId, bus, sourceAsc: caster);
// 上身立即 Execution 一次，之后每 2 秒；Tick 6 秒共 4 次
```

### SetByCaller 伤害

```csharp
instance.ActivationInfo.SetSetByCaller("Damage", 120f);
// DamageExecution 读取 SetByCaller
```

### 控制 / 驱散 / 护盾

```csharp
// 眩晕：授予 State.CrowdControl.Stunned → CanActivate 失败并打断 Active
asc.ApplyEffect(stunDef, sourceId, bus, sourceAsc: caster);
// 驱散冷却后立刻可放
asc.DispelEffects(new GameplayTag(BattleConstants.TagCooldown), maxCount: 0, bus);
// 护盾：GE 提供上限，伤害扣当前值；上晕等其它 GE 不会把血/盾打回满
// 易伤：IncomingDamageMultiplier 默认 1，Vulnerable GE 乘 1.5
asc.CancelAbilitiesWithTag(new GameplayTag("Ability.Melee"), bus);
// 近战连招：Slash 判定开始时上 ComboWindow，Slash2/Slash3 需要该 Tag 并 Cancel Ability.Melee.Active
// Slash3 带 State.HyperArmor：期间被晕不会 Cancel；Knockdown 仍会打断
```

### Instant GE

```csharp
// Instant：Modifier 与 Execution 都会跑；GrantedTags 只在本次施加期间存在；Cue 为 Execute
asc.ApplyEffect(instantDef, sourceId, bus);
```

### Cue

Duration / Infinite GE 的 `CueTagsOnApply` 发 Add，移除时 `CueTagsOnRemove` 发 Remove（Demo 配同一 Tag 才能成对销毁）。Instant GE 与技能 Cast 发 Execute。表现表 `tbcue`（`CfgTbCue`）由业务层 `BattleCuePresenter` 订阅 `GameplayCueEvent` 播放，GAS 不引用资源。

```csharp
asc.CueManager = new EventBusGameplayCueManager(bus);
framework.EventBus.Subscribe<GameplayCueEvent>(presenter.Handle);
```

## 被谁使用

- `Framework.GamePlay` — Tick、目标查询、Cue 转发
- `Framework.GamePlay.Data` — Luban 表 → GAS Def 装配
