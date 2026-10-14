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

- 切换顺序：旧状态 `OnExit` → `StateChanged` 事件 → 新状态 `OnEnter`，`CurrentStateTime` 归零
- `OnEnter` 里可以直接再切换；`OnExit` 里切换会抛 `InvalidOperationException`
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
