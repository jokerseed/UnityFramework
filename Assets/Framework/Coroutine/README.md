# Framework.Coroutine

基于 Unity 原生 `IEnumerator` 的协程模块。静态入口 `GameCoroutine`，支持全局 / 场景 / GameObject 三种生命周期。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Coroutine` |
| 命名空间 | `Framework.Coroutine` |
| 依赖 | `Framework.Core`、`Bootstrap`、`Logging` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `GameCoroutine` | 静态 `Start*` / `Stop*` 入口 |
| `CoroutineManager` | 常驻单例，管理各作用域宿主 |
| `ICoroutineHandle` | `IsRunning` / `Stop` / `Dispose` |
| `CoroutineScope` | `Global` / `Scene` |
| `CoroutineBehaviour` | 挂在业务 GO 上的宿主组件 |
| `CoroutineModule` | Bootstrap 模块 |

## 生命周期

| 方式 | API | 终止时机 |
|------|-----|----------|
| 全局 | `StartGlobal` | 手动 Stop / `StopAllGlobal` / 模块 Shutdown |
| 场景 | `StartScene` | 场景卸载、手动 Stop / `StopAllScene` |
| GameObject | `Start(go, …)` | GO 销毁、手动 Stop / `StopAll(go)` |

## 用法

```csharp
using Framework.Coroutine;
using System.Collections;
using UnityEngine;

// 全局（跨场景）
var h1 = GameCoroutine.StartGlobal(FadeIn());

// 当前场景（切场景自动停）
var h2 = GameCoroutine.StartScene(WaveLoop());

// 绑定 GameObject
var h3 = GameCoroutine.Start(gameObject, LocalFx());

GameCoroutine.Stop(h1);
h2.Dispose(); // 等同 Stop

GameCoroutine.StopAllGlobal();
GameCoroutine.StopAllScene();
GameCoroutine.StopAll(gameObject);

IEnumerator FadeIn()
{
    yield return new WaitForSeconds(0.5f);
}
```

## Bootstrap

```csharp
new LoggingModule(...),
new CoroutineModule(), // Dependencies: LoggingModule
```

须先 `ObjectPoolModule` 无强依赖；协程模块仅依赖 Logging。`Launch` 已在 Logging 之后注册。

## 规范（强制）

自有代码（`Assets/Framework`、`Assets/Scripts`）的协程**生命周期控制只能**走本模块：

- 用 `GameCoroutine.Start*` / `Stop*` / `ICoroutineHandle`
- Unity `StartCoroutine` / `StopCoroutine` / `StopAllCoroutines` **仅允许** `Framework.Coroutine` 与 `Framework.Bootstrap` 调用
- 详见 `.cursor/rules/framework-coroutine.mdc`
