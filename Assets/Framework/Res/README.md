# Framework.Res

YooAsset 资源管线封装：**通用**加载、释放、反序列化统一入口。Luban 配置表见 `Framework.Config`。

异步路径由 **`ResourceScheduler` 分帧调度**：Load / Instantiate / Unload 统一排队，**时间预算为主、个数上限为辅**。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Res` |
| 命名空间 | `Framework.Res` |
| 依赖 | `Framework.Core`、`Framework.Logging`、`YooAsset` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `ResourceModule` | `IGameModule` 实现，初始化 YooAsset 并注册 `ResourceManager` |
| `ResourceManager` | 常驻单例：包初始化、加载/实例化/卸载入口，每帧驱动调度器 |
| `ResourceScheduler` | 内部调度器（业务不直接调用） |
| `ResourceSchedulerOptions` | 每帧时间预算与个数上限 |
| `ResourceRequestHandle` | 调度请求：等待完成 / 取消 |
| `ResourceInitOptions` | 包名、运行模式（EditorSimulate / Offline / Host） |
| `ResourceAddresses` | 寻址规则 |
| `ResourceAssetHandle` | 资源句柄，`Dispose` / `InstantiateSync` |
| `ResourceSceneHandle` | 场景句柄 |

## 调度思路

### 为什么要调度

同一帧内连开很多 `LoadAssetAsync`、连 `Instantiate` 多个 Prefab、或反复 `UnloadUnusedAssets`，会造成主线程尖峰。调度器把这三类工作放进队列，**按预算分帧消化**。

### 为什么不用「每帧固定 N 个」

Instantiate 可能 3ms 也可能 30ms；发起一次 Load 几乎不占主线程。固定 N 个无法稳住帧时间。

实际规则：

```
本帧能处理多少 = min(
    时间预算还够,
    各类型 Count 上限还剩多少,
    队列里还有几个
)
```

- **时间（ms）是主控**
- **个数上限是安全阀**（防并发过高、防回调扎堆）
- 某一类本帧一个都还没跑、且总预算还没用完时，**至少执行 1 个**（避免超重 Instantiate 永远排不上）

### 三类工作

| 类型 | 怎么限 | 说明 |
|------|--------|------|
| **Load** | 分三阶段 | Start 有个数+并发上限；InFlight 不占主线程；Complete 回调走时间预算 |
| **Instantiate** | 专用 ms + 个数上限 | 内部仍是 `InstantiateSync`，只是换帧执行 |
| **UnloadUnusedAssets** | 合并请求，默认空闲才跑 | 多次 `Request` 合成 1 次；不和 UI 开窗口抢预算 |

Load 三阶段：

```
Enqueue → Pending
  Tick: 取出 ≤ MaxLoadStartsPerFrame，且 InFlight < MaxLoadInFlight，调用 YooAsset LoadAssetAsync
  InFlight: 轮询 IsDone，完成的进入 Complete 队列
  Tick: 按 CallbackBudgetMs / MaxCallbacksPerFrame 派发 onComplete
```

### 谁走调度、谁旁路

| API | 是否进调度 |
|-----|------------|
| `LoadAssetAsync` / `LoadAssetScheduled` | 是 |
| `InstantiateAsync` / `InstantiateScheduled` | 是 |
| `RequestUnloadUnusedAssets` | 是（合并） |
| `LoadAssetSync` | **否**（热路径 / 启动读表） |
| `LoadBytes` / `LoadBinary` | **否** |
| `LoadSceneAsync` | **否**（切场景本身就是整帧级操作） |
| `ResourceAssetHandle.InstantiateSync` | **否**（调用方当帧立刻实例化） |

`UIManager.Show` 仍同步加载+同步实例化；`ShowAsync` 走调度（Load 再 Instantiate）。

## 加载 API

```csharp
var res = ResourceManager.Instance;

// 同步（旁路调度）
using var handle = res.LoadAssetSync<GameObject>(ResourceAddresses.MainPrefab);

// 异步协程（内部入队，用法与以前相同）
yield return res.LoadAssetAsync<GameObject>(location, h => handle = h);

// 入队并拿到请求句柄（可 Cancel）
var request = res.LoadAssetScheduled<AudioClip>(location, clipHandle => { }, priority: 0);
request.Cancel();

// 分帧实例化
yield return res.InstantiateAsync(handle, parent, go => instance = go, priority: 10);

// 合并卸载未使用资源（禁止业务直接调 YooAsset UnloadUnusedAssets）
res.RequestUnloadUnusedAssets();
```

`priority` 越大越先处理；同优先级 FIFO。UI 异步打开默认 `10`。

## 预算配置

挂在 `ResourceManager` Inspector 的 `Scheduler Options`：

| 字段 | 默认 | 含义 |
|------|------|------|
| `MaxFrameBudgetMs` | 4 | 本帧调度总时间上限 |
| `MaxLoadStartsPerFrame` | 2 | 每帧新发起加载数 |
| `MaxLoadInFlight` | 8 | 同时进行中的加载上限 |
| `CallbackBudgetMs` | 1 | 完成回调时间预算 |
| `MaxCallbacksPerFrame` | 5 | 完成回调个数上限 |
| `InstantiateBudgetMs` | 3 | Instantiate 时间预算 |
| `MaxInstantiatesPerFrame` | 3 | Instantiate 个数上限 |
| `MaxUnloadPerFrame` | 1 | Unload 启动上限 |
| `UnloadOnlyWhenIdle` | true | 仅当 Load/Instantiate 队列空时才 Unload |

## 生命周期与释放

| 操作 | API |
|------|-----|
| 单次加载释放 | `using var handle = ...` 或 `handle.Dispose()` |
| 通用 bytes 缓存释放 | `ResourceManager.Instance.ReleaseCache()` |
| 卸载未使用资源 | `ResourceManager.Instance.RequestUnloadUnusedAssets()` |
| 配置 TextAsset 缓存 | `ConfigManager.ReleaseTableAssetCache()` |
| 模块/应用关闭 | `ResourceModule.Shutdown()` → 取消调度队列 → `ResourceManager.Shutdown()` |

Shutdown 时 Pending / InFlight 请求会被 **Cancelled**，协程等待会结束；`onComplete` 收到无效句柄或 null。

**禁止**在业务代码中直接调用 YooAsset 的 `Release` / `Destroy` / `UnloadUnusedAssets`。

## 运行模式

| 模式 | 场景 |
|------|------|
| `EditorSimulateMode` | Editor 下调试，无需打真实 Bundle |
| `OfflinePlayMode` | 本地离线包 |
| `HostPlayMode` | 热更新 CDN 模式 |

## Bootstrap 集成

```csharp
new ResourceModule(),
```

初始化选项与调度预算都在 `ResourceManager` Inspector 上配置。

## 被谁使用

- `Framework.Config` — `LoadAssetSync<TextAsset>`，不走调度
- `Framework.UI` — `Show` 同步旁路；`ShowAsync` 走调度
- `Framework.Audio` — `PlayBgmAsync` 等走 `LoadAssetAsync`
- `Assets/Scripts/Launch.cs` — 注册 `ResourceModule`
