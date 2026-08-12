# Framework.Events

参考 [TEngine GameEvent](https://github.com/Alex-Rachel/TEngine) 实现的零 GC 事件系统。事件按 `struct` 类型分通道，分发时不复制监听列表，分发中增删监听会延迟到本轮结束。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Events` |
| 命名空间 | `Framework.Events` |
| 依赖 | 无（契约与实现同程序集） |

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
| `GlobalEventBus` / `GameEvent` | 全局 UI / 流程事件 |
| `ZeroGcEventBus` | 单场战斗表现总线（`BattleFramework.EventBus`） |

分发时直接遍历监听列表（不 `AddRange` 复制），分发中增删延迟到本轮结束后应用。

## 典型用法

### 全局事件

```csharp
using Framework.Events;
using Framework.GAS.Events;

void OnEnable()
{
    GameEvent.Listen<DamageDealtEvent>(OnDamage);
}

void OnDisable()
{
    GameEvent.RemoveListener<DamageDealtEvent>(OnDamage);
}

static void OnDamage(DamageDealtEvent evt)
{
    // UI / VFX
}
```

### 战斗内独立总线（`BattleFramework` 已默认使用）

```csharp
var framework = new BattleFramework();
framework.EventBus.Subscribe<GameplayCueEvent>(e => { /* ... */ });

// 推荐无 IDisposable 分配：
framework.EventBus.Unsubscribe<GameplayCueEvent>(handler);
```

### 发送

```csharp
GameEvent.Send(new DamageDealtEvent { /* ... */ });
// 或
framework.EventBus.Publish(evt);
```

## 设计说明

- 事件载荷必须是 **`struct`**（如 `Framework.GAS.Events`），避免装箱
- 领域事件定义在对应业务模块（GAS / ECS / …），总线只负责分发
- `Subscribe` 返回的 `IDisposable` 仍有少量堆分配；热路径注册建议 `Listen` + `RemoveListener`
- `Clear()` 会清空已注册过的所有事件通道

## 被谁使用

- `Framework.Core` — `BattleContext.Presentation`
- `Framework.GAS` / `Framework.Bridge` — 发布与订阅表现事件
- 业务层可通过 `GameEvent` 做全局 UI/流程事件
