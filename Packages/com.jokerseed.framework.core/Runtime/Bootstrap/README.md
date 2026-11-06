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
- **组与组之间**可先后或分别 `Run`；依赖其它组时，必须先重置依赖方才能重置提供方。`Run` / `RunAsync` 在开始初始化之前就检查组名，找不到组会立刻抛 `KeyNotFoundException`

## 生命周期

```
SetModules("a", ...)  →  Idle
Run("a")              →  Running → Ready / Failed
SetModules("b", ...)  →  Idle（独立组）
Run("b")              →  Running → Ready / Failed
```

`Initialize` / `InitializeAsync` 抛出的异常（含取消产生的 `OperationCanceledException`）会把组设为 `Failed` 并触发 `Failed` 事件。已初始化的模块按逆序 `Shutdown`；某个模块的 `Shutdown` 抛异常不会挡住后面的模块。并发失败会立即取消同波次其它模块，并等待所有初始化退出后统一回滚；不响应取消的模块结束前，组保持 Running，禁止重置或重配，避免旧任务影响新一轮初始化。Bootstrap 销毁会递增 `RunSerial`，迟到的成功完成会立即 `Shutdown`。失败后再次 `Run` 会重新初始化。全部是同步模块时，`RunAsync` 在调用当帧就结束。

## 相关模块

- `Framework.Res` → `ResourceModule`
- `Framework.Config` → `ConfigModule`
- 启动入口：`Assets/Scripts/Launch.cs`

### 所有权与事件边界

- 一个模块实例只能注册到一个组；重复类型与 null 项在配置时拒绝。配置先生成并验证快照，失败不会覆盖原有模块列表。
- 外部依赖在启动时固定到具体提供组；Running / Ready 的依赖方仍存在时不能重置提供组。先重置依赖方，再重置提供方。
- Bootstrap 销毁按组启动的逆序关闭，保证依赖方先于提供方关闭。组内仍按初始化完成的逆序关闭。
- 状态、进度、Ready、Failed 的每个订阅者独立执行；订阅者异常仅记录日志，不改变初始化结果，也不阻止其它订阅者。通知回调中不允许重置或重配正在通知的组。
- 初始化函数如果抛错，模块自身必须清理尚未完成的部分资源；Bootstrap 只关闭成功完成初始化的模块。

## 全局服务所有权约定

- Bootstrap 管理模块的启动次序、取消与组依赖；PersistentSingleton 管理服务对象身份与最终释放。直接访问 Manager.Instance 仍兼容，但不要在 Bootstrap 管理期间手动关闭或替换该服务。
- 包装全局单例或静态服务的模块实现 `IExclusiveGameModule`。同一具体类型只能注册到一个组；其它组通过 Dependencies 使用已有服务，重置原组后才可转移注册。普通 IGameModule 保持多实例能力。
- 内置 Manager 的销毁清理集中在 `OnSingletonDestroy`，只对拥有单例身份的对象调用一次。重复 GameObject 的销毁不会清空活跃服务；DestroyInstance 同步完成清理，再安排 Unity 对象销毁，避免旧对象的延迟 OnDestroy 清掉替换实例。
- 清理钩子期间访问本类型 Instance 会抛 InvalidOperationException；重复 DestroyInstance 不会重复释放。清理抛错仍会解绑并销毁对象。
- 自定义派生类应将 OnDestroy 中的服务清理迁移至 OnSingletonDestroy；旧 OnDestroy override 仍可编译，但不能获得新钩子的一次性保证。所有 Unity 单例操作仍仅限主线程。
- 模块初始化失败须自行清理部分初始化资源；已成功初始化的模块由 Bootstrap 逆序关闭。异步取消须等待初始化退出，不能由第二个入口抢先重建同一服务。
