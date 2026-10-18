# Framework.Battle — GAS + ECS 战斗框架

## 设计原则

| 原则 | 实现 |
|------|------|
| 单一数据源 | GAS 规则权威；ECS 仅存 `CombatStateComponent.IsAlive` |
| 热路径命令化 | 模拟走 `BattleCommandBuffer`；玩家/AI 行为走 `BattleIntentCommand`（Move/Cast）；表现只 Publish 到本场 `IEventBus` |
| 配置驱动 | `IBattleAbilityCatalog` / `IBattleEffectCatalog` 装配到 GAS；默认后端是 Luban |
| 资源管线 | YooAsset 打包；运行时加载/释放统一走 `ResourceManager` |
| 模块编排 | `GameBootstrap` 按依赖拓扑排序初始化各 `IGameModule` |
| 示例文档化 | 演示写在各模块 README。测试在 `Assets/Tests` 与 `tests/Framework.Sim.Tests` |

## 事件边界

模拟层（`Framework.ECS` / `Framework.GAS` / `Framework.GamePlay`）只写两类输出：`BattleCommandBuffer`，以及 `Framework.GAS.Events` 里的表现 struct（Publish 到本场 `ZeroGcEventBus`）。这些程序集不调用 `Subscribe`、`GameEvent` 或 `GlobalEventBus`。`SimDeterminismScanner` 会把这类调用标成错误。

| 通道 | 角色 |
|------|------|
| `GlobalEventBus` / `GameEvent` | 进程级 UI、流程、网络。不承载战斗结算，模拟不订阅 |
| `ZeroGcEventBus` | 一场战斗的表现总线（`GamePlayFramework.EventBus`）。模拟只 Publish；订阅在业务表现层 |
| `GameplayEventData` | `AbilitySystemComponent.HandleGameplayEvent` 同步投递给技能和 Task。不是总线，监听顺序由调用方决定 |

表现订阅的先后不影响校验和。把规则挂到总线上会让监听顺序进入模拟。

## 帧同步与回滚（能力边界）

| 项 | 状态 |
|----|------|
| 定点 / 锁步 / 物理 / 行为树基座 | 已迁移或已实现 |
| 战斗 Demo 锁步权威 Tick | 已接 `LocalLockstepHost`（意图帧 + checksum + F8 对拍） |
| **预测回滚 / 状态快照** | **`LockstepHost` 可等输入，并按检查点回滚重放**。检查点含 `GasSnapshot`（版本 4）、ECS、随机数和 `BattlePhysicsSnapshot`（内存克隆，不是字节流）。`World.Restore` 之后要 `RebindEntitiesAfterSnapshot`。行为树游标已纳入检查点；旧的 `RollbackLockstep` 仍未接入当前战斗。Farseer 的 `_bodyIdCounter` 在每个 `World` 上。Jitter 的 `RigidBody.instanceCount` / `Constraint.instanceCount` 仍是静态字段：构造世界时清零，克隆快照会存回再还原，所以单场回滚能对上；两场 Jitter 世界同时存在会互相踩计数器。效果 Spec 的 RuntimeId、技能实例 ID 和活跃效果句柄在每个 ASC 上递增 |
| **反作弊（checksum 接线 / Obscured 适配）** | **未实现**（校验类型已迁；FixedMath 刻意无 Obscured） |

详见根 [README 战斗套件](../README.md#战斗套件)、[Lockstep](../Packages/com.jokerseed.framework.battle/Runtime/Lockstep/README.md)、[FixedMath](../Packages/com.jokerseed.framework.battle/Runtime/FixedMath/README.md)、[BehaviourTree](../Packages/com.jokerseed.framework.battle/Runtime/BehaviourTree/README.md)。

## 两套锁步不要混

| | 战斗 Demo | TrueSync 基座 |
|--|--|--|
| 入口 | `LocalLockstepHost` | `AbstractLockstep` |
| 步长 | `BattleSession.FixedDeltaTime`（`FP`） | 构造参数 `float deltaTime` |
| 校验 | `BattleFrameChecksum` | `ChecksumExtractor`（物理体 MD5） |
| 录像 | `BattleReplayTape`（内存） | `ReplayRecord` |
| 回滚 | `LockstepHost` + `BattleLockstepSimulation` 按检查点回滚（GAS、ECS、随机数、物理克隆、已绑定 Agent 的行为树）。`LocalLockstepHost` 仍是单机意图帧，不等输入 | `RollbackLockstep` 只克隆物理世界，GAS/ECS/行为树不还原 |

战斗结果以 `BattleFrameChecksum` 为准。`AbstractLockstep` 不驱动当前 Battle；它只在 `RUNNING` 时推进，校验和第一次不一致会锁存。联网回滚走 `LockstepHost`，不要把 `RollbackLockstep` 当成已经接上的战斗回滚。

## 线程

模拟、事件、UI、资源、对象池、音频、配置都在主线程。网络收发的完成回调在 IO 线程，只负责把字节放进待分发队列；`NetworkManager.Update` 再在主线程调用处理器和 `GameEvent`。IO 回调里不要碰 GAS、ECS、行为树或 `GameEvent`。内存池每个类型一把锁，主线程和 IO 线程可以同时 `Acquire` / `Release`；完成回调里仍然不要借这个锁去碰模拟。

## 时间源

| 模块 | 时钟 | 原因 |
|------|------|------|
| 战斗模拟 | `FP` 固定步长 | 校验和只认这个 |
| `LocalLockstepHost` 追赶 | `Time.unscaledDeltaTime` 只在入口转一次 | 暂停 `timeScale` 时逻辑仍追帧 |
| `ITickable.Tick(float)` | 调用方传入 | 桥，内部应立刻转 `FP` |
| `ObjectPoolManager` | `Time.deltaTime`（受 `timeScale` 影响） | 表现对象过期跟着游戏暂停 |
| 对象池过期戳 | `DateTime.UtcNow` | 墙钟，不进模拟 |
| `NetworkManager` | `Time.unscaledDeltaTime` | 心跳不受暂停影响 |
| 音频淡入淡出 | `Time.unscaledDeltaTime` | 暂停时 BGM 仍能淡出 |

## 程序集与文档

| 程序集 | 职责 | 文档 |
|--------|------|------|
| `Framework.Bootstrap` | 模块启动编排（Host；`IGameModule` 契约在 Core） | [Bootstrap](../Packages/com.jokerseed.framework.core/Runtime/Bootstrap/README.md) |
| `Framework.Core` | `IGameModule`、常驻单例 | [Core](../Packages/com.jokerseed.framework.core/Runtime/Core/README.md) |
| `Framework.Battle.Core` | `ActorId`、`BattleContext`、命令缓冲。命名空间是 `Framework.Battle`（含 `.Commands` / `.Snapshot`） | [Battle.Core](../Packages/com.jokerseed.framework.battle/Runtime/Battle.Core/README.md) |
| `Framework.Events` | `IEventBus` 契约与零 GC 实现 | [Events](../Packages/com.jokerseed.framework.core/Runtime/Events/README.md) |
| `Framework.GAS` | ASC、伤害管线、Effect、Tag。快照版本 4 | [GAS](../Packages/com.jokerseed.framework.battle/Runtime/GAS/README.md) |
| `Framework.ECS` | World、System、空间哈希。销毁实体时只调用 `IEcsBodyRemover` | [ECS](../Packages/com.jokerseed.framework.battle/Runtime/ECS/README.md) |
| `Framework.ECS.Physics` | `BattlePhysicsWorld` 与 Farseer 步进 | [ECS.Physics](../Packages/com.jokerseed.framework.battle/Runtime/ECS.Physics/README.md) |
| `Framework.GamePlay` | `GamePlayFramework` 玩法主入口。不引用 Luban | [GamePlay](../Packages/com.jokerseed.framework.battle/Runtime/GamePlay/README.md) |
| `Game.Luban` | 演示层程序集（`Assets/Scripts/Luban`）。Luban 表到技能/效果目录的适配，类型仍在 `Framework.GamePlay.Data` | [Game.Luban](../Assets/Scripts/Luban/README.md) |
| `Framework.Config` | Luban 加载与 Tables 缓存 | [Config](../Packages/com.jokerseed.framework.config/Runtime/README.md) |
| `Framework.Res` | YooAsset 封装（`ResourceManager`） | [Res](../Packages/com.jokerseed.framework.res/Runtime/README.md) |
| `Framework.MemoryPool` | 轻量内存池（`IMemory`） | [MemoryPool](../Packages/com.jokerseed.framework.core/Runtime/MemoryPool/README.md) |
| `Framework.ObjectPool` | 对象池（`ObjectBase` / 容量过期） | [ObjectPool](../Packages/com.jokerseed.framework.core/Runtime/ObjectPool/README.md) |
| `Framework.Coroutine` | 协程（Global / Scene / GameObject） | [Coroutine](../Packages/com.jokerseed.framework.core/Runtime/Coroutine/README.md) |
| `Framework.Logging` | `GameLog` 分级日志 | [Logging](../Packages/com.jokerseed.framework.core/Runtime/Logging/README.md) |
| `Framework.FixedMath` | 确定性定点数（锁步基座 A） | [FixedMath](../Packages/com.jokerseed.framework.battle/Runtime/FixedMath/README.md) |
| `Framework.Lockstep` | 帧同步调度与输入抽象（基座 B/C） | [Lockstep](../Packages/com.jokerseed.framework.battle/Runtime/Lockstep/README.md) |
| `Framework.LockstepPhysics` | 确定性 Farseer 2D；Jitter 3D 在可选包 `com.jokerseed.framework.battle.legacy` | [LockstepPhysics](../Packages/com.jokerseed.framework.battle/Runtime/LockstepPhysics/README.md) |
| `Framework.Lockstep.Legacy` / `Framework.LockstepPhysics.Jitter` | 可选包：TrueSync 旧锁步调度器、Jitter 3D 物理，战斗包不引用 | [Battle Legacy](../Packages/com.jokerseed.framework.battle.legacy/README.md) |
| `Framework.BehaviourTree` | 帧同步友好的 AI 行为树运行时 | [BehaviourTree](../Packages/com.jokerseed.framework.battle/Runtime/BehaviourTree/README.md) |
| `Framework.UI` | UI 窗口管理（`UIManager` / `UIWindow`） | [UI](../Packages/com.jokerseed.framework.ui/Runtime/README.md) |
| `Framework.Network` | TCP / WebSocket / KCP 频道与 HTTP 短请求。KCP 源码在本包 `Runtime/ThirdParty/KCP` | [Network](../Packages/com.jokerseed.framework.network/Runtime/README.md) |
| `Framework.Audio` | 音频管理（`AudioManager` / BGM·SFX 双通道） | [Audio](../Packages/com.jokerseed.framework.audio/Runtime/README.md) |
| `Framework.Core.Editor` / `Framework.Res.Editor` / `Framework.Config.Editor` / `Framework.Battle.Editor` / `Framework.BehaviourTree.Editor` | 各包自己的编辑器工具，没有单独的 `Framework.Editor` 程序集 | [编辑器工具](editor-tools.md) |
| `Game` | 业务入口（Launch / 主界面 / Battle 演示） | [Scripts](../Assets/Scripts/README.md) |
| `Generated.Luban` | Luban 生成配置代码 | [Generated](../Assets/Generated/README.md) |

## 示例与验证（写在 README）

| 内容 | 位置 |
|------|------|
| 启动流程 + 配置加载 | `Assets/Scripts/Launch.cs`、[Config](../Packages/com.jokerseed.framework.config/Runtime/README.md) |
| 完整战斗演示 | [GamePlay](../Packages/com.jokerseed.framework.battle/Runtime/GamePlay/README.md) → 完整示例 |
| ASC 伤害/效果/标签验证 | [GAS](../Packages/com.jokerseed.framework.battle/Runtime/GAS/README.md) → 行为验证示例 |

## 模块依赖图

```
Bootstrap (Host)
    │
    ├── Logging (LoggingModule)       Infrastructure
    ├── Coroutine (CoroutineModule)   Infrastructure（依赖 Logging）
    ├── MemoryPool (MemoryPoolModule) Infrastructure
    ├── ObjectPool (ObjectPoolModule) Infrastructure（依赖 MemoryPool）
    ├── Network (NetworkModule)       Infrastructure（依赖 Logging + MemoryPool）
    ├── Res (ResourceModule)          Infrastructure
    │       ├── UI (UIModule)             Presentation（依赖 Resource + Coroutine）
    │       └── Config (ConfigModule)     Data
    │               └── GamePlay (GamePlayModule)  Gameplay
    │                       ├── GAS / ECS
    │                       └── BehaviourTree（AI Agent，非 IGameModule）
    │
    ├── FixedMath / Lockstep / LockstepPhysics / BehaviourTree（帧同步与 AI 基座，按需引用，非 IGameModule）
    └── 各包 Editor 程序集（仅编辑器）
```

## 启动流程

```
Launch.Awake
  → GameBootstrap.SetModules([ResourceModule, ConfigModule, GamePlayModule, ...])
  → 拓扑排序 → 依次 InitializeAsync
  → IsReady → 业务逻辑
```

启动入口：`Assets/Scripts/Launch.cs`（挂到 `Bundles/Scenes/Launch.unity`）

## Tick 顺序

```
按需补建空间索引 → GAS Tick → Flush Spawn → Farseer Step → 物理后重建索引 → Flush Damage/Heal/GE/Displace → SyncDeath → Sync Positions
```

## Luban 配置表

策划编辑：

```
Config/Luban/Datas/battle/ability.xlsx
Config/Luban/Datas/battle/effect.xlsx
```

表结构定义：`Config/Luban/Defines/battle.xml`

导表：

| 方式 | 入口 |
|------|------|
| 命令行 | `Config\Luban\gen_client.bat` |
| Unity 菜单 | **Tools → Framework → Config → Generate Luban Config** |

产出：

| 类型 | 路径 |
|------|------|
| C# 代码 | `Assets/Generated/Luban/` |
| 二进制 | `Assets/Bundles/Configs/*.bytes` |
| JSON 调试 | `Config/Luban/Output/json/`（不参与运行时） |

战斗侧通过 `LubanAbilityCatalog` / `LubanEffectCatalog` 消费这些表。换后端时实现 `IBattleAbilityCatalog` / `IBattleEffectCatalog`，不必改 GAS。

## YooAsset 资源

| 步骤 | 菜单 |
|------|------|
| 生成 Collector | **Tools → Framework → Res → Generate YooAsset Collector** |
| 构建 Bundle | **YooAsset → Bundle Builder** |

Collector 规则：
- `Assets/Bundles/Configs/` 下每个 `.bytes` 单独打包
- 寻址：`bundles/configs/{表名}.unity3d`（如 `tbability`）

## 运行时加载配置

```csharp
// 启动时注册：ConfigModule.Create(loader => new cfg.CfgTables(loader))
var tables = ConfigManager.Instance.LoadTables<cfg.CfgTables>();
// 或 ConfigManager.Instance.CreateTables<cfg.CfgTables>()（不写缓存）
// 单表：ConfigManager.Instance.LoadConfigBytes("tbability", cache: true)
```

## 编辑器工具

| 菜单 | 功能 |
|------|------|
| Tools/Framework/Settings | 工具路径与 Collector 规则（`ProjectSettings/FrameworkSettings.asset`） |
| Tools/Framework/Config/Generate Luban Config | 执行 Luban 打表并校验生成代码前缀 |
| Tools/Framework/Res/Generate YooAsset Collector | 生成 `BundleCollectorSetting.asset` |

详见 [editor-tools.md](editor-tools.md)。
