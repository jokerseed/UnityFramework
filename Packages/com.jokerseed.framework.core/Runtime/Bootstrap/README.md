# Framework.Bootstrap

应用级模块启动编排内核。支持**多组模块**独立配置、运行与状态监听。

> **注意**：`GameBootstrap` 是 Host（编排者），本身**不是** `IGameModule`。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Bootstrap` |
| 命名空间 | `Framework.Bootstrap` |
| 依赖 | `Framework.Core`、`Framework.Logging` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `IGameModule` / `ModulePhase` / `ModuleInitMode` | 模块契约（定义在 `Framework.Core`，由本程序集调度） |
| `ModuleGroup` | 一组模块的句柄，含状态与事件 |
| `ModuleGroupState` | `Idle` / `Running` / `Ready` / `Failed` |
| `GameBootstrap` | 常驻单例，仅负责多组模块的初始化调度 |

## 多组模块

```csharp
var bootstrap = GameBootstrap.Instance;

// 配置组 A
var groupA = bootstrap.SetModules("launch", new IGameModule[]
{
    new ResourceModule(),
    new ConfigModule(),
});

// 配置组 B（可稍后运行）
var groupB = bootstrap.SetModules("battle", new IGameModule[]
{
    // new BattleModule(),
});

// 监听状态
groupA.StateChanged += g => Debug.Log($"{g.Name} -> {g.State}");
groupA.Ready += g => Debug.Log($"{g.Name} ready");
groupA.Failed += (g, ex) => Debug.LogError(ex);
groupA.ProgressChanged += (g, cur, total) => Debug.Log($"{cur}/{total}");

groupB.StateChanged += g => Debug.Log($"{g.Name} -> {g.State}");

// 运行组 A，完成后可再运行组 B
await bootstrap.RunAsync("launch", this.GetCancellationTokenOnDestroy());
bootstrap.Run("battle"); // 或 await bootstrap.RunAsync("battle")

// 各组初始化完成后，从各模块自己的入口取用（非 Bootstrap 职责）
var tables = ConfigManager.Instance.LoadTables<cfg.CfgTables>();
var manager = ResourceManager.Instance;
```

| API | 说明 |
|-----|------|
| `SetModules(name, modules)` | 配置一组模块，返回 `ModuleGroup` |
| `Run(name)` | 启动该组初始化，不等待（内部 `RunAsync(name).Forget()`） |
| `RunAsync(name, ct)` | `await` 等待该组结束；失败不抛异常，查 `ModuleGroup.State`。取消或 Bootstrap 销毁时中止并回滚 |
| `GetGroup(name)` | 获取组句柄，查 `IsReady` / 状态事件 |
| `ResetGroup(name)` | 关闭并清空该组，可重新配置。组仍在 `Running` 时抛异常 |

## 初始化模式

| 模式 | Bootstrap 行为 |
|------|----------------|
| `InitMode = Synchronous` | 直接调用 `Initialize()` |
| `InitMode = Asynchronous` | `await InitializeAsync(CancellationToken)`（UniTask）；默认实现调用 `Initialize()` |

## 并发规则

- 组内按依赖波次串行；同波次且 `AllowConcurrentInitialization = true` 可并发（`UniTask.WhenAll`），任一模块失败会取消同波次其余模块
- 依赖可以是具体类型或接口。同一个依赖匹配到多个模块时直接失败，不会随便挑一个
- 已经 `Ready` 的其它组可以满足当前组的依赖。还没就绪的组不算
- **组与组之间**互不影响，可先后或分别 `Run`。`Run` / `RunAsync` 在开始初始化之前就检查组名，找不到组会立刻抛 `KeyNotFoundException`

## 生命周期

```
SetModules("a", ...)  →  Idle
Run("a")              →  Running → Ready / Failed
SetModules("b", ...)  →  Idle（独立组）
Run("b")              →  Running → Ready / Failed
```

`Initialize` / `InitializeAsync` 抛出的异常（含取消产生的 `OperationCanceledException`）会把组设为 `Failed` 并触发 `Failed` 事件。已初始化的模块按逆序 `Shutdown`；某个模块的 `Shutdown` 抛异常不会挡住后面的模块。失败会递增 `RunSerial`，迟到的完成（并发波次中失败后才结束的模块）不会再记进已初始化列表，而是立即 `Shutdown`。失败后再次 `Run` 会重新初始化。全部是同步模块时，`RunAsync` 在调用当帧就结束。

## 相关模块

- `Framework.Res` → `ResourceModule`
- `Framework.Config` → `ConfigModule`
- 启动入口：`Assets/Scripts/Launch.cs`
