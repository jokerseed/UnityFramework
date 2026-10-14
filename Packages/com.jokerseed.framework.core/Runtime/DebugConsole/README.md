# Framework.DebugConsole

运行时调试控制台（IMGUI，不依赖 uGUI）：真机上看日志、看堆栈、执行调试命令。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.DebugConsole` |
| 命名空间 | `Framework.DebugConsole` |
| 依赖 | `Framework.Core`、`Framework.Logging` |

## 用法

```csharp
new DebugConsoleModule(),                    // Editor / Development Build 才创建
new DebugConsoleModule(enableInRelease: true),  // 内测包也要

RuntimeConsole.Instance.Commands.Register("gold", "gold <n> 加金币", args =>
{
    Player.Gold += int.Parse(args[0]);
    return $"gold = {Player.Gold}";
});
```

- 打开：左上角 `Console` 按钮（有错误时显示 `!`），或旧输入系统下按 `` ` ``
- 日志：按 Log / Warn / Error 过滤，点一行看完整内容和堆栈；默认保留 1000 条（`Capacity`）
- 命令：`help` 列出全部命令，`clear` 清空；上下方向键翻历史；双引号包住带空格的参数
- 日志通过 `Application.logMessageReceivedThreaded` 收集，子线程日志也能看到
- `DebugCommandRegistry` 与界面无关，可以单独接到别的终端（如远程调试）
