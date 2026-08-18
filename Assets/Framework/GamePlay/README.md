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
| `BattleSession` | 一场战斗的独立会话：持有独立 `GamePlayFramework` + `ResourceScope` + ActorId 分配器 |
| `GamePlayFramework` | 玩法运行时：创建 Actor、注册技能、驱动单场 Tick |
| `ActorRegistry` | ActorId ↔ ECS Entity ↔ ASC 三向映射 |
| `BattleCommandProcessor` | 刷写 Spawn / Damage / Heal / GE / Area / Displace |
| `BattleAgent` / `BattleAiNodes` | GamePlay 侧 BT 叶子 |
| `BattleIntentCommand` / `BattleIntentApplier` | 玩家与 AI 共用的 Move / Cast 行为指令与执行器 |
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
    ├── Start  → Director.CreateSession → 加载 GO → 创建 Actor
    ├── Update → 读输入 + session.Framework.Tick(dt) + 同步 GO
    └── OnDestroy → Director.DestroySession → Destroy GO
```

## BattleSession

每个 Session 是**一场战斗的完整规则实例**，彼此完全隔离：

- 独立 `GamePlayFramework`（ECS World / ActorRegistry / EventBus）
- 独立 `ResourceScope`（资源随 Session 释放）
- ActorId 从 1 自增，不跨 Session 冲突

```csharp
var director = GamePlayModule.Instance.Director;

// 创建战斗
var session = director.CreateSession();
var heroId = session.AllocateActorId();
session.Framework.CreateActor(heroId, pos, 100f, teamId: 1);

// 每帧（或由 Director.Tick 集中驱动）
session.Framework.Tick(Time.deltaTime);

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
| 行为意图 | `BattleIntentCommand` | 玩家编码 / AI 叶子 | `BattleIntentApplier` → `SetActorVelocity` / `TryActivateAbility` |
| 模拟结算 | `BattleCommandBuffer` | GAS / ECS 碰撞 | `BattleCommandProcessor`（Spawn / Damage / Heal 等） |

约定：

- 连招选择、火球瞄准、闪避朝向在 **编码时** 决定；`Apply` 只执行
- 眩晕 / 倒地禁移动、闪避中不覆盖速度，留在 **执行器** 作为规则
- 刷波、`CreateActor` 不是行为意图
- 指令带来源 `Player` / `Ai` / `Replay`，只用于日志与回放过滤，不改变规则

## Tick 流程

```
GamePlayFramework.Tick(deltaTime)
  0. RebuildActors
  1. SyncCuePose → BT Agent → 定身/眩晕/倒地清速度
  2. 存活 ASC.Tick
  3. Flush Spawn → ECS Tick → Flush 结算 → SyncDeath → Sync Positions
```

## Bootstrap 集成

```csharp
// Launch.cs
new GamePlayModule(), // 无外部依赖；Config 由业务层按需加载
```

## 典型用法（业务层）

```csharp
// BattleBootstrap.cs（场景级 MonoBehaviour）
var session = GamePlayModule.Instance.Director.CreateSession();
var heroId = session.AllocateActorId();
session.Framework.CreateActor(heroId, heroPos, 120f, teamId: 1);
session.Framework.RegisterActorAbilities(heroId, 1, new[] { "Fireball", "Slash" }, tables);

// Update
session.Framework.Tick(Time.deltaTime);

// OnDestroy
GamePlayModule.Instance.Director.DestroySession(session);
```

## 行为验证示例

- 英雄 WASD 移动；J 近战 Slash→Slash2→Slash3（第三段霸体+倒地）；K 火球；Left Shift 闪避无敌
- 12 只怪物槽位常驻：全灭约 2 秒后在英雄周围复活为下一波（生命随波次增加）
- 杂兵按方位角占位围攻，走槽位、面朝英雄；远距只朝槽位走，靠近后才跑完整 BT
- 英雄造成伤害时短暂 hit-stop
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
