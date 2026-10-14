# Framework.GamePlay

玩法主入口：编排 GAS 规则与 ECS 模拟，通过 **BattleDirector / BattleSession** 支持多战斗并行。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.GamePlay` |
| 命名空间 | `Framework.GamePlay` / `Framework.GamePlay.Data` |
| 依赖 | `Framework.Core`、`Framework.Battle.Core`、`Framework.Events`、`Framework.GAS`、`Framework.ECS`、`Framework.ECS.Physics`、`Framework.Logging`、`Framework.Res`、`Framework.BehaviourTree`、`Framework.FixedMath`。Luban 适配在 `Framework.GamePlay.Luban`，类型命名空间不变 |

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
| `LocalLockstepHost` | 单机自循环。`InputDelayFrames` 为 0 时入队后立刻出队，与原来相同；大于 0 时多留几帧。用 unscaled 固定步长推进；每步录像 + checksum |
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

命名空间 `Framework.GamePlay.Data`。装配入口是 `IBattleAbilityCatalog` / `IBattleEffectCatalog`，接口在 `Framework.GamePlay`，上面没有 Luban 类型。默认实现 `LubanAbilityCatalog` / `LubanEffectCatalog` 和工厂在程序集 `Framework.GamePlay.Luban`（引用 `Framework.Core`、`Framework.Events`、`Framework.Battle.Core`、`Framework.Config`、`Framework.FixedMath`、`Framework.GamePlay`、`Framework.GAS`、`Generated.Luban`），仍读 `cfg.CfgTables`。`BattleConfigApplier` 把 `BattleContext.Presentation`（`IEventBus`）传给 GAS，所以该程序集必须直接引用 `Framework.Events`。调用 `CfgTables` 重载的程序集要引用 `Framework.GamePlay.Luban`。`RegisterActorAbilities` 与 `BattleConfigApplier.ApplyEffect` 保留 `CfgTables` 重载，内部转成上述目录。`GamePlayFramework.EffectCatalog` 为 null，或目录里没有该效果时，命中结算抛出 `InvalidOperationException`，不再静默查 `ConfigManager`。`EffectConfigFactory.CreateDef(row)` 仍保留，并标为过时；目录实现应调用 `CreateDef(row, tables)`。本层不含 Tick。

| 类型 | 职责 |
|------|------|
| `IBattleAbilityCatalog` | `abilityId` + 队伍 → `GameplayAbilityDef` |
| `IBattleEffectCatalog` | `effectId` → `GameplayEffectDef` |
| `LubanAbilityCatalog` / `LubanEffectCatalog` | Luban 默认后端 |
| `AbilityConfigFactory` | Luban 技能行 → `GameplayAbilityDef` |
| `EffectConfigFactory` | Luban 效果行 → `GameplayEffectDef` |
| `GamePlayConfigSetup` | `RegisterActorAbilities` 扩展方法 |
| `BattleConfigApplier` | 对 ASC 应用效果 |

## 行为指令（玩家 / AI 共用）

玩家与 AI 产出同一套 `BattleIntentCommand`（`Move` / `Cast`），由 `BattleIntentApplier` 执行。这与 `BattleCommandBuffer` **不是同一层**：

| 层 | 类型 | 谁产出 | 谁消费 |
|----|------|--------|--------|
| 行为意图 | `BattleIntentCommand` | 玩家编码 / AI `SubmitIntent` | `BattleIntentApplier` → `SetActorVelocity` / `TryActivateAbility` |
| 模拟结算 | `BattleCommandBuffer` | GAS / ECS 碰撞 | `BattleCommandProcessor`（Spawn / Damage / Heal 等） |
| 表现 | `Framework.GAS.Events` 的 struct | GAS `Publish` 到本场 `EventBus` | 业务表现层 `Subscribe`；模拟层不订阅 |

约定：

- 连招选择、火球瞄准、闪避朝向在 **编码时** 决定；`Apply` 只执行
- `BattleIntentCommand` 速度 / 朝向 / 施法原点与方向均为 `TSVector`；`BattleActor.SimPosition` 为逻辑链权威位姿，`Position` 保留给 ViewBinder
- AI / 围攻槽位 / 远距追击全程读 `SimPosition`；黑板运行时读 `FP`，JSON 配置 float 只在装配时转一次
- 刷波环上坐标与 `ReviveActor` 直接用 `TSVector` / `SimPosition`，不做 FP→Vector3→FP 往返
- 眩晕 / 倒地禁移动、闪避中不覆盖速度，留在 **执行器** 作为规则
- 刷波、`CreateActor` 不是行为意图
- 模拟内 Actor 遍历（ASC.Tick、Cue、定身、死亡同步、位置回写）按 `ActorId` 升序，不依赖 `Dictionary` 枚举顺序。AI Agent 同样按 `ActorId` 排序后再 Tick。空间查询候选与投射物寿命销毁也按实体 ID 升序。围攻槽位在方位角相同时用 ActorId 打破平局
- 指令带来源 `Player` / `Ai` / `Replay`，只用于日志与回放过滤，不改变规则
- 渲染帧只 `Sample` 锁存；逻辑帧把意图写入 `BattleIntentFrame` 再入队执行，禁止 Sample 直接改模拟

## 单机锁步 Host

结算命令最多再刷 4 轮，避免本轮事件里新入队的伤害在清空时被丢掉。平时只有一轮，结果与原来相同。

`LocalLockstepHost` 用 `Time.unscaledDeltaTime`（仅渲染追赶）追赶 `BattleSession.FixedDeltaTime`（`FP` 逻辑步长）。追赶累加器是 `FP`：float 只在 `Tick(float)` 入口转换一次，比较和扣减不再用 float，因此相同的 float 输入序列得到相同的逻辑步数。`InterpolationAlpha` 仍是 float，只给表现插值。录像重放走 `BattleSession.Step(1)`，不把步长转回 float。每个逻辑步的 `beforeFixedStep` / `afterFixedStep`、`CollectAiIntents`、`Framework.Tick`、刷波与录像带均携带该 `FP`：

这套和 `Framework.Lockstep` 里的 TrueSync（`AbstractLockstep`、`ReplayRecord`、`ChecksumExtractor`）不是同一条链路。战斗校验和是 `BattleFrameChecksum`，录像是内存里的 `BattleReplayTape`。不要把 TrueSync 的 MD5 物理校验和当成战斗结果。

```
fillFrame（玩家编码）→ CollectAiIntents → Queue.Enqueue → Queue.Dequeue → BattleIntentApplier → Framework.Tick → 刷波 → checksum / 录像
```

`InputDelayFrames` 为 0 时入队后立即出队，已有单机录像不变；大于 0 时队列里多于这个帧数才出队。联网时同一队列可改为等远端帧到齐再出队。逻辑步不受 `Time.timeScale` 影响。意图、AI、位姿查询、命令缓冲位姿与物理常量均为定点（`FP` / `TSVector`）；Actor 挤开 / 位移 / 弹道命中走 `BattlePhysicsWorld`（Farseer 2D）。表现事件与 `BattleActor.Position` 仍为 float / `Vector3`。超大数值结算不要用 Q31.32。`InputDelayFrames` 大于 0 时，Host 每个逻辑步都会推进 Agent，但录像只记下真正出队的帧，`BattleReplayVerifier` 对不上，不要用它校验这种录像。

这一组修正会让旧录像和旧校验和对不上，需要重新录制，不能拿以前存下来的逐步校验和当金标：刚体按给定坐标生成，不再堆在原点；扇形夹角不再恒为 90°；第 0 帧物理快照会先收进待添加的刚体；AI 和技能任务进入校验和；实体 ID 回收后穿透名单记的是 `ActorId`。快照字节布局没有变。ECS / GAS 快照版本仍是 3。

战斗中按 **F8** 会用当前录像在影子 Session 上重放。每步先 `CollectAiIntents`（结果丢弃，只为推进 Agent），再 Apply 录像里的命令，逐帧比对 `BattleFrameChecksum`。

`BattleFrameChecksum` 覆盖：随机状态、Actor 生命/法力/护盾/死亡、标签计数、活跃效果与冷却剩余时间及层数、激活技能 ID 与状态、有任务时再加实例 ID 和任务进度、已绑定 Agent 的私有随机数、权威黑板（含字符串，按键排序）和行为树槽位、定点位姿与速度、击退、投射物队伍/穿透/爆炸半径/伤害类型/寿命/位置。不覆盖 Farseer 接触缓存、黑板 object 袋和表现层 float。没有激活技能实例、也没有 Agent 时，哈希输入与加入这些字段之前相同。激活实例没有任务时，仍只写技能 ID 和状态。字符串按字符序哈希，不用 `string.GetHashCode`。`BattleAgent` 未传入随机源时，用 `DeriveSeed(sessionSeed, actorValue)`，不再固定种子 1。生产里的近战杂兵走这条派生，不再共用会话随机源。

`MinimalBattle` 是无视图样本：种子 42，队伍 0 在原点放弹道，队伍 1 在 +X 1.6 米放近战，默认 48 个逻辑步。`MinimalBattle.Run` 返回逐步 `BattleFrameChecksum`。它直接 `GiveAbility`，不经过配置目录。业务层把 `Game.MinimalBattleSample` 挂到空场景任意物体上，Play 后在日志里看末帧校验和；本仓库没有另存 `.unity`，因为没有编辑器就写不出合法的脚本引用。EditMode `MinimalBattleTests` 把同一场跑两遍并逐项对比。

`LockstepHost` 等所有玩家的输入到齐再推进一步。`BattleLockstepSimulation` 把 `BattleSession` 交给它，检查点含随机数、ECS、`GasSnapshot`、`BattlePhysicsSnapshot`，以及当时已绑定 Agent 的行为树快照（含随机源，不含 object 袋）。`PredictRemote` 先填占位，正式输入不同就从最近的检查点重放。`LocalLockstepHost` 仍是单机意图帧，不走 `ICommunicator`。同一进程的两场物理世界共用 Farseer 静态 ID。

EditMode 测试在 `Assets/Tests/EditMode`（程序集 `Framework.EditMode.Tests`）。Unity 菜单 **Window → General → Test Runner → EditMode** 运行：同一种子两遍校验和、录像 `BattleReplayVerifier` 对拍、确定性扫描、ECS+随机数快照往返、最小战斗两遍对拍，以及 `LockstepRollbackTests`（环回等输入、迟到输入回滚）。`World.Restore` 之后调用 `Registry.RebindEntitiesAfterSnapshot`。命令行扫描等价于 **Tools → Determinism → Scan Battle Sim**。可选 GitHub Actions 工作流 `.github/workflows/editmode-tests.yml` 只用手动触发，需要 `UNITY_LICENSE` / `UNITY_EMAIL` / `UNITY_PASSWORD`，不是必需检查。

## Tick 流程

```
LocalLockstepHost.Tick(unscaledDeltaTime)
  fillFrame（玩家）→ CollectAiIntents（AI 只入帧）
  Enqueue → Dequeue → BattleIntentApplier
  GamePlayFramework.Tick(fixedDt) 按 BattleTickPhase 执行，可用 AddTickStep 在顺序值之间插入
    0. RebuildSpatialIndex：仅当空间索引 StructuralDirty 时 RebuildActors（刷出 / 传送 / 复活）
    100. SyncCue：拍下本步 Actor 列表，同步 Cue
    200. ApplyRootAndStun：定身/眩晕/倒地清速度
    300. TickAbilities：存活 ASC.Tick（查询读本步物理前的格子）
    400. FlushSpawn
    500. TickEcs：Farseer Step → SpatialIndex（每逻辑步必做的一次全量重建）→ 投射物寿命
    600. FlushOutcome
    700. SyncDeath
    800. SyncPositions
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
