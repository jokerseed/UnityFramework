# Framework.Config

Luban 配置表运行时缓存；**加载走 `ResourceManager.LoadLubanTables()`**，本模块只持有 `Tables`。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Config` |
| 命名空间 | `Framework.Config` |
| 依赖 | `Framework.Bootstrap`、`Framework.Core`、`Framework.Res`、`Framework.Logging`、`Generated.Luban`、`Luban.Runtime` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `ConfigModule` | `IGameModule`：`Tables = ResourceManager.Instance.LoadLubanTables()` |
| `ConfigService` | `Tables` 静态访问；Editor 下 `LoadEditorDefault()` |
| `ConfigLoader` | **仅 Editor** 直读 `Assets/Bundles/Configs/*.bytes` |
| `ConfigPaths` | bin 目录路径常量 |

## 运行时加载（推荐）

```csharp
// Launch 初始化后
var tables = ConfigService.Tables;
// 或
var tables = ConfigModule.Instance.Tables;

// 或直接走 Res（与 ConfigModule 相同）
var tables = ResourceManager.Instance.LoadLubanTables();
```

## Editor 调试（未打 Bundle）

```csharp
#if UNITY_EDITOR
var tables = ConfigService.LoadEditorDefault();
#endif
```

## 战斗表装配

Luban → GAS 装配在 **`Framework.GamePlay.Data`**（见 [GamePlay/README.md](../GamePlay/README.md)）。

## Bootstrap

```
ResourceModule → ConfigModule → GamePlayModule
```
