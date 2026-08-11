# Framework.Events

参考 [TEngine GameEvent](https://github.com/Alex-Rachel/TEngine) 实现的零 GC 事件系统。事件按 `struct` 类型分通道，分发时不复制监听列表，分发中增删监听会延迟到本轮结束。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Events` |
| 命名空间 | `Framework.Events` |
| 依赖 | `Framework.Core`（`IEventBus` 契约） |

## 核心类型

| 类型 | 职责 |
|------|------|
| `GlobalEventBus` | 全局零 GC 总线（`GameEvent` / `GameEventMgr`） |
| `ZeroGcEventBus` | 实例零 GC 总线，战斗内隔离 |
| `HandlerList<T>` | 单事件类型监听列表，延迟增删 |
| `GameEventMgr` | 全局单例管理器（参考 TEngine `EventMgr`） |
| `GameEvent` | 静态 `Send` / `Listen` / `Subscribe` 入口 |

## 与旧 `EventBus` 的差异

| | `Framework.Core.EventBus` | `ZeroGcEventBus` |
|---|---|---|
| 分发 | 每帧 `AddRange` 复制监听列表 | 直接遍历，无复制 |
| 存储 | `Dictionary<Type, List<Delegate>>` | 全局：`GlobalEventChannel<T>`；实例：`Dictionary` + `HandlerList<T>` |
| 分发中增删 | 不安全 | 延迟到分发结束后应用 |
| 适用 | 低频/原型 | 战斗热路径、表现事件 |

## 典型用法

### 全局事件

```csharp
using Framework.Events;
using Framework.Core.Events;

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

- 事件载荷必须是 **`struct`**（与现有 `BattleEvents` 一致），避免装箱
- `Subscribe` 返回的 `IDisposable` 仍有少量堆分配；热路径注册建议 `Listen` + `RemoveListener`
- `Clear()` 会清空已注册过的所有事件通道

## 被谁使用

- `Framework.Bridge` — `BattleFramework` 表现层总线
- 业务层可通过 `GameEvent` 做全局 UI/流程事件
