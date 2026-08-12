# Framework.GAS

Gameplay Ability System 层，战斗规则的权威数据源。负责技能、效果、属性、标签与伤害管线。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.GAS` |
| 命名空间 | `Framework.GAS` |
| 依赖 | `Framework.Core` |

## 目录结构

```
GAS/
├── AbilitySystemComponent.cs     ASC 核心
├── Abilities/
│   ├── GameplayAbility.cs        技能基类
│   ├── AbilityActivationResult.cs
│   └── Builtin/                  内置技能（Fireball、Slash 等）
├── Effects/
│   ├── GameplayEffect.cs         效果定义与运行时实例
│   └── EffectStackingPolicy.cs   叠加策略
├── Attributes/
│   └── GameplayAttribute.cs      属性与 Modifier 聚合
├── Tags/
│   └── GameplayTag.cs            标签容器
├── Combat/
│   └── IDamageProcessor.cs       伤害计算接口
├── Events/
│   └── GasEvents.cs              技能/伤害/属性/Tag/Cue 表现事件
└── Targeting/
    └── ITargetSelector.cs        目标选择
```

## 核心类型

| 类型 | 职责 |
|------|------|
| `AbilitySystemComponent` | 管理生命/属性/技能/效果/标签，战斗逻辑入口 |
| `GameplayAbility` | 技能基类，`TryActivate` → 产生命令或效果 |
| `GameplayEffect` / `ActiveGameplayEffect` | Buff/Debuff 定义与运行时 |
| `GameplayAttributeSet` | 属性集（Health、Attack、Defense 等） |
| `GameplayTagContainer` | 状态标签（眩晕、无敌等） |
| `DefaultDamageProcessor` | 默认伤害公式 |

## 表现事件（`Framework.GAS.Events`）

| 事件 | 触发时机 |
|------|----------|
| `AbilityActivatedEvent` | 技能激活 |
| `DamageDealtEvent` | 伤害结算完成 |
| `DamageBlockedEvent` | 伤害被格挡/免疫 |
| `AttributeChangedEvent` | 属性值变化 |
| `TagChangedEvent` | GameplayTag 增删 |
| `GameplayCueEvent` | 表现 Cue（特效、音效） |

## 与 ECS 的分工

| 层 | 权威数据 |
|----|----------|
| GAS | 生命、属性、技能 CD、效果、标签 |
| ECS | 位置、速度、碰撞、投射物轨迹；`CombatStateComponent.IsAlive` 仅作存活标记 |

## 典型用法

```csharp
var asc = new AbilitySystemComponent(actorId);
asc.InitializeHealth(100f);
asc.RegisterAbility(new ProjectileAbility(...));

var result = asc.TryActivateAbility("Fireball", context, battleContext);
asc.ApplyEffect(effect, sourceId, battleContext.Presentation);
```

## 配置驱动

技能/效果的具体数值由 `Framework.Config` 的 `AbilityFactory` / `EffectFactory` 从 Luban 表创建，GAS 本身不依赖 Luban。

## 行为验证示例

以下用例描述 ASC 的预期行为，供开发时对照验证（不单独维护 Tests 程序集）。

### 伤害结算

```csharp
using Framework.Events;

IEventBus bus = new ZeroGcEventBus();
var asc = new AbilitySystemComponent(new ActorId(1));
asc.InitializeHealth(100f);

asc.ApplyDamage(new DamageContext(new ActorId(2), asc.ActorId, 30f, "Test"), bus);

// 基础生命不变，当前生命减少
Assert(asc.Attributes.GetBaseValue(BattleConstants.Health) == 100f);
Assert(asc.Attributes.GetCurrentValue(BattleConstants.Health) == 70f);
```

### 防御减伤

```csharp
asc.Attributes.GetOrCreate(BattleConstants.Defense).SetBaseValue(10f);
asc.ApplyDamage(new DamageContext(new ActorId(2), asc.ActorId, 30f, "Test"), bus);

// 30 伤害 - 10 防御 = 20 实际伤害
Assert(asc.Attributes.GetCurrentValue(BattleConstants.Health) == 80f);
```

### 免疫标签

```csharp
asc.Tags.AddTag(new GameplayTag(BattleConstants.TagImmuneDamage));
var applied = asc.ApplyDamage(new DamageContext(new ActorId(2), asc.ActorId, 50f, "Test"), bus);

Assert(applied == false);
Assert(asc.Attributes.GetCurrentValue(BattleConstants.Health) == 100f);
```

### 效果叠加（RefreshDuration）

```csharp
var spec = new GameplayEffectSpec(
    "Buff.Attack", EffectDurationPolicy.Duration, 5f,
    new[] { new EffectModifier(BattleConstants.Attack, EffectModifierOperation.Add, 5f) },
    stackingPolicy: EffectStackingPolicy.RefreshDuration);

asc.ApplyEffect(spec, new ActorId(99), bus);
asc.Tick(3f, bus);
asc.ApplyEffect(spec, new ActorId(99), bus);  // 刷新持续时间
asc.Tick(3f, bus);

// 效果仍在，攻击力 +5
Assert(asc.Attributes.GetCurrentValue(BattleConstants.Attack) == 15f);
```

### 标签层级匹配

```csharp
var container = new GameplayTagContainer();
container.AddTag(new GameplayTag("State.CrowdControl.Stunned"));

Assert(container.HasTag(new GameplayTag("State.CrowdControl")) == true);
Assert(container.HasTag(new GameplayTag("State.Dead")) == false);
```

## 被谁使用

- `Framework.Bridge` — 通过 `ActorRegistry` 持有 ASC
- `Framework.Config` — 工厂创建 `GameplayAbility` / `GameplayEffect`
