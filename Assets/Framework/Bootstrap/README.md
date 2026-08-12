# Framework.Bootstrap

应用级模块启动编排内核。支持**多组模块**独立配置、运行与状态监听。

> **注意**：`GameBootstrap` 是 Host（编排者），本身**不是** `IGameModule`。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Bootstrap` |
| 命名空间 | `Framework.Bootstrap` |
| 依赖 | `Framework.Core` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `IGameModule` | 业务模块契约（名称、阶段、依赖、同步/异步初始化） |
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
yield return bootstrap.RunAsync("launch");
bootstrap.Run("battle"); // 或 yield return RunAsync("battle")

// 各组初始化完成后，从各模块自己的入口取用（非 Bootstrap 职责）
var tables = ConfigService.Tables;
var manager = ResourceManager.Instance;
```

| API | 说明 |
|-----|------|
| `SetModules(name, modules)` | 配置一组模块，返回 `ModuleGroup` |
| `Run(name)` | 启动该组初始化（协程） |
| `RunAsync(name)` | `yield return` 等待该组完成 |
| `GetGroup(name)` | 获取组句柄，查 `IsReady` / 状态事件 |
| `ResetGroup(name)` | 关闭并清空该组，可重新配置 |

## 初始化模式

| 模式 | Bootstrap 行为 |
|------|----------------|
| `InitMode = Synchronous` | 直接调用 `Initialize()` |
| `InitMode = Asynchronous` | 协程等待 `InitializeAsync()` |

## 并发规则

- 组内按依赖波次串行；同波次且 `AllowConcurrentInitialization = true` 可并发
- **组与组之间**互不影响，可先后或分别 `Run`

## 生命周期

```
SetModules("a", ...)  →  Idle
Run("a")              →  Running → Ready / Failed
SetModules("b", ...)  →  Idle（独立组）
Run("b")              →  Running → Ready / Failed
```

## 相关模块

- `Framework.Res` → `ResourceModule`
- `Framework.Config` → `ConfigModule`
- 启动入口：`Assets/Scripts/Launch.cs`
