# Framework.Bridge

GAS 与 ECS 的粘合层，提供战斗框架的唯一运行时入口 `BattleFramework`。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Bridge` |
| 命名空间 | `Framework.Bridge` |
| 依赖 | `Framework.Core`、`Framework.GAS`、`Framework.ECS` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `BattleFramework` | 战斗入口：创建 Actor、注册技能、驱动 Tick |
| `ActorRegistry` | ActorId ↔ ECS Entity ↔ ASC 三向映射 |
| `BattleCommandProcessor` | 刷写 `BattleCommandBuffer`（生成投射物、结算伤害） |

## Tick 流程

```
BattleFramework.Tick(deltaTime)
  1. 所有 ASC.Tick（技能 CD、效果持续时间）
  2. Flush Spawn（生成投射物 Entity）
  3. World.Tick（ECS 移动/碰撞/生命周期）
  4. Flush Damage（碰撞产生的伤害命令 → GAS 结算）
  5. Sync Positions（GAS → ECS 坐标同步）
```

## 典型用法

```csharp
using Framework.Bridge;

var framework = new BattleFramework();

// 创建 Actor
var asc = framework.CreateActor(new ActorId(1), Vector3.zero, maxHealth: 100f, teamId: 1);

// 注册技能（通常由 Config 模块的工厂创建）
framework.RegisterAbility(actorId, ability);

// 激活技能
framework.TryActivateAbility(actorId, "Fireball", activationContext);

// 订阅表现事件
framework.EventBus.Subscribe<DamageDealtEvent>(e => { /* UI / VFX */ });

// 每帧驱动
framework.Tick(Time.deltaTime);
```

## 完整示例（战斗演示）

以下示例原先在 `BattleFrameworkDemo` 中，现以文档形式维护。可复制到 `Assets/Scripts/` 下的 MonoBehaviour 使用。

```csharp
using Framework.Bridge;
using Framework.Config;
using Framework.Core;
using Framework.Core.Events;
using Framework.GAS.Abilities;
using UnityEngine;

public sealed class BattleDemo : MonoBehaviour
{
    BattleFramework _framework;
    readonly ActorId _player = new ActorId(1);
    readonly ActorId _enemy = new ActorId(2);

    void Start()
    {
        _framework = new BattleFramework();

        // Editor 直读配置（正式项目走 Launch + Bootstrap）
        var tables = BattleConfigBootstrap.LoadTables();
        var player = _framework.CreateActor(_player, Vector3.zero, 100f, teamId: 1);
        var enemy = _framework.CreateActor(_enemy, new Vector3(5f, 0f, 0f), 100f, teamId: 2);

        BattleConfigBootstrap.RegisterActorAbilities(
            _framework, _player, teamId: 1,
            abilityIds: new[] { "Fireball", "Slash" }, tables);

        _framework.EventBus.Subscribe<DamageDealtEvent>(e =>
            Debug.Log($"[Combat] {e.Source.Value} → {e.Target.Value}, Final={e.FinalDamage:F1}"));

        _framework.EventBus.Subscribe<GameplayCueEvent>(e =>
            Debug.Log($"[Cue] {e.CueTag} @ {e.Position}"));

        var result = _framework.TryActivateAbility(_player, "Fireball",
            new AbilityActivationContext(origin: Vector3.zero, direction: Vector3.right));

        Debug.Log($"[Demo] Fireball: {result.Success}");
    }

    void Update()
    {
        _framework?.Tick(Time.deltaTime);
    }

    void OnDestroy() => _framework?.Dispose();
}
```

前置条件：已执行 **Tools → Luban → Generate Client Config** 打表。

## 对外暴露

| 属性 | 类型 | 说明 |
|------|------|------|
| `EventBus` | `IEventBus` | 表现层事件 |
| `Commands` | `BattleCommandBuffer` | 命令缓冲（高级用法） |
| `Context` | `BattleContext` | 传给 GAS 的上下文 |
| `EcsWorld` | `World` | ECS 世界（高级用法） |
| `Registry` | `ActorRegistry` | Actor 注册表 |

## 依赖关系

```
Bridge
 ├── Core    (命令、事件、标识符)
 ├── GAS     (ASC、技能、效果)
 └── ECS     (World、System、空间哈希)
```

## 被谁使用

- `Framework.Config` — `RegisterActorAbilities` 向框架注册 Luban 驱动的技能
- 未来可新增 `BattleModule`（`IGameModule`）在此层初始化 `BattleFramework`
