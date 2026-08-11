# Framework.Logging

统一日志输出：级别过滤、分类开关、自定义格式化与 Sink。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Logging` |
| 命名空间 | `Framework.Logging` |
| 依赖 | `Framework.Bootstrap`、`Framework.Core` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `LoggingModule` | `IGameModule`，位于 `Framework.Bootstrap` 程序集 |
| `GameLog` | 全局静态入口（级别 / 分类 / Sink / 格式化） |
| `ILogSink` | 自定义输出目标 |
| `UnityConsoleLogSink` | 默认输出到 Unity Console |
| `LogInitOptions` | Inspector 可配的初始化选项 |
| `LogCategories` | 常用分类常量 |

## Bootstrap 集成

`LoggingModule` 应在 `ResourceModule` 之前注册（同 `Infrastructure` 阶段按名称排序）：

```csharp
bootstrap.SetModules("launch", new IGameModule[]
{
    new LoggingModule(_logOptions),
    new ResourceModule(_resourceOptions),
});
```

## 典型用法

```csharp
using Framework.Logging;

GameLog.Info(LogCategories.Resource, "Package ready.");
GameLog.Warning(LogCategories.Launch, "Config not loaded.");
GameLog.Error(LogCategories.Bootstrap, "Init failed.");

try { /* ... */ }
catch (Exception ex)
{
    GameLog.Exception(LogCategories.Bootstrap, ex);
}
```

## 运行时控制

```csharp
GameLog.SetMinLevel(LogLevel.Warning);           // 全局最低级别
GameLog.SetCategoryEnabled("GAS", false);      // 关闭某分类
GameLog.SetCategoryMinLevel("ECS", LogLevel.Error);

// 自定义格式
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

- 未 `Configure` 时 `GameLog` 不输出（避免 Bootstrap 之前误用）
- `Shutdown` 时清空 Sink 与过滤规则
- 业务模块通过 `GameLog` 输出，避免散落 `Debug.Log`
