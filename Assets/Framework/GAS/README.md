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
| Cue | `IGameplayCueManager` / `EventBusGameplayCueManager` |

## Tick 顺序（GamePlay 驱动）

```
ActiveAbility Task → Cooldown → Effects（Duration / Periodic）
```

## 典型用法

```csharp
var asc = new AbilitySystemComponent(actorId);
asc.InitializeHealth(100f);

var handle = asc.GiveAbility(new GameplayAbilityDef(new ProjectileAbility(...)));
var result = asc.TryActivateAbility(handle, context, battle, out var instance);

// 取消
asc.CancelAbility(instance.InstanceId, eventBus);
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
asc.ApplyEffect(dot, sourceId, bus);
// Tick 6 秒 → 3 次 Execution
```

### SetByCaller 伤害

```csharp
instance.ActivationInfo.SetSetByCaller("Damage", 120f);
// DamageExecution 读取 SetByCaller
```

## 被谁使用

- `Framework.GamePlay` — Tick、目标查询、Cue 转发
- `Framework.Config` — Luban 工厂创建 Def
