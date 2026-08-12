# Framework.GamePlay

玩法主入口：编排 GAS 规则与 ECS 模拟，提供 `GamePlayFramework` 与 Bootstrap 模块 `GamePlayModule`。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.GamePlay` |
| 命名空间 | `Framework.GamePlay` / `Framework.GamePlay.Data` |
| 依赖 | `Framework.Bootstrap`、`Framework.Core`、`Framework.Events`、`Framework.GAS`、`Framework.ECS`、`Framework.Logging`、`Framework.Config` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `GamePlayModule` | `IGameModule` 实现，创建并持有 `GamePlayFramework` |
| `GamePlayFramework` | 玩法运行时入口：创建 Actor、注册技能、驱动 Tick |
| `ActorRegistry` | ActorId ↔ ECS Entity ↔ ASC 三向映射 |
| `BattleCommandProcessor` | 刷写 `BattleCommandBuffer`（生成投射物、结算伤害） |

## GamePlay.Data（配置装配层）

命名空间 `Framework.GamePlay.Data`：读 `cfg.Tables`，装配到 GAS，**不含 Tick/模拟**。

| 类型 | 职责 |
|------|------|
| `AbilityConfigFactory` | Luban 技能行 → `GameplayAbilityDef` |
| `EffectConfigFactory` | Luban 效果行 → `GameplayEffectDef` |
| `GamePlayConfigSetup` | `RegisterActorAbilities` 扩展方法 |
| `BattleConfigApplier` | 对 ASC 应用 Luban 效果 |

## Tick 流程

```
GamePlayFramework.Tick(deltaTime)
  0. RebuildActors（空间索引，供 GAS 查询）
  1. 所有 ASC.Tick（技能 CD、效果、AbilityTask）
  2. Flush Spawn → ECS Tick → Flush Damage → Sync Positions
```

## Bootstrap 集成

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
using Framework.GamePlay.Data;

var framework = new GamePlayFramework();
framework.RegisterActorAbilities(actorId, teamId: 1, abilityIds, ConfigService.Tables);
framework.Tick(Time.deltaTime);
```

## 完整示例（战斗演示）

```csharp
using Framework.Config;
using Framework.Core;
using Framework.GamePlay;
using Framework.GamePlay.Data;
using Framework.GAS.Events;
using UnityEngine;

public sealed class BattleDemo : MonoBehaviour
{
    GamePlayFramework _framework;
    readonly ActorId _player = new ActorId(1);
    readonly ActorId _enemy = new ActorId(2);

    void Start()
    {
        _framework = GamePlayModule.Instance?.Framework ?? new GamePlayFramework();

        var tables = ConfigService.Tables;
        if (tables == null)
        {
            throw new System.InvalidOperationException("Initialize ConfigModule before running battle demo.");
        }
        _framework.CreateActor(_player, Vector3.zero, 100f, teamId: 1);
        _framework.CreateActor(_enemy, new Vector3(5f, 0f, 0f), 100f, teamId: 2);

        _framework.RegisterActorAbilities(
            _player, teamId: 1,
            abilityIds: new[] { "Fireball", "Slash" }, tables);

        _framework.EventBus.Subscribe<DamageDealtEvent>(e =>
            Debug.Log($"[Combat] {e.Source.Value} → {e.Target.Value}, Final={e.FinalDamage:F1}"));
    }

    void Update() => _framework?.Tick(Time.deltaTime);
    void OnDestroy() => _framework?.Dispose();
}
```

## 依赖关系

```
GamePlay
 ├── Config      (Tables 只读)
 ├── Data        (Tables → GAS Def 装配)
 ├── Core / GAS / ECS
```

## 被谁使用

- `Assets/Scripts/Launch.cs` — `GamePlayModule`
- 业务层 — `GamePlayModule.Instance.Framework` 或自行创建实例
