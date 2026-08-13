# Framework.Res

YooAsset 资源管线封装：加载、释放、反序列化统一入口。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Res` |
| 命名空间 | `Framework.Res` |
| 依赖 | `Framework.Bootstrap`、`Framework.Core`、`Framework.Logging`、`Generated.Luban`、`Luban.Runtime`、`YooAsset` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `ResourceModule` | `IGameModule` 实现，初始化 YooAsset 并注册 `ResourceManager` |
| `ResourceManager` | 常驻单例，包初始化、加载、释放、Luban 表加载 |
| `ResourceInitOptions` | 包名、运行模式（EditorSimulate / Offline / Host） |
| `ResourceAddresses` | 寻址规则（如 `bundles/configs/{表名}.unity3d`、`bundles/scenes/{场景}.unity3d`） |
| `ResourceAssetHandle` | 资源句柄封装，支持 `Dispose` / `InstantiateSync` |
| `ResourceSceneHandle` | 场景句柄封装，支持状态查询 / `UnloadAsync` |

## 加载 API（推荐用法）

```csharp
var res = ResourceManager.Instance;

// Luban 全表：业务侧经 ConfigManager 按需加载（内部调用本接口）
Tables tables = ConfigManager.Instance.LoadTables();
// 或直接：
// Tables tables = res.LoadLubanTables();

// 任意二进制 + 反序列化
MyData data = res.LoadBinary("bundles/data/foo.unity3d", bytes => MyCodec.Decode(bytes));

// 原始 bytes
byte[] raw = res.LoadBytes("bundles/data/foo.unity3d");

// Unity 资源
using var handle = res.LoadAssetSync<GameObject>("bundles/prefabs/player");
var prefab = handle.GetAsset<GameObject>();

// 场景（协程）
yield return res.LoadSceneAsync(ResourceAddresses.BattleScene);
```

## 生命周期与释放

| 操作 | API |
|------|-----|
| 单次加载释放 | `using var handle = ...` 或 `handle.Dispose()` |
| 配置/Luban 缓存释放 | `ResourceManager.Instance.ReleaseCache()` |
| 模块/应用关闭 | `ResourceModule.Shutdown()` → `ResourceManager.Shutdown()` |

**禁止**在业务代码中直接调用 YooAsset 的 `Release` / `Destroy` / `Unload`。

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

初始化选项在 `ResourceManager.InitOptions`（Inspector）上配置。

## 被谁使用

- `Framework.Config` — `ConfigManager.LoadTables()` 内部调用 `LoadLubanTables()`
- `Assets/Scripts/Launch.cs` — 注册 `ResourceModule`
