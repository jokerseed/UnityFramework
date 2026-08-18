# Game

业务入口程序集：启动编排、主界面、战斗演示场景。引用 `Framework.*`，**不被**框架程序集反向引用。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Game` |
| 命名空间 | `Game` |
| 依赖 | `Framework.Bootstrap`、`Config`、`Core`、`Coroutine`、`ECS`、`Events`、`GamePlay`、`GAS`、`Logging`、`MemoryPool`、`ObjectPool`、`Res`、`UI`、`Generated.Luban` |

## 入口

| 类型 | 场景 | 职责 |
|------|------|------|
| `Launch` | `Bundles/Scenes/Launch.unity` | `GameBootstrap.SetModules` / `Run`，`ShowAsync` 打开主界面 |
| `MainUIWindow` | Launch 流程中打开 | 「进入游戏」→ `LoadMainSceneAsync` 进 Battle |
| `BattleBootstrap` | `Bundles/Scenes/Battle.unity` | 异步 Preload 模型（Scheduler + Scope）、杂兵 `BattleMonsterView` 池、12 槽刷波 |

框架模块不要引用本程序集；演示与产品业务都从这里往下接 `Framework.*`。
