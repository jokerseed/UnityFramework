# Framework.Res

YooAsset 资源管线封装：**通用**加载、释放、反序列化统一入口。Luban 配置表见 `Framework.Config`。

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
| `ResourceManager` | 常驻单例，包初始化、Asset/Scene/bytes 加载与释放 |
| `ResourceInitOptions` | 包名、运行模式（EditorSimulate / Offline / Host） |
| `ResourceAddresses` | 寻址规则（配置表 / 场景 / **行为树** `BehaviourTree(treeId)` 等） |
| `ResourceAssetHandle` | 资源句柄封装，支持 `Dispose` / `InstantiateSync` |
| `ResourceSceneHandle` | 场景句柄封装，支持状态查询 / `UnloadAsync` |

## 加载 API（推荐用法）

```csharp
var res = ResourceManager.Instance;

// Luban 全表 — 走 ConfigManager，不在 Res 层加载
CfgTables tables = ConfigManager.Instance.LoadTables();

// 任意二进制 + 反序列化
MyData data = res.LoadBinary("bundles/data/foo.unity3d", bytes => MyCodec.Decode(bytes));

// 原始 bytes（cache=true 时进 ResourceManager 通用缓存）
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
| 通用 bytes 缓存释放 | `ResourceManager.Instance.ReleaseCache()` |
| 配置 TextAsset 缓存 | `ConfigManager.ReleaseTableAssetCache()`（见 Config README） |
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

- `Framework.Config` — 底层 `LoadAssetSync<TextAsset>` 读配置 Bundle
- `Assets/Scripts/Launch.cs` — 注册 `ResourceModule`
