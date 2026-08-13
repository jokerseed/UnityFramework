# Framework.Config

Luban 配置表的按需加载与缓存。**读表不在模块初始化时发生**，由业务通过 `ConfigManager` 触发。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Config` |
| 命名空间 | `Framework.Config` |
| 依赖 | `Framework.Bootstrap`、`Framework.Core`、`Framework.Res`、`Framework.Logging`、`Generated.Luban`、`Luban.Runtime` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `ConfigModule` | `IGameModule`：仅确保 `ConfigManager` 就绪，**不读表** |
| `ConfigManager` | `PersistentSingleton`：`LoadTables` / 缓存 / `UnloadTables` / Shutdown |
| `ConfigLoader` | **仅 Editor** 直读 `Assets/Bundles/Configs/*.bytes` |
| `ConfigPaths` | bin 目录路径常量 |

## 生成代码前缀

见 `Config/Luban/codegen.json` 与 `.cursor/rules/framework-luban.mdc`：bean/enum/table 类型名（文件名）须带可配置前缀（默认 `Cfg`）。

## 运行时用法

```csharp
// 首次需要时加载并缓存（内部走 ResourceManager.LoadLubanTables）
var tables = ConfigManager.Instance.LoadTables();

// 之后可读缓存
var cached = ConfigManager.Instance.Tables;   // 未加载则为 null
var again = ConfigManager.Instance.GetTables(); // 未加载则自动 LoadTables
```

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
