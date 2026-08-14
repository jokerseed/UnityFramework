# Framework.Core

战斗框架的基础设施层，不依赖 GAS / ECS / 资源管线。提供标识符、命令缓冲、战斗上下文、Tick 契约与模块启动契约。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Core` |
| 命名空间 | `Framework.Core`（`Commands` / `Tick` 为子命名空间） |
| 依赖 | `Framework.Events`（`BattleContext.Presentation` 使用 `IEventBus`） |

## 目录结构

```
Core/
├── Module/                     启动模块契约（namespace Framework.Core）
│   ├── IGameModule.cs
│   ├── ModulePhase.cs
│   └── ModuleInitMode.cs
├── Battle/                     战斗标识与上下文
│   ├── ActorId.cs
│   ├── BattleContext.cs
│   └── BattleConstants.cs
├── Singleton/                  常驻单例
│   └── PersistentSingleton.cs
├── Commands/                   热路径命令（namespace Framework.Core.Commands）
│   ├── BattleCommandBuffer.cs
│   └── BattleCommands.cs
└── Tick/                       Tick 契约（namespace Framework.Core.Tick）
    └── ITickable.cs
```

根目录仅保留 `Framework.Core.asmdef` 与本 README。

## 核心类型

| 类型 | 职责 |
|------|------|
| `IGameModule` | 业务模块契约；由 `GameBootstrap` 调度 |
| `ModulePhase` / `ModuleInitMode` | 启动阶段与同步/异步初始化 |
| `ActorId` | 逻辑层 Actor 标识，与 ECS `Entity` 解耦 |
| `BattleContext` | 将 `Commands` + `Presentation`（`IEventBus`）打包，供 GAS 使用 |
| `BattleConstants` | GAS 属性名、标签与物理默认值 |
| `PersistentSingleton<T>` | 懒加载 + `DontDestroyOnLoad` 常驻单例 |
| `BattleCommandBuffer` | 模拟热路径命令批量刷写（生成投射物、结算伤害） |
| `ITickable` | `GamePlayFramework`、`World` 的统一 Tick 契约 |

## PersistentSingleton 用法

```csharp
public sealed class AudioManager : PersistentSingleton<AudioManager>
{
    public void Play(string clip) { /* ... */ }
}

// 首次访问时自动创建 GameObject 并 DontDestroyOnLoad
AudioManager.Instance.Play("bgm_main");

// 需要销毁时（如热更切换）
AudioManager.DestroyInstance();
```

## 设计原则

- **命令 vs 事件**：模拟走 `BattleCommandBuffer`，表现走 `Framework.Events.IEventBus`
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
