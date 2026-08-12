# Framework.GamePlay

玩法主入口：编排 GAS 规则与 ECS 模拟，提供 `GamePlayFramework` 与 Bootstrap 模块 `GamePlayModule`。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.GamePlay` |
| 命名空间 | `Framework.GamePlay` |
| 依赖 | `Framework.Bootstrap`、`Framework.Core`、`Framework.Events`、`Framework.GAS`、`Framework.ECS`、`Framework.Logging`、`Framework.Config` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `GamePlayModule` | `IGameModule` 实现，创建并持有 `GamePlayFramework` |
| `GamePlayFramework` | 玩法运行时入口：创建 Actor、注册技能、驱动 Tick |
| `ActorRegistry` | ActorId ↔ ECS Entity ↔ ASC 三向映射 |
| `BattleCommandProcessor` | 刷写 `BattleCommandBuffer`（生成投射物、结算伤害） |
| `GamePlayConfigSetup` | 将 Luban 技能配置装配到框架的扩展方法 |

## Tick 流程

```
GamePlayFramework.Tick(deltaTime)
  1. 所有 ASC.Tick（技能 CD、效果持续时间、AbilityTask）
  2. Flush Spawn（生成投射物 Entity）
  3. World.Tick（ECS 移动/碰撞/生命周期）
  4. Flush Damage（碰撞产生的伤害命令 → GAS 结算）
  5. Sync Positions（ECS → BattleActor 坐标同步）
```

## Bootstrap 集成

`GamePlayModule` 在 `ModulePhase.Gameplay` 阶段初始化，依赖 `ConfigModule`：

```
Launch → ConfigModule → GamePlayModule
```

```csharp
using Framework.GamePlay;

var framework = GamePlayModule.Instance.Framework;
```

## 典型用法

```csharp
using Framework.GamePlay;

var framework = new GamePlayFramework();

var asc = framework.CreateActor(new ActorId(1), Vector3.zero, maxHealth: 100f, teamId: 1);
framework.GiveAbility(actorId, abilityDef);

framework.TryActivateAbility(actorId, abilityHandle, activationContext);
framework.EventBus.Subscribe<DamageDealtEvent>(e => { /* UI / VFX */ });

framework.Tick(Time.deltaTime);
```

## 完整示例（战斗演示）

以下示例可复制到 `Assets/Scripts/` 下的 MonoBehaviour 使用。

```csharp
using Framework.Config;
using Framework.Core;
using Framework.GamePlay;
using Framework.GAS.Events;
using UnityEngine;

public sealed class BattleDemo : MonoBehaviour
{
    GamePlayFramework _framework;
    readonly ActorId _player = new ActorId(1);
    readonly ActorId _enemy = new ActorId(2);

    void Start()
    {
        // 推荐：Launch 初始化后从 GamePlayModule 取用
        _framework = GamePlayModule.Instance?.Framework ?? new GamePlayFramework();

        var tables = BattleConfigBootstrap.Tables ?? BattleConfigBootstrap.LoadTables();
        var player = _framework.CreateActor(_player, Vector3.zero, 100f, teamId: 1);
        var enemy = _framework.CreateActor(_enemy, new Vector3(5f, 0f, 0f), 100f, teamId: 2);

        _framework.RegisterActorAbilities(
            _player, teamId: 1,
            abilityIds: new[] { "Fireball", "Slash" }, tables);

        _framework.EventBus.Subscribe<DamageDealtEvent>(e =>
            Debug.Log($"[Combat] {e.Source.Value} → {e.Target.Value}, Final={e.FinalDamage:F1}"));

        _framework.EventBus.Subscribe<GameplayCueEvent>(e =>
            Debug.Log($"[Cue] {e.CueTag} @ {e.Position}"));

        var result = _framework.TryActivateAbility(_player, "Fireball",
            new AbilityActivationContext(origin: Vector3.zero, direction: Vector3.right));

        Debug.Log($"[Demo] Fireball: {result.Success}");
    }

    void Update() => _framework?.Tick(Time.deltaTime);

    void OnDestroy() => _framework?.Dispose();
}
```

前置条件：已执行 **Tools → Luban → Generate Client Config** 打表；Launch 场景需注册 `ConfigModule` 与 `GamePlayModule`。

## 对外暴露

| 属性 | 类型 | 说明 |
|------|------|------|
| `EventBus` | `IEventBus` | 表现层事件 |
| `CueManager` | `IGameplayCueManager` | GameplayCue 转发 |
| `Commands` | `BattleCommandBuffer` | 命令缓冲（高级用法） |
| `Context` | `BattleContext` | 传给 GAS 的上下文 |
| `EcsWorld` | `World` | ECS 世界（高级用法） |
| `Registry` | `ActorRegistry` | Actor 注册表 |

## 依赖关系

```
GamePlay
 ├── Config   (Luban 表、AbilityFactory)
 ├── Core     (命令、事件、标识符)
 ├── GAS      (ASC、技能、效果)
 └── ECS      (World、System、空间哈希)
```

## 被谁使用

- `Assets/Scripts/Launch.cs` — 通过 `GamePlayModule` 初始化玩法框架
- 业务层 — 从 `GamePlayModule.Instance.Framework` 取用，或自行创建 `GamePlayFramework` 实例
