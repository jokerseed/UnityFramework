# Framework.Fsm

通用有限状态机 `Fsm<TOwner>`，以及基于它的流程（Procedure）管理：把游戏生命周期拆成“检查更新 → 预加载 → 登录 → 主城 → 战斗”这样的阶段。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Fsm` |
| 命名空间 | `Framework.Fsm` |
| 依赖 | `Framework.Core`、`Framework.Logging` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `Fsm<TOwner>` | 状态机本体，不依赖 Unity；持有者自己调用 `Update(dt)` |
| `FsmState<TOwner>` | 状态基类：`OnInit` / `OnEnter` / `OnUpdate` / `OnExit` / `OnDestroy` |
| `ProcedureBase` | 流程基类（`FsmState<ProcedureManager>`） |
| `ProcedureManager` | 持有流程状态机，在 Unity `Update` 中驱动当前流程 |
| `ProcedureModule` | Bootstrap 模块：注册流程，**不自动启动** |

## 状态机

```csharp
sealed class Idle : FsmState<Hero>
{
    protected override void OnUpdate(Fsm<Hero> fsm, float dt)
    {
        if (fsm.Owner.HasTarget) ChangeState<Chase>(fsm);
    }
}

var fsm = new Fsm<Hero>("hero-ai", hero, new Idle(), new Chase());
fsm.Start<Idle>();
fsm.Update(Time.deltaTime);
```

- 切换顺序：旧状态 `OnExit` → 设置新 `CurrentState` 并将 `CurrentStateTime` 归零 → 新状态 `OnEnter` → `StateChanged` 事件。首次启动没有 `OnExit`，事件的旧状态为 null。
- `OnEnter` 里可以立即再切换或销毁。只有返回后仍有效的那次进入会发出 `StateChanged`；例如 A 的 `OnEnter` 立即进入 B，则只通知 A → B，不再补发首次进入 A 的旧通知。即使后来返回同一个状态实例，也不会补发旧通知。
- 事件按 `OnEnter` 完成后的订阅快照同步分发。监听器内添加/移除订阅只影响后续分发；监听器切换或销毁状态机后，本次旧通知立即停止，后续监听器只接收仍有效的新通知。同状态重入也算一次新进入。`ProcedureChanged` 使用相同规则。
- `OnExit` 里切换或销毁会抛 `InvalidOperationException`，调用 `Update` 会被忽略，不增加计时、不执行 `OnUpdate`。
- 回调异常采用立即向调用方传播的规则：`OnExit` 抛错会中止切换/销毁并保留原状态；`OnEnter` 抛错会保留已设置的新状态但不发送本次通知；事件监听器抛错会中止本次分发但不会跳过 `OnEnter` 或回滚已完成的进入。回调自身已执行的操作不会回滚，调用方处理异常后可继续切换或销毁。
- 状态间共享数据用持有者字段，或 `SetData` / `TryGetData`
- 帧同步逻辑可以用 `Fsm<T>`，但 `Update` 要传逻辑帧步长，不要传 `Time.deltaTime`

## 流程

```csharp
// Launch：注册
new ProcedureModule(new ProcedureCheckUpdate(), new ProcedurePreload(), new ProcedureMain()),

// 启动组 Ready 后启动入口流程
group.Ready += _ => ProcedureManager.Instance.StartProcedure<ProcedureCheckUpdate>();

// 流程内切换
sealed class ProcedurePreload : ProcedureBase
{
    protected override void OnEnter(Fsm<ProcedureManager> fsm) => PreloadAsync(fsm).Forget();

    async UniTaskVoid PreloadAsync(Fsm<ProcedureManager> fsm)
    {
        await ConfigManager.Instance.PreloadTableAssetsAsync(TableNames, fsm.Owner.GetCancellationTokenOnDestroy());
        ChangeState<ProcedureMain>(fsm);
    }
}
```

入口流程不在模块 `Initialize` 里启动：同组其它模块此时可能还没就绪。
