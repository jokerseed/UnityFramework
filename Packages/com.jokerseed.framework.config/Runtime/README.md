# Framework.Config

Luban 配置表的按需加载与缓存。**读表不在模块初始化时发生**，由业务通过 `ConfigManager` 触发。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Config` |
| 命名空间 | `Framework.Config` |
| 依赖 | `Framework.Core`、`Framework.Res`、`Framework.Logging`、`Generated.Luban`、`Luban.Runtime` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `ConfigModule` | `IGameModule`：仅确保 `ConfigManager` 就绪，**不读表** |
| `ConfigManager` | `PersistentSingleton`：`LoadTables` / `LoadLubanTables` / `LoadConfigBytes` / 缓存 / `Shutdown` |
| `ConfigLoader` | **仅 Editor** 直读 `Assets/Bundles/Configs/*.bytes` |
| `ConfigPaths` | bin 目录路径常量 |

## 生成代码前缀

见 `Config/Luban/codegen.json` 与 `.cursor/rules/framework-luban.mdc`：bean/enum/table 类型名（文件名）须带可配置前缀（默认 `Cfg`）。

## 运行时用法

```csharp
// 首次需要时加载并缓存 CfgTables
var tables = ConfigManager.Instance.LoadTables();

// 单表 bytes（cache=true 时句柄由 ConfigManager 管理）
var bytes = ConfigManager.Instance.LoadConfigBytes("tbability", cache: true);

// 仅要 CfgTables 对象、不走 Manager 单例缓存
var tablesOnly = ConfigManager.Instance.LoadLubanTables();

// 之后可读缓存
var cached = ConfigManager.Instance.Tables;   // 未加载则为 null
var again = ConfigManager.Instance.GetTables(); // 未加载则自动 LoadTables
```

## 释放

| 操作 | API |
|------|-----|
| 丢弃 CfgTables 对象 | `UnloadTables()` |
| 释放配置 TextAsset 句柄 | `ReleaseTableAssetCache()` 或 `Shutdown()` |

底层通过 `ResourceManager.LoadAssetSync<TextAsset>` 读 Bundle；**不在 Res 层暴露 Luban API**。

## Editor 调试（未打 Bundle）

```csharp
#if UNITY_EDITOR
var tables = ConfigManager.Instance.LoadEditorDefault();
#endif
```

## Bootstrap

```
ResourceModule → ConfigModule → GamePlayModule
```

`ConfigModule.Initialize` 只创建 Manager；进战斗等业务再 `LoadTables()`。
