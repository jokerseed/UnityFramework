# Game

`Assets/Scripts` 是本项目的业务入口层，负责把 `Framework.*` 提供的通用能力装配成一个可运行的 Demo 流程。当前这层代码主要覆盖三件事：

1. 启动模块组并拉起主界面。
2. 从主界面切到战斗场景，并为该次战斗创建独立会话。
3. 在战斗场景里完成输入采样、逻辑推进、表现同步与资源回收。

这层代码**只依赖框架，不被框架反向引用**。如果以后要接正式玩法，通常也是在这里继续加业务流程、场景编排、UI 入口与表现脚本，而不是把业务逻辑塞回 `Framework.*`。

## 程序集信息

| 项 | 值 |
|---|---|
| 程序集 | `Game` |
| 命名空间 | `Game` |
| 角色 | 业务入口、场景流程、UI 入口、战斗演示装配 |
| 依赖 | `Framework.Bootstrap`、`Framework.Config`、`Framework.Core`、`Framework.Coroutine`、`Framework.ECS`、`Framework.Events`、`Framework.GamePlay`、`Framework.GAS`、`Framework.Logging`、`Framework.MemoryPool`、`Framework.ObjectPool`、`Framework.Res`、`Framework.UI`、`Generated.Luban` |

## 目录结构

```text
Assets/Scripts
├── Launch.cs                      启动入口，配置并运行模块组
├── README.md                      本文档
├── Flow/
│   └── GameSceneFlow.cs           全局场景流程
├── UI/
│   └── MainUIWindow.cs            首页窗口
└── Battle/
    ├── BattleBootstrap.cs         战斗场景装配器
    ├── BattleSetup.cs             战斗启动与波次
    ├── BattleInputFrame.cs        输入快照
    ├── BattleInputController.cs   输入采样与应用
    ├── BattleViewBinder.cs        View 同步与插值
    ├── BattlePresentation.cs      事件表现与 HitStop
    ├── BattleCuePresenter.cs      Cue -> 简易可视化
    ├── BattleMonsterView.cs       杂兵对象池 View
    └── Flow/
        ├── BattleFlow.cs          战斗流程 / Session 持有
        └── BattleFlowState.cs     战斗流程状态
```

## 整体运行链路

当前业务层的主链路如下：

```text
Launch 场景
  -> Launch.Awake 配模块
  -> Launch.Start 运行模块组
  -> GameSceneFlow.ShowMainPage
  -> MainUIWindow 点击“进入游戏”
  -> GameSceneFlow.EnterBattle
  -> ResourceManager.LoadMainSceneAsync(Battle)
  -> BattleBootstrap.StartBattleAsync
  -> BattleFlow.TryEnter 创建 BattleSession
  -> BattleSetup.LoadViewsAsync / CreateActors
  -> BattleInputController + BattleSession.Tick + BattleViewBinder
```

可以把它理解成两层：

- **流程层**：`Launch`、`GameSceneFlow`、`BattleFlow`
- **战斗场景层**：`BattleBootstrap` 及其拆分出的 `Setup / Input / Presentation / View`

前者决定“什么时候进入哪一个场景、持有哪些流程对象”，后者决定“战斗场景里每帧做什么”。

## 启动阶段

### `Launch`

`Launch` 挂在 `Assets/Bundles/Scenes/Launch.unity` 中，是业务代码真正的起点。

职责：

- 通过 `GameBootstrap.SetModules("launch", ...)` 声明启动模块组。
- 把 `Logging`、`Coroutine`、`Resource`、`UI`、`Config`、`GamePlay` 等框架模块按统一入口启动。
- 监听模块组状态变更、Ready、Failed、Progress 日志。
- 在 `launch` 组 Ready 后调用 `GameSceneFlow.ShowMainPage()` 打开首页。

这里的设计重点是：**业务层不直接 new 各种 Manager，而是通过模块组统一初始化底层能力**。这样以后无论是换场景、扩启动顺序，还是增加新的系统模块，都从这里接。

## 主界面与场景流程

### `MainUIWindow`

`MainUIWindow` 是 Launch 阶段展示的首页窗口，资源地址来自 `ResourceAddresses.MainPrefab`。它当前只做一件事：

- 点击“进入游戏”按钮后关闭自己，然后调用 `GameSceneFlow.Instance.EnterBattle()`。

窗口使用 `UIReleasePolicy.HideAndDelayUnload`，说明这个界面不是每次关闭都立刻彻底卸载，而是允许短时间缓存，减少频繁开关的资源抖动。

### `GameSceneFlow`

`GameSceneFlow` 是业务层的全局流程单例，职责不是保存战斗细节，而是做**场景级编排**。

当前已实现能力：

- `ShowMainPage()`：打开首页 UI。
- `EnterBattle()`：发起进入战斗场景请求。
- `EnsureBattleFlow()`：保证当前有一个可用的 `BattleFlow`，支持直接在编辑器打开 Battle 场景时自举。
- `ClearBattleFlow()`：当战斗表现层销毁后，清空当前流程引用。
- `RequestUnusedAssetsCleanup()`：在战斗流程退出后请求 `ResourceManager` 分帧执行 `UnloadUnusedAssets`。

`GameSceneFlow` 解决的是“场景与流程对象谁来持有”的问题。战斗逻辑本身不在这里，而是在 `BattleFlow` 和 `BattleSession` 里。

## 战斗流程层

### `BattleFlow`

`BattleFlow` 是**一场战斗的流程外壳**。它不直接跑每帧逻辑，也不关心 MonoBehaviour 表现，而是只负责：

- 检查 `GamePlayModule` 和 `ConfigManager` 是否就绪。
- 触发配置表加载。
- 向 `BattleDirector` 申请一个新的 `BattleSession`。
- 持有该 `BattleSession` 直到退出。
- 在 `Exit()` 时释放 Session，并请求做一次未使用资源清理。

也就是说，`BattleFlow` 是业务场景层和框架玩法层之间的桥。场景表现脚本通过它拿到 `BattleSession`，但不用直接依赖 `GamePlayModule.Instance.Director` 这些底层入口。

### `BattleFlowState`

当前流程状态包括：

- `Idle`：尚未创建会话
- `Preparing`：正在检查依赖并创建会话
- `Running`：会话创建成功，战斗可运行
- `Exited`：流程已退出
- `Failed`：流程准备失败

这个状态机虽然简单，但能把“战斗有没有真正进入可运行态”表达清楚，后续如果加 Loading、结算、退出战斗，也可以继续沿着这层扩。

## 战斗场景装配

### `BattleBootstrap`

`BattleBootstrap` 挂在 `Assets/Bundles/Scenes/Battle.unity` 的常驻节点上，是战斗场景的总装配器。它本身现在已经被压薄，不再承担所有细节，而是统一编排四类子职责：

- `BattleSetup`
- `BattleInputController`
- `BattlePresentation`
- `BattleViewBinder`

启动顺序：

1. `TryEnterBattle()` 通过 `GameSceneFlow.EnsureBattleFlow()` 拿到 `BattleFlow`
2. `BattleFlow.TryEnter()` 创建 `BattleSession`
3. `BattleSetup.LoadViewsAsync()` 加载英雄/怪物模型并注册到 View 层
4. `BattleSetup.CreateActors()` 创建英雄与杂兵 Actor、能力和波次控制
5. `BattlePresentation.Bind()` 订阅事件总线
6. 标记 `_battleStarted = true`，正式进入战斗运行态

每帧顺序：

1. `BattleInputController.Sample()` 在渲染帧采样输入
2. `BattlePresentation.RestoreHitStopIfExpired()` 恢复时间缩放
3. `BattleSession.Tick()` 用固定步长推进逻辑帧
4. 每个逻辑步里调用 `BattleInputController.Apply()` 与 `BattleSetup.TickWaves()`
5. `BattlePresentation.Tick()` 更新持续型 Cue
6. `BattleViewBinder.Sync()` 用插值后的状态更新表现层

清理顺序：

1. 清掉 View
2. 解绑表现层事件
3. `BattleFlow.Exit()` 释放 Session
4. `BattleSetup.TearDown()` 回收对象池等资源

这套顺序的核心思想是：**输入采样和渲染帧绑定，逻辑推进和固定步长绑定，表现同步放在逻辑之后统一做**。

## 战斗子模块说明

### `BattleSetup`

`BattleSetup` 负责“战斗开始前要准备好什么”以及“战斗运行中的波次生成”。

主要职责：

- 定义英雄出生点与怪物出生布局。
- 通过 `BattleSession.Scope` 加载英雄和怪物 Prefab。
- 用 `ResourceManager.InstantiateAsync` 实例化英雄模型。
- 通过 `BattleMonsterView` 对象池批量生成 12 个杂兵视图。
- 创建英雄 Actor、怪物 Actor。
- 给英雄注册 `Fireball / Slash / Slash2 / Slash3 / Dodge`。
- 给怪物注册 `MobSlash` 并挂上 `BattleAiNodes.CreateMeleeChaserAgent(...)`。
- 创建 `BattleWaveDirector` 驱动后续刷波。

这里最重要的边界是：

- **资源加载与实例创建放在 Setup**
- **逻辑 Actor 创建也放在 Setup**
- **但每帧同步显示不放在 Setup，而交给 ViewBinder**

这样就避免“初始化”和“运行中表现”搅在一起。

### `BattleInputFrame`

`BattleInputFrame` 是从渲染帧采样出来的一份输入快照。它把输入从“直接读 Unity Input”变成“可重复消费的一帧命令”。

当前字段包括：

- `MoveDirection`
- `HasMoveInput`
- `TriggerMelee`
- `TriggerFireball`
- `TriggerDodge`
- `AimDirection`

它的意义不只是让代码更整洁，更关键的是：**为帧同步或固定步长逻辑预留了输入命令层**。以后如果接网络同步、录制回放、AI 输入替换，都会从这里继续往下扩。

### `BattleInputController`

`BattleInputController` 分成两个阶段：

- `Sample(deltaTime)`：在渲染帧通过 `Battle.BattleInputActions` 读取输入，生成 `BattleInputFrame`
- `Apply(framework, heroId, ref inputFrame)`：在逻辑步里消费输入快照
- `Enable()` / `Disable()` / `Dispose()`：战斗开始启用 `Battle` Action Map，退出时释放

输入绑定定义在 `Assets/Settings/Input/Game.inputactions`，生成类为 `Assets/Generated/Input/BattleInputActions.cs`。

当前支持的操作（键位可在 Input Actions 资产里改）：

- `WASD`：移动
- `J`：近战三连（带 `ComboBufferSeconds` 输入缓冲）
- `K`：火球
- `Left Shift`：闪避

实现上还处理了几个关键状态：

- 眩晕时不能移动
- 倒地时不能移动
- 闪避标签存在时不再覆盖位移
- 一次性命令在逻辑步消费后会清掉，避免一个渲染帧触发多次

相比最早直接在 `Update()` 里操作 `GamePlayFramework` 的写法，现在的输入层已经更接近“命令化输入”。

### `BattlePresentation`

`BattlePresentation` 负责所有**由事件驱动的表现层逻辑**，当前覆盖：

- 伤害日志输出
- 死亡日志输出
- Gameplay Cue 订阅与转发
- HitStop 触发与恢复

其中 HitStop 的策略是：

- 当英雄造成有效伤害时，把 `Time.timeScale` 临时降到 `0.18`
- 经过 `0.05s` 的 `unscaledTime` 后恢复为 `1`

这类逻辑放在 Presentation 而不是 Setup / Input / View 里，是因为它本质上属于“事件触发的视觉反馈”，而不是玩法规则本身。

### `BattleCuePresenter`

`BattleCuePresenter` 是 `BattlePresentation` 的一个子组件，它把 `GameplayCueEvent` 映射为简单的球体可视化。

当前支持：

- `Add`：创建持续跟随的 Cue 球
- `Remove`：移除持续 Cue
- 其他动作：创建限时 Burst Cue

它会通过 Luban 的 `CfgTbCue` 读取颜色、缩放、时长等参数，并在 `Tick()` 中：

- 让持续 Cue 跟随对应 Actor 的位置
- 回收已经到期的瞬时 Cue

这套实现很轻量，但已经把**规则事件 -> 配置表 -> 表现物**这条链路打通了。

### `BattleViewBinder`

`BattleViewBinder` 负责把逻辑世界中的 Actor / 投射物状态映射到 Unity 场景中的 GameObject。

当前覆盖三类工作：

1. 同步英雄与怪物的显隐状态
2. 同步英雄与怪物的位置、朝向
3. 同步火球等投射物 View 的创建、移动与销毁

它还维护了一份 `ActorRenderState`，保存：

- 上一个逻辑位置
- 当前逻辑位置
- 上一个逻辑旋转
- 当前逻辑旋转

然后根据 `BattleSession.InterpolationAlpha` 做插值渲染。这样逻辑层可以按固定步长推进，而表现层仍能看起来平滑。

这部分是目前业务代码里最重要的“逻辑 / 表现解耦”落点之一。

### `BattleMonsterView`

`BattleMonsterView` 是杂兵表现对象池包装器，底层基于 `Framework.ObjectPool`。

它的职责很明确：

- `Configure(prefab)`：由 `BattleSetup` 在资源加载完成后设置 Prefab
- `SpawnAt(...)`：从池里拿一个怪物实例并摆到指定位置
- `TearDown()`：在战斗生命周期结束时配合对象池回收

这里采用的策略是：

- **Prefab 资源由 `BattleSession.Scope` 持有**
- **Prefab 实例由对象池持有**

这样 Scope 负责“战斗会话结束时卸掉资源”，对象池负责“战斗运行时少 Instantiate / Destroy”。

## 核心机制总结

### 1. 业务层不反向污染框架

`Assets/Scripts` 只消费 `Framework.*` 的能力，不把业务层概念塞回框架。这样框架仍保持通用，而 Demo / 业务则可以按项目需求自由变化。

### 2. 战斗会话是一次性、独立的

每次进入战斗都通过 `BattleFlow` 申请一个新的 `BattleSession`。该 Session 持有：

- 独立的 `GamePlayFramework`
- 独立的 `ResourceScope`
- 独立的 ActorId 分配器

这让战斗状态和全局单例解耦，也让后续多战斗实例、录制回放、战斗重开更容易处理。

### 3. 输入采样和逻辑推进解耦

输入先采样成 `BattleInputFrame`，再在固定逻辑步里消费，而不是每次 `Update()` 直接写玩法状态。这是后续帧同步、回放、网络对战最关键的前置条件之一。

### 4. 表现同步晚于逻辑推进

Actor 状态、Cue、投射物都在逻辑更新之后再统一刷新。这样表现层始终读取“当前逻辑结果”，避免输入、逻辑、表现彼此穿插带来的时序问题。

### 5. 资源生命周期绑定战斗会话

战斗资源的主要释放路径是：

```text
BattleBootstrap.OnDestroy
  -> BattleViewBinder.Clear
  -> BattleFlow.Exit
  -> BattleSession.Dispose
  -> ResourceScope.Dispose
  -> GameSceneFlow.RequestUnusedAssetsCleanup
```

这套链路保证了资源、表现实例、逻辑会话基本能随着战斗结束一起收口。

## 当前已覆盖的核心功能

当前这层业务代码已经打通了以下能力：

- Launch 场景启动模块组
- 首页 UI 打开与进入战斗
- 战斗场景主流程自举
- 独立 BattleSession 创建 / 释放
- 配置表加载
- 英雄 / 杂兵 Actor 创建
- 技能注册与怪物 AI 挂载
- 12 槽杂兵刷波
- 输入采样与固定步长应用
- Hero 近战 / 火球 / 闪避
- Damage / Death / Gameplay Cue 事件表现
- 杂兵对象池
- 火球投射物可视化
- Actor 渲染插值
- 战斗退出时请求未使用资源清理

## 当前边界与后续可扩展点

这份业务层代码目前已经能支撑一个结构清晰的战斗演示，但仍有一些刻意保留的边界：

- 还没有完整的“战斗结束 -> 回 Launch -> 重新打开主界面”闭环
- 还没有 Loading / 结算 / 失败界面
- 输入虽然已经命令化了一步，但还没有网络帧同步协议
- 目前表现仍以简化球体、简单日志和对象池为主，尚未接 Animator / Timeline / 正式特效
- 当前战斗内容偏 Demo，更多是用来验证 `Framework.GamePlay + GAS + ECS + Res + UI` 之间的拼装方式

后续如果继续扩，建议优先按这个顺序：

1. 补齐退出战斗与回到 Launch 的完整闭环
2. 加入 Loading / 预加载 / BGM 切换
3. 把输入帧进一步改造成可同步的命令流
4. 在 `BattleViewBinder` 之上增加更正式的动画与特效表现层

## 使用建议

- 如果要新增一个业务场景，优先参考 `GameSceneFlow + BattleFlow + BattleBootstrap` 这套分层，不要把所有逻辑堆进一个 MonoBehaviour。
- 如果要新增战斗技能演示，优先改 `BattleSetup` 的 Actor 能力注册，再在 `BattlePresentation` / `BattleCuePresenter` 补表现。
- 如果要接网络同步，优先从 `BattleInputFrame` 与 `BattleSession.Tick()` 下手，而不是直接改 View 层。
- 如果要改资源释放策略，优先沿着 `BattleSession.Scope -> BattleFlow.Exit -> RequestUnusedAssetsCleanup` 这条链路梳理。
