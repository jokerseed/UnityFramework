# Framework.Core

战斗框架的基础设施层，不依赖 GAS / ECS / 资源管线。提供标识符、命令缓冲、战斗上下文与 Tick 契约。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Core` |
| 命名空间 | `Framework.Core` |
| 依赖 | `Framework.Events`（`BattleContext.Presentation` 使用 `IEventBus`） |

## 目录结构

```
Core/
├── BattleContext.cs          战斗上下文（命令 + 表现事件）
├── BattleConstants.cs        属性名等常量
├── Identifiers.cs            ActorId
├── PersistentSingleton.cs    常驻 Mono 单例基类
├── Commands/
│   ├── BattleCommandBuffer.cs  热路径命令队列
│   └── BattleCommands.cs       SpawnProjectile、ApplyDamage 等
└── Tick/
    └── ITickable.cs            Tick 接口
```

## 核心类型

| 类型 | 职责 |
|------|------|
| `BattleCommandBuffer` | 模拟热路径命令批量刷写（生成投射物、结算伤害） |
| `BattleContext` | 将 `Commands` + `Presentation`（`IEventBus`）打包，供 GAS 使用 |
| `ActorId` | 逻辑层 Actor 标识，与 ECS `Entity` 解耦 |
| `ITickable` | `BattleFramework`、`World` 的统一 Tick 契约 |
| `PersistentSingleton<T>` | 懒加载 + `DontDestroyOnLoad` 常驻单例 |

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
- **零上层依赖**：Core 不引用 GAS、ECS、Config、Res；事件契约与实现均在 `Framework.Events`

## 被谁使用

| 模块 | 用法 |
|------|------|
| `Framework.GAS` | ASC 通过 `BattleContext` 发命令和事件 |
| `Framework.ECS` | `World` 消费 `BattleCommandBuffer` |
| `Framework.Bridge` | 创建 `BattleContext` 并驱动 Tick |

## 说明

- 事件总线契约 / 实现：`Framework.Events`（`IEventBus`、`ZeroGcEventBus`、`GameEvent`）
- 领域事件载荷：对应业务模块（如 `Framework.GAS.Events`）
