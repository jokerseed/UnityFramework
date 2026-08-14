# Framework.Logging

统一日志输出：级别过滤、分类开关、自定义格式化与 Sink。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Logging` |
| 命名空间 | `Framework.Logging` |
| 依赖 | `Framework.Core` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `LoggingModule` | `IGameModule` 实现（本程序集） |
| `LoggingManager` | 常驻单例，Inspector 配置 `LogInitOptions` |
| `GameLog` | 全局静态入口（级别 / 分类 / Sink / 格式化） |
| `ILogSink` | 自定义输出目标 |
| `UnityConsoleLogSink` | 默认输出到 Unity Console |
| `LogInitOptions` | Inspector 可配的初始化选项（挂在 `LoggingManager` 上） |
| `LogStyle` | Console 富文本：级别/分类颜色，正文 `Name` / `Value` / `Ok` / `Fail` |
| `LogCategories` | 常用分类常量 |

## Bootstrap 集成

`LoggingModule` 应在 `ResourceModule` 之前注册。初始化选项在常驻 `LoggingManager`（`PersistentSingleton`）上配置，不要写在 `Launch` 上：

```csharp
bootstrap.SetModules("launch", new IGameModule[]
{
    new LoggingModule(),
    new ResourceModule(),
});
```

## 典型用法

```csharp
using Framework.Logging;

GameLog.Info(LogCategories.Resource, $"Package {LogStyle.Ok("ready")}: {LogStyle.Name("DefaultPackage")}");
GameLog.Warning(LogCategories.Launch, $"Config {LogStyle.Fail("not loaded")}");
GameLog.Error(LogCategories.Bootstrap, $"Init {LogStyle.Fail("failed")}");

try { /* ... */ }
catch (Exception ex)
{
    GameLog.Exception(LogCategories.Bootstrap, ex);
}
```

默认 Console 行结构（Unity 富文本）：

```text
14:27:01.235 │ INFO │ Resource │ Package ready: DefaultPackage  version=Simulate
^灰色时间     ^级别色  ^分类色    ^正文；重点用 LogStyle 加粗高亮
```

`LoggingManager` 上可关 `UseRichText`，输出变为纯文本 `时间 | INFO | Resource | ...`。

## 运行时控制

```csharp
GameLog.SetMinLevel(LogLevel.Warning);           // 全局最低级别
GameLog.SetCategoryEnabled("GAS", false);      // 关闭某分类
GameLog.SetCategoryMinLevel("ECS", LogLevel.Error);

// 自定义格式（覆盖默认富文本）
GameLog.Formatter = entry => $"{entry.UtcTime:HH:mm:ss} [{entry.Level}] {entry.Message}";
```

## 自定义 Sink

```csharp
public sealed class FileLogSink : ILogSink
{
    public void Write(in LogEntry entry)
    {
        // 写入文件或上报服务
    }
}

GameLog.AddSink(new FileLogSink());
```

## 设计说明

- 未 `Configure` 时 `GameLog` 仍可用 fallback Sink（便于 Bootstrap 首轮与 Editor 工具）
- `Shutdown` 时清空 Sink 与过滤规则
- 业务模块通过 `GameLog` 输出，避免散落 `Debug.Log`
- 重点内容用 `LogStyle.Name` / `Value` / `Ok` / `Fail`，不要把关键 ID 埋在纯字符串里
