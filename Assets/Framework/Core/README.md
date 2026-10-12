# Framework.Core

战斗框架的基础设施层，不依赖 GAS / ECS / 资源管线。提供模块启动契约、Tick 契约、快照缓冲和常驻单例。战斗标识、命令缓冲和 `BattleContext` 在 `Framework.Battle.Core`，命名空间仍是 `Framework.Core`。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Core` |
| 命名空间 | `Framework.Core`（`Commands` / `Tick` 为子命名空间） |
| 依赖 | `Framework.Events`、`Framework.FixedMath` |

## 目录结构

```
Core/
├── Module/                     启动模块契约
├── Singleton/                  常驻单例
├── Snapshot/                   快照缓冲
└── Tick/                       Tick 契约
```

战斗类型在 `Assets/Framework/Battle.Core/`：`ActorId`、`BattleContext`、`BattleConstants`、`BattleDamageType`、`IDeterministicRandom`、`BattleCommandBuffer`。调用方的 `using` 不用改。

根目录仅保留 `Framework.Core.asmdef` 与本 README。

## 核心类型

| 类型 | 职责 |
|------|------|
| `IGameModule` | 业务模块契约；由 `GameBootstrap` 调度 |
| `ModulePhase` / `ModuleInitMode` | 启动阶段与同步/异步初始化 |
| `ActorId` | 逻辑层 Actor 标识，与 ECS `Entity` 解耦 |
| `BattleContext` | 将 `Commands` + `Presentation` + `IDeterministicRandom` 打包，供 GAS 使用 |
| `BattleConstants` | GAS 属性名、标签与物理默认值 |
| `IDeterministicRandom` | 战斗模拟随机契约，`Next01()` 返回 `FP`；由 GamePlay 注入 `TSRandom` |
| `PersistentSingleton<T>` | 懒加载 + `DontDestroyOnLoad` 常驻单例。退出标志在 SubsystemRegistration 时由 `ResetStaticState` 清掉，关闭 Domain Reload 后可以再次进入 Play |
| `BattleCommandBuffer` | 模拟热路径命令批量刷写（生成投射物、结算伤害）；不含玩家 / AI 行为意图 |
| `ITickable` | `GamePlayFramework`、`World` 的统一 Tick 契约 |

## PersistentSingleton 用法

```csharp
public sealed class AudioManager : PersistentSingleton<AudioManager>
{
    public void Play(string clip) { /* ... */ }
}

// 首次访问时自动创建 GameObject；Play Mode 下 DontDestroyOnLoad
AudioManager.Instance.Play("bgm_main");

// 需要销毁时（如热更切换）
AudioManager.DestroyInstance();

// 关闭 Domain Reload 时框架会在进入 Play 前调用；测试里也可以手动清退出标志
AudioManager.ResetStaticState();
```

## 设计原则

- **命令 vs 事件**：模拟走 `BattleCommandBuffer`，表现走 `Framework.Events.IEventBus`
- **随机**：战斗模拟使用 `IDeterministicRandom.Next01()`（返回 `FP`；由 GamePlay 注入 `TSRandom`），禁止 `UnityEngine.Random`
- **命令标量与位姿**：`SpawnProjectile` / `ApplyAreaEffect` 的位置与朝向为 `TSVector`；速度、半径、伤害、半角等为 `FP`；位移 `Offset` 为 `TSVector`
- **物理常量**：`BattleConstants` 中击退时长、碰撞半径、空间格子、默认法力/暴击倍率为 `FP`
- **零上层依赖**：Core 不引用 GAS、ECS、Config、Res、Bootstrap；事件契约与实现均在 `Framework.Events`

## 被谁使用

| 模块 | 用法 |
|------|------|
| `Framework.Bootstrap` | 调度 `IGameModule`；`GameBootstrap` 继承 `PersistentSingleton` |
| `Framework.GAS` | ASC 通过 `BattleContext` 发命令和事件 |
| `Framework.ECS` | `World` 消费 `BattleCommandBuffer` |
| `Framework.GamePlay` | 创建 `BattleContext` 并驱动 Tick |

## 说明

- 事件总线契约 / 实现：`Framework.Events`（`IEventBus`、`ZeroGcEventBus`、`GameEvent`）
- 领域事件载荷：对应业务模块（如 `Framework.GAS.Events`）
