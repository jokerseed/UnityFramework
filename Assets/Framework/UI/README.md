# Framework.UI

参考 [TEngine UIModule](https://github.com/Alex-Rachel/TEngine) 的轻量 UI 框架：纯 C# 驱动、窗口栈、层级管理、资源自动释放。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.UI` |
| 命名空间 | `Framework.UI` |
| 依赖 | `Framework.Core`、`Framework.Logging`、`Framework.Res`、`Framework.Coroutine`、`Framework.Events` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `UIManager` | 打开/关闭窗口、层级栈、全屏遮挡、资源句柄生命周期 |
| `UIShowHandle` | `ShowAsync` 返回值：`Cancel` / `IsRunning` / `IsCancelled` |
| `UIBase` | UI 基类与生命周期（`OnCreate` / `OnRefresh` / `OnUpdate` / `OnDestroy`） |
| `UIWindow` | 窗口级 UI |
| `UIWindowAttribute` | 层级 / 全屏 / 寻址 / 释放策略 |
| `UIReleasePolicy` | 关闭时 Destroy / Hide / Cache / 延迟卸载 |
| `UIModule` | Bootstrap 模块入口 |

## 生命周期

```
Show → OnCreate → ScriptGenerator → BindMemberProperty → RegisterEvent → OnRefresh
每帧 OnUpdate（可见窗口）
Close → 按 UIReleasePolicy 处理（Destroy / 缓存 / 延迟卸载）
ForceDestroy → 忽略策略，立即 Destroy + Dispose
```

## 典型用法

```csharp
using Framework.Res;
using Framework.UI;
using UnityEngine.UI;

[UIWindow(UILayer.UI, fullScreen: true, location: ResourceAddresses.MainPrefab)]
public sealed class SettingsUIWindow : UIWindow { }

// 主界面：关后隐藏 60s，期间再开直接复用；超时自动卸资源
[UIWindow(
    UILayer.UI,
    fullScreen: true,
    location: ResourceAddresses.MainPrefab,
    releasePolicy: UIReleasePolicy.HideAndDelayUnload,
    delayUnloadSeconds: 60f)]
public sealed class MainUIWindow : UIWindow
{
    Button _startButton;

    public override void ScriptGenerator()
    {
        _startButton = FindChildComponent<Button>("Button");
        if (_startButton != null)
        {
            _startButton.onClick.AddListener(OnClickStart);
        }
    }

    void OnClickStart()
    {
        GameLog.Info(LogCategories.Launch, "Start clicked");
    }

    protected override void OnDestroy()
    {
        if (_startButton != null)
        {
            _startButton.onClick.RemoveListener(OnClickStart);
        }
    }
}

// 模块就绪后
UIManager.Instance.Show<MainUIWindow>();

// 异步打开；失败或取消时 window 为 null
var show = UIManager.Instance.ShowAsync<MainUIWindow>(onComplete: window => { /* ... */ });
show.Cancel(); // 或 show.Stop() / Dispose()，释放未完成的加载与实例

// Close / CloseAll / 再次 Show(Async) 同类型时，会取消进行中的异步打开
```

## 资源寻址

默认规则（`UIPaths.Window<T>()`）：

```
MainUIWindow → bundles/ui/mainuiwindow.unity3d
```

可通过 `[UIWindow(..., location: "...")]` 覆盖。

## 释放策略

在 `[UIWindow(..., releasePolicy: ..., delayUnloadSeconds: ...)]` 声明：

| 策略 | Close 行为 | 再次 Show | `IsOpen` / `IsCached` |
|------|------------|-----------|------------------------|
| `DestroyImmediate`（默认） | Destroy 实例 + Dispose Handle | 重新 Load | 均 false |
| `HideOnly` | 隐藏并移出栈，保留实例与 Handle | 走已打开分支，`OnRefresh` | `IsOpen` true；不进缓存 |
| `Cached` | 移出栈并缓存 | `TryReviveCached` 快速复用 | `IsOpen` false；`IsCached` true |
| `HideAndDelayUnload` | 先缓存；超时未再打开则 Destroy + Dispose | 超时前 `TryReviveCached`；超时后重新 Load | 等待中 `IsCached` true |

`ForceDestroy<T>()` 可无视策略立即销毁（含缓存）。`Shutdown()` 销毁全部窗口与缓存。

## Bootstrap

```csharp
new ResourceModule(),
new UIModule(),      // Dependencies: Logging + Resource + Coroutine
new ConfigModule(),
```

## 设计说明

- **纯 C#**：窗口逻辑不挂 MonoBehaviour，由 `UIManager.Update` 驱动
- **五层 Canvas**：`Bottom` / `UI` / `Top` / `Tips` / `System`
- **全屏遮挡**：`fullScreen: true` 时隐藏其下方的已打开窗口（不销毁）
- **事件绑定**：`AddUIEvent<T>` 在窗口销毁时自动取消订阅
- **资源管理**：按 `UIReleasePolicy` 释放；`HideOnly` 关后仍算已打开，`Cached` / `HideAndDelayUnload` 关后进缓存由 `TryReviveCached` 复用
- **异步取消**：`ShowAsync` 返回 `UIShowHandle`；`Cancel` 会停协程、取消 Res 调度请求，并销毁未绑定的实例
