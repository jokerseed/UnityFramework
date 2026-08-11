# Framework.Core

战斗框架的基础设施层，不依赖 GAS / ECS / 资源管线。提供标识符、命令缓冲、事件总线与 Tick 契约。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Core` |
| 命名空间 | `Framework.Core` |
| 依赖 | 无（仅 Unity Engine） |

## 目录结构

```
Core/
├── BattleContext.cs          战斗上下文（命令 + 表现事件）
├── BattleConstants.cs        属性名等常量
├── Identifiers.cs            ActorId、EntityId
├── PersistentSingleton.cs    常驻 Mono 单例基类
├── Commands/
│   ├── BattleCommandBuffer.cs  热路径命令队列
│   └── BattleCommands.cs       SpawnProjectile、ApplyDamage 等
├── Events/
│   ├── IEventBus.cs / EventBus.cs
│   └── BattleEvents.cs         伤害、Cue、属性变更等事件
└── Tick/
    └── ITickable.cs            Tick 接口
```

## 核心类型

| 类型 | 职责 |
|------|------|
| `BattleCommandBuffer` | 模拟热路径命令批量刷写（生成投射物、结算伤害） |
| `EventBus` | 表现层事件（UI、特效 Cue），与命令缓冲分离 |
| `BattleContext` | 将 `Commands` + `Presentation` 打包，供 GAS 使用 |
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

- **命令 vs 事件**：模拟走 `BattleCommandBuffer`，表现走 `EventBus`
- **零上层依赖**：Core 不引用 GAS、ECS、Config、Res，可被任意层引用

## 被谁使用

| 模块 | 用法 |
|------|------|
| `Framework.GAS` | ASC 通过 `BattleContext` 发命令和事件 |
| `Framework.ECS` | `World` 消费 `BattleCommandBuffer` |
| `Framework.Bridge` | 创建 `BattleContext` 并驱动 Tick |
| `Framework.Bootstrap` | 可选扩展：注册全局 `EventBus` 服务 |

## 事件一览

| 事件 | 触发时机 |
|------|----------|
| `AbilityActivatedEvent` | 技能激活 |
| `DamageDealtEvent` | 伤害结算完成 |
| `DamageBlockedEvent` | 伤害被格挡/免疫 |
| `AttributeChangedEvent` | 属性值变化 |
| `TagChangedEvent` | GameplayTag 增删 |
| `GameplayCueEvent` | 表现 Cue（特效、音效） |
