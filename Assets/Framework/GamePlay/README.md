# Framework.GamePlay

玩法主入口：编排 GAS 规则与 ECS 模拟，通过 **BattleDirector / BattleSession** 支持多战斗并行。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.GamePlay` |
| 命名空间 | `Framework.GamePlay` / `Framework.GamePlay.Data` |
| 依赖 | `Framework.Core`、`Framework.Events`、`Framework.GAS`、`Framework.ECS`、`Framework.Logging`、`Framework.Res`、`Framework.Config`、`Framework.BehaviourTree`、`Framework.FixedMath` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `GamePlayModule` | `IGameModule` 实现，持有 `BattleDirector` |
| `BattleDirector` | Session 工厂 + 集中 Tick：`CreateSession` / `DestroySession` / `GetSession` / `Tick(dt)` |
| `BattleSession` | 一场战斗的独立会话：持有独立 `GamePlayFramework` + `ResourceScope` + ActorId 分配器；`FixedDeltaTime` 为 `FP` |
| `GamePlayFramework` | 玩法运行时：创建 Actor、注册技能、驱动单场 Tick |
| `ActorRegistry` | ActorId ↔ ECS Entity ↔ ASC 三向映射 |
| `BattleCommandProcessor` | 刷写 Spawn / Damage / Heal / GE / Area / Displace |
| `BattleAgent` / `BattleAiNodes` | GamePlay 侧 BT 叶子 |
| `BattleIntentCommand` / `BattleIntentApplier` | 玩家与 AI 共用的 Move / Cast 行为指令与执行器 |
| `BattleIntentFrame` / `BattleIntentFrameQueue` | 按逻辑帧归档的意图队列 |
| `LocalLockstepHost` | 单机自循环：编码入队后立刻出队，用 unscaled 固定步长推进；每步录像 + checksum |
| `BattleFrameChecksum` | 逻辑步结束后的轻量状态哈希 |
| `BattleReplayTape` / `BattleReplayRecorder` / `BattleReplayVerifier` | 内存录像与影子 Session 对拍 |
| `EngageSlotAllocator` | 按追击目标把杂兵铺在环上，避免叠点 |
| `BattleWaveDirector` | 杂兵槽位回收与刷波 |

## 架构分层

```
全局常驻
└── GamePlayModule
    └── BattleDirector（Session 工厂 + 集中 Tick）
        ├── Session 1 → GamePlayFramework + ResourceScope + ActorId 命名空间
        ├── Session 2 → ...
        └── ...

场景级（业务层 Game）
└── BattleBootstrap : MonoBehaviour
    ├── Start  → Director.CreateSession(seed) → 加载 GO → 创建 Actor
    ├── Update → Sample(unscaled) → LocalLockstepHost.Tick → 同步 GO；F8 影子对拍
    └── OnDestroy → Director.DestroySession → Destroy GO
```

## BattleSession

每个 Session 是**一场战斗的完整规则实例**，彼此完全隔离：

- 独立 `GamePlayFramework`（ECS World / ActorRegistry / EventBus）
- 独立 `ResourceScope`（资源随 Session 释放）
- 独立 `TSRandom`（`CreateSession(randomSeed)` 注入，默认种子 1）
- ActorId 从 1 自增，不跨 Session 冲突

```csharp
var director = GamePlayModule.Instance.Director;

// 创建战斗
var session = director.CreateSession(randomSeed: 1);
var heroId = session.AllocateActorId();
session.Framework.CreateActor(heroId, pos, 100f, teamId: 1);

// 每帧由 LocalLockstepHost 用 unscaled 时间推进固定步长
// session.Framework.Tick(fixedDt) 只应在逻辑步内调用

// 结束战斗
director.DestroySession(session); // Framework.Dispose + Scope.Dispose
```

## GamePlay.Data（配置装配层）

命名空间 `Framework.GamePlay.Data`：读 `cfg.CfgTables`，装配到 GAS，**不含 Tick/模拟**。

| 类型 | 职责 |
|------|------|
| `AbilityConfigFactory` | Luban 技能行 → `GameplayAbilityDef` |
| `EffectConfigFactory` | Luban 效果行 → `GameplayEffectDef` |
| `GamePlayConfigSetup` | `RegisterActorAbilities` 扩展方法 |
| `BattleConfigApplier` | 对 ASC 应用 Luban 效果 |

## 行为指令（玩家 / AI 共用）

玩家与 AI 产出同一套 `BattleIntentCommand`（`Move` / `Cast`），由 `BattleIntentApplier` 执行。这与 `BattleCommandBuffer` **不是同一层**：

| 层 | 类型 | 谁产出 | 谁消费 |
|----|------|--------|--------|
| 行为意图 | `BattleIntentCommand` | 玩家编码 / AI `SubmitIntent` | `BattleIntentApplier` → `SetActorVelocity` / `TryActivateAbility` |
| 模拟结算 | `BattleCommandBuffer` | GAS / ECS 碰撞 | `BattleCommandProcessor`（Spawn / Damage / Heal 等） |

约定：

- 连招选择、火球瞄准、闪避朝向在 **编码时** 决定；`Apply` 只执行
- `BattleIntentCommand` 速度 / 朝向 / 施法原点与方向均为 `TSVector`；`BattleActor.SimPosition` 为逻辑链权威位姿，`Position` 保留给 ViewBinder
- AI / 围攻槽位 / 远距追击全程读 `SimPosition`；黑板运行时读 `FP`，JSON 配置 float 只在装配时转一次
- 刷波环上坐标与 `ReviveActor` 直接用 `TSVector` / `SimPosition`，不做 FP→Vector3→FP 往返
- 眩晕 / 倒地禁移动、闪避中不覆盖速度，留在 **执行器** 作为规则
- 刷波、`CreateActor` 不是行为意图
- 指令带来源 `Player` / `Ai` / `Replay`，只用于日志与回放过滤，不改变规则
- 渲染帧只 `Sample` 锁存；逻辑帧把意图写入 `BattleIntentFrame` 再入队执行，禁止 Sample 直接改模拟

## 单机锁步 Host

`LocalLockstepHost` 用 `Time.unscaledDeltaTime`（仅渲染追赶累加器）追赶 `BattleSession.FixedDeltaTime`（`FP` 逻辑步长）。每个逻辑步的 `beforeFixedStep` / `afterFixedStep`、`CollectAiIntents`、`Framework.Tick`、刷波与录像带均携带该 `FP`：

```
fillFrame（玩家编码）→ CollectAiIntents → Queue.Enqueue → Queue.Dequeue → BattleIntentApplier → Framework.Tick → 刷波 → checksum / 录像
```

单机入队后立即出队。联网时同一队列可改为等远端帧到齐再出队。逻辑步不受 `Time.timeScale` 影响。意图、AI、位姿查询、命令缓冲位姿与物理常量均为定点（`FP` / `TSVector`）；Actor 挤开 / 位移 / 弹道命中走 `BattlePhysicsWorld`（Farseer 2D）。表现事件与 `BattleActor.Position` 仍为 float / `Vector3`。超大数值结算不要用 Q31.32。

战斗中按 **F8** 会用当前录像在影子 Session 上重放指令（不跑 AI 收集），逐帧比对 `BattleFrameChecksum`。

## Tick 流程

```
LocalLockstepHost.Tick(unscaledDeltaTime)
  fillFrame（玩家）→ CollectAiIntents（AI 只入帧）
  Enqueue → Dequeue → BattleIntentApplier
  GamePlayFramework.Tick(fixedDt)
    0. RebuildActors
    1. SyncCuePose → 定身/眩晕/倒地清速度
    2. 存活 ASC.Tick
    3. Flush Spawn → Farseer Step → SpatialIndex → 投射物寿命 → Flush 结算 → SyncDeath → Sync Positions
  afterFixedStep（刷波）→ checksum → 录像
```

## Bootstrap 集成

```csharp
// Launch.cs
new GamePlayModule(), // 无外部依赖；Config 由业务层按需加载
```

## 典型用法（业务层）

```csharp
// BattleBootstrap.cs（场景级 MonoBehaviour）
var session = GamePlayModule.Instance.Director.CreateSession(randomSeed: 1);
var host = new LocalLockstepHost(session);

// Update
input.Sample(Time.unscaledDeltaTime);
host.Tick(Time.unscaledDeltaTime, frame => input.Encode(framework, heroId, frame.Commands, session.FixedDeltaTime));

// OnDestroy
GamePlayModule.Instance.Director.DestroySession(session);
```

## 行为验证示例

- 英雄 WASD 移动；J 近战 Slash→Slash2→Slash3（第三段霸体+倒地）；K 火球；Left Shift 闪避无敌
- 12 只怪物槽位常驻：全灭约 2 秒后在英雄周围复活为下一波（生命随波次增加）
- 杂兵按方位角占位围攻，走槽位、面朝英雄；远距只朝槽位走，靠近后才跑完整 BT
- 英雄造成伤害时短暂 **表现层 HitStop**（冻结 View，不改 `Time.timeScale`）
- F8：影子 Session 重放本场录像并比对校验和
- 近战命中带击退冲量；Actor 圆形挤开避免叠模（完全重叠时随机方向推开）
- Fireball 命中扣血；BoomShot 只走爆炸范围伤（不双算直击）

## 依赖关系

```
GamePlay
 ├── Config      (Tables 只读，业务层按需加载)
 ├── Data        (Tables → GAS Def 装配)
 ├── Res         (BattleSession 持有 ResourceScope)
 ├── Core / GAS / ECS
 └── BehaviourTree / FixedMath（AI Agent）
```

## 被谁使用

- `Assets/Scripts/Launch.cs` — 注册 `GamePlayModule`
- `Assets/Scripts/Battle/BattleBootstrap.cs` — 向 `Director` 申请 Session，驱动战斗表现
