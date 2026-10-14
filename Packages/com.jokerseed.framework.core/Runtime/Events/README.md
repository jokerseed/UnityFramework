# Framework.Events

参考 [TEngine GameEvent](https://github.com/Alex-Rachel/TEngine) 实现的零 GC 事件系统。事件按 `struct` 类型分通道，分发时不复制监听列表，分发中增删监听会延迟到本轮结束。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Events` |
| 命名空间 | `Framework.Events` |
| 依赖 | `Framework.Logging` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `IEventBus` | 事件总线契约（`Publish` / `Subscribe` / `Unsubscribe` / `Clear`） |
| `GlobalEventBus` | 全局零 GC 总线（`GameEvent` / `GameEventMgr`） |
| `ZeroGcEventBus` | 实例零 GC 总线，战斗内隔离 |
| `HandlerList<T>` | 单事件类型监听列表，延迟增删 |
| `GameEventMgr` | 全局单例管理器（参考 TEngine `EventMgr`） |
| `GameEvent` | 静态 `Send` / `Listen` / `Subscribe` 入口 |

## 契约与实现

`IEventBus` 与实现均在本模块，不放在 Core：

| 实现 | 场景 |
|------|------|
| `GlobalEventBus` / `GameEvent` | 进程级 UI、流程、网络。不进入战斗结算 |
| `ZeroGcEventBus` | 单场战斗表现总线（`GamePlayFramework.EventBus`）。模拟只 `Publish` |

战斗模拟（ECS / GAS / GamePlay）不订阅任何总线。规则走 `BattleCommandBuffer`；表现 struct 定义在 `Framework.GAS.Events`，只发到本场 `ZeroGcEventBus`。`GameplayEventData` 由 `AbilitySystemComponent.HandleGameplayEvent` 同步交给技能和 Task，不经过本模块。`SimDeterminismScanner` 会拒绝模拟目录里的 `GameEvent`、`GlobalEventBus`、`.Subscribe` 和 `.Listen<`。

分发时直接遍历监听列表（不 `AddRange` 复制）。分发中的增删要等最外层 `Publish` 结束才应用；同一轮里刚退订的监听器不会再被调用。某个监听器抛异常时记一条日志并继续后面的监听器，通道不会卡在“正在分发”。进入 Play（含关闭 Domain Reload）时，`SubsystemRegistration` 会 `Clear` 全部静态通道。实例总线 `ZeroGcEventBus` 不在这次清理里，由持有者自己 `Clear`。

## 典型用法

### 全局事件

全局总线只发流程 / UI 自己的 struct。下面的 `SomeFlowEvent` 是示意，换成业务自己的事件类型。不要在这里监听 `DamageDealtEvent`：伤害、死亡、Cue 只在本场战斗总线上。

```csharp
using Framework.Events;

void OnEnable()
{
    GameEvent.Listen<SomeFlowEvent>(OnFlow);
}

void OnDisable()
{
    GameEvent.RemoveListener<SomeFlowEvent>(OnFlow);
}
```

### 战斗内独立总线（`GamePlayFramework` 已默认使用）

```csharp
var framework = new GamePlayFramework();
framework.EventBus.Subscribe<GameplayCueEvent>(e => { /* ... */ });

// 推荐无 IDisposable 分配：
framework.EventBus.Unsubscribe<GameplayCueEvent>(handler);
```

### 发送

```csharp
GameEvent.Send(new SomeFlowEvent());
framework.EventBus.Publish(new DamageDealtEvent { /* 只发到本场总线 */ });
```

## 设计说明

- 事件载荷必须是 **`struct`**（如 `Framework.GAS.Events`），避免装箱
- 领域事件定义在对应业务模块（GAS / ECS / …），总线只负责分发
- `Subscribe` 返回的 `IDisposable` 仍有少量堆分配；热路径注册建议 `Listen` + `RemoveListener`
- `Clear()` 会清空已注册过的所有事件通道

## 被谁使用

- `Framework.Core` — `BattleContext.Presentation`
- `Framework.GAS` / `Framework.GamePlay` — 只向本场总线 `Publish` 表现事件，不 `Subscribe`
- 业务表现层订阅 `GamePlayFramework.EventBus`；全局 UI / 流程走 `GameEvent`
