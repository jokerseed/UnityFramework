# Framework.Res

YooAsset 资源管线封装，提供配置表与通用资源的同步/异步加载。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Res` |
| 命名空间 | `Framework.Res` |
| 依赖 | `Framework.Bootstrap`、`YooAsset` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `ResourceModule` | `IGameModule` 实现，初始化 YooAsset 并注册 `ResourceManager` |
| `ResourceManager` | 常驻单例，Inspector 配置 `InitOptions`，包初始化与加载 |
| `ResourceInitOptions` | 包名、运行模式（EditorSimulate / Offline / Host），挂在 `ResourceManager` 上 |
| `ResourceAddresses` | 寻址规则（如 `bundles/configs/{表名}.unity3d`） |
| `ResourceAssetHandle` | 资源句柄封装，支持 Dispose |

## 运行模式

| 模式 | 场景 |
|------|------|
| `EditorSimulateMode` | Editor 下调试，无需打真实 Bundle |
| `OfflinePlayMode` | 本地离线包 |
| `HostPlayMode` | 热更新 CDN 模式 |

非 Editor 环境下 `ResourceModule` 会自动将 `EditorSimulateMode` 降级为 `OfflinePlayMode`。

## YooAsset 工作流

| 步骤 | 菜单 |
|------|------|
| 生成 Collector | **Tools → YooAsset → Generate Collector** |
| 构建 Bundle | **YooAsset → Bundle Builder** |

Collector 规则：
- `Assets/Bundles/Configs/` 下每个 `.bytes` 单独打包
- 寻址：`bundles/configs/{表名}.unity3d`

## 典型用法

```csharp
// ResourceModule 初始化后
var manager = ResourceManager.Instance;

// 加载配置 bytes
var tables = BattleConfigBootstrap.LoadTables(manager);

// 加载任意资源
using var handle = manager.LoadAssetSync<GameObject>("path/to/prefab");
var prefab = handle.GetAsset<GameObject>();
```

## Bootstrap 集成

```csharp
new ResourceModule(),
```

初始化选项在常驻 `ResourceManager`（`PersistentSingleton`）上配置，不要写在 `Launch` 上。
- `Phase` = `Infrastructure`
- `Dependencies` = `LoggingModule`
- 初始化后通过 `ResourceManager.Instance` 访问
- `Shutdown` / 停 Play 时销毁 YooAsset，避免编辑器 abort 未完成异步任务的 Warning

## 被谁使用

- `Framework.Config` — `ConfigModule` 依赖 `ResourceModule` 加载 Luban bytes
- `Assets/Scripts/Launch.cs` — 注册 `ResourceModule`
