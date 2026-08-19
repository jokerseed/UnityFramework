# Framework.GAS

Gameplay Ability System 层，战斗规则的权威数据源。对标 UE GAS **单机 gameplay runtime**（无网络/编辑器）。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.GAS` |
| 命名空间 | `Framework.GAS` |
| 依赖 | `Framework.Core`、`Framework.Events`、`Framework.FixedMath` |

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
| 伤害管线 | 免疫 → 护盾 → 护甲/魔抗 → 易伤 → 暴击（`ASC.Random.Next01()` 为 `FP`，禁止 `UnityEngine.Random`） |
| 驱散 | `DispelEffects(tagQuery, maxCount)` |
| 资源属性 | Health / Mana / Shield 当前值不被 Modifier 全量重算覆盖 |
| 空闲跳过 | 无 Active 技能且无持续效果时 `ASC.Tick` 立即返回 |
| 打断 | `CancelAbilitiesWithTag` + Def `CancelAbilitiesWithTags` |
| 近战扇形 | `MeleeSweepAbility` / `MeleeSweepTask`（前摇、窗口、后摇，每目标只结算一次） |
| 闪避 | `DashAbility`：自身 IFrame + 朝向冲刺 |
| 霸体 / 倒地 | 眩晕不打断霸体；倒地无视霸体并禁手禁移 |
| 定点仿真 | 冷却 / 范围 / 伤害 / 属性 / GE 时长与 Modifier 为 `FP`；`AbilityActivationContext` 位姿为 `TSVector`；表现事件仍发 float |

## 定点约定

技能与效果的**仿真数值**使用 `Framework.FixedMath.FP`（含冷却、范围、半角、弹速、伤害、击退、属性、Cost / SetByCaller）。`AbilityActivationContext.Origin` / `Direction` 与 ASC `CueSimPosition` / `CueSimDirection` 为 `TSVector`；`CuePosition` / `CueDirection` 仍供 Cue 与事件 float 发布。`GameplayEventData.TargetSimLocation` 为仿真坐标（`TSVector`），`HandleGameplayEvent` 直接用于施法原点。Luban 表仍为 float，只在 `AbilityConfigFactory` / `EffectConfigFactory` 装配时转入。`DamageDealtEvent` / `AttributeChangedEvent` 等表现事件保持 float，由 ASC 在 Publish 时 `AsFloat()`。`ASC.Tick(FP)` 由 `GamePlayFramework` 以会话固定步长驱动，不再在入口把 float 转定点。超大数值战斗结算不要用 Q31.32。

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
dot.ToRuntimeSpec(new Dictionary<string, FP> { ["Damage"] = 10f });
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

- `Framework.GamePlay` — Tick、目标查询、Cue 转发、注入 `IDeterministicRandom`
- `Framework.GamePlay.Data` — Luban 表 → GAS Def 装配
