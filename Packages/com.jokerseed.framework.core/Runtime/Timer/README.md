# Framework.Timer

表现层定时器：延时、重复、下一帧回调。适合 UI 倒计时、提示自动关闭、轮询等；**帧同步逻辑不要用**（依赖 Unity 帧时间）。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Timer` |
| 命名空间 | `Framework.Timer` |
| 依赖 | `Framework.Core`、`Framework.Logging` |

## 用法

```csharp
var timers = TimerManager.Instance;

// 2 秒后执行；owner 被销毁时自动取消
var close = timers.Delay(2f, Hide, owner: this);

// 每秒一次，共 10 次；暂停菜单期间仍走（非缩放时间）
var countdown = timers.Repeat(1f, Tick, count: 10, unscaled: true);

timers.NextFrame(RefreshLayout);

timers.Pause(countdown);
timers.Resume(countdown);
timers.Cancel(close);
```

## 规则

- 新建的定时器从下一帧开始计时，不会吃到创建当帧的 `deltaTime`
- 一帧最多触发同一定时器一次；掉帧时不补发
- `TimerHandle` 带版本号：定时器结束或取消后，旧句柄的 `Cancel` / `Pause` 不会误伤复用同一槽位的新定时器
- 回调抛异常只记日志；抛异常的重复定时器会被取消
- `TimerModule.Shutdown` 取消全部定时器
