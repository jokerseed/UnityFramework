# Framework.Config

Luban 配置表的按需加载与缓存。**读表不在模块初始化时发生**，由业务通过 `ConfigManager` 触发。

框架不依赖任何生成代码：表聚合类型（例如样例里的 `cfg.CfgTables`）由游戏在启动时注册，框架只按泛型读取。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Config` |
| 命名空间 | `Framework.Config` |
| 依赖 | `Framework.Core`、`Framework.Res`、`Framework.Logging`、`Luban.Runtime` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `ConfigModule` | `IGameModule`：确保 `ConfigManager` 就绪并注册表工厂，**不读表** |
| `ConfigManager` | `PersistentSingleton`：`RegisterTables` / `LoadTables<T>` / `GetTables<T>` / `CreateTables<T>` / `LoadConfigBytes` / `ReloadTables` / `Shutdown` |
| `ConfigLoader` | **仅 Editor** 直读 bin 目录下的 `*.bytes` |
| `ConfigPaths` | bin 目录路径常量 |

## 生成代码前缀

见 `Config/Luban/codegen.json` 与 `.cursor/rules/framework-luban.mdc`：bean/enum/table 类型名（文件名）须带可配置前缀（默认 `Cfg`）。

## 注册

```csharp
// 启动模块列表里用工厂创建 ConfigModule
ConfigModule.Create(loader => new cfg.CfgTables(loader));

// 或者自己注册（须在第一次读表前）
ConfigManager.Instance.RegisterTables(loader => new cfg.CfgTables(loader));
```

同一进程只能注册一种表类型；重复注册同类型会替换工厂并丢弃缓存。

## 运行时用法

```csharp
// 首次需要时加载并缓存
var tables = ConfigManager.Instance.LoadTables<cfg.CfgTables>();

// 已缓存时直接返回，未缓存则加载
var again = ConfigManager.Instance.GetTables<cfg.CfgTables>();

// 只看缓存，不触发加载
if (ConfigManager.Instance.TryGetTables(out cfg.CfgTables cached)) { }

// 新建一份独立副本，不写缓存
var copy = ConfigManager.Instance.CreateTables<cfg.CfgTables>();

// 单表 bytes（cache=true 时句柄由 ConfigManager 管理）
var bytes = ConfigManager.Instance.LoadConfigBytes("tbability", cache: true);
```

### 异步预热（WebGL / 小游戏）

`LoadTables` 内部按表同步读包。不允许同步加载的平台先异步预热 TextAsset 缓存，再 `LoadTables`（默认 `cacheTableAssets=true`）就只读缓存：

```csharp
await ConfigManager.Instance.PreloadTableAssetsAsync(new[] { "tbability", "tbeffect" }, ct);
var tables = ConfigManager.Instance.LoadTables<cfg.CfgTables>();
```

表名与 Luban 生成代码里 loader 收到的文件名一致；漏掉的表仍会走同步加载。

## 热重载

`ReloadTables()` 丢弃表对象和 TextAsset 句柄后重新加载，并触发 `TablesReloaded`。持有旧表引用的业务（技能目录、效果目录等）要订阅该事件自行替换。

## 释放

| 操作 | API |
|------|-----|
| 丢弃表对象 | `UnloadTables()` |
| 释放配置 TextAsset 句柄 | `ReleaseTableAssetCache()` 或 `Shutdown()` |

底层通过 `ResourceManager.LoadAssetSync<TextAsset>` 读 Bundle；**不在 Res 层暴露 Luban API**。

## Editor 调试（未打 Bundle）

```csharp
#if UNITY_EDITOR
var tables = ConfigManager.Instance.LoadEditorDefault<cfg.CfgTables>();
#endif
```

## Bootstrap

```
ResourceModule → ConfigModule → GamePlayModule
```

`ConfigModule.Initialize` 只创建 Manager 并注册工厂；进战斗等业务再调用 `LoadTables<T>()`。
