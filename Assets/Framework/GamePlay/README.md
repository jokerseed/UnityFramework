# Framework.GamePlay

玩法主入口：编排 GAS 规则与 ECS 模拟，提供 `GamePlayFramework` 与 Bootstrap 模块 `GamePlayModule`。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.GamePlay` |
| 命名空间 | `Framework.GamePlay` / `Framework.GamePlay.Data` |
| 依赖 | `Framework.Core`、`Framework.Events`、`Framework.GAS`、`Framework.ECS`、`Framework.Logging`、`Framework.Config`、`Framework.BehaviourTree`、`Framework.FixedMath` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `GamePlayModule` | `IGameModule` 实现，创建并持有 `GamePlayFramework` |
| `GamePlayFramework` | 玩法运行时入口：创建 Actor、注册技能、驱动 Tick |
| `ActorRegistry` | ActorId ↔ ECS Entity ↔ ASC 三向映射 |
| `BattleCommandProcessor` | 刷写 Spawn / Damage / Heal / GE / Area / Displace |
| `BattleAgent` / `BattleAiNodes` | GamePlay 侧 BT 叶子，不反向引用 BehaviourTree 业务 |
| `EngageSlotAllocator` | 按追击目标把杂兵铺在环上，避免叠点 |
| `BattleWaveDirector` | 杂兵槽位回收与刷波 |

## GamePlay.Data（配置装配层）

命名空间 `Framework.GamePlay.Data`：读 `cfg.CfgTables`，装配到 GAS，**不含 Tick/模拟**。

| 类型 | 职责 |
|------|------|
| `AbilityConfigFactory` | Luban 技能行 → `GameplayAbilityDef` |
| `EffectConfigFactory` | Luban 效果行 → `GameplayEffectDef` |
| `GamePlayConfigSetup` | `RegisterActorAbilities` 扩展方法 |
| `BattleConfigApplier` | 对 ASC 应用 Luban 效果 |

## Tick 流程

```
GamePlayFramework.Tick(deltaTime)
  0. RebuildActors
  1. SyncCuePose → BT Agent → 定身/眩晕/倒地清速度
  2. 存活 ASC.Tick
  3. Flush Spawn → ECS Tick → Flush 结算 → SyncDeath → Sync Positions
```

## Bootstrap 集成

```
Launch → ConfigModule → GamePlayModule
```

```csharp
using Framework.GamePlay;

var framework = GamePlayModule.Instance.Framework;
```

## 典型用法

```csharp
using Framework.GamePlay;
using Framework.GamePlay.Data;

var framework = new GamePlayFramework();
framework.RegisterActorAbilities(actorId, teamId: 1, abilityIds, ConfigManager.Instance.LoadTables());
framework.Tick(Time.deltaTime);
```

## 完整示例（战斗演示）

进 Battle 场景后由业务入口接管，见 `Assets/Scripts/BattleBootstrap.cs`：

```csharp
using Framework.Config;
using Framework.Core;
using Framework.GamePlay;
using Framework.GamePlay.Data;

var framework = GamePlayModule.Instance.Framework;
framework.CreateActor(heroId, heroPos, 100f, teamId: 1);
framework.CreateActor(monsterId, monsterPos, 100f, teamId: 2);
framework.RegisterActorAbilities(heroId, 1, new[] { "Fireball", "Slash" }, ConfigManager.Instance.LoadTables());
framework.RegisterActorAbilities(monsterId, 2, new[] { "Slash" }, ConfigManager.Instance.GetTables());

void Update()
{
    // WASD 移动；J 近战三连（扇形多目标），K 火球
    framework.TryActivateAbility(heroId, "Slash", new AbilityActivationContext(origin, dir));
    framework.SetBattleAgent(monsterId, BattleAiNodes.CreateMeleeChaserAgent("MobSlash", heroId));
    framework.Tick(Time.deltaTime);
}
// 场景退出：DestroyActor + Unsubscribe；不要 Dispose 模块持有的 Framework
```

## 行为验证示例

- 英雄 WASD 移动；J 近战 Slash→Slash2→Slash3（第三段霸体+倒地）；K 火球；Left Shift 闪避无敌
- 12 只怪物槽位常驻：全灭约 2 秒后在英雄周围复活为下一波（生命随波次增加）
- 杂兵按方位角占位围攻，走槽位、面朝英雄；远距只朝槽位走，靠近后才跑完整 BT
- 无技能/效果的 ASC 跳过 Tick
- 英雄造成伤害时短暂 hit-stop
- 近战命中带击退冲量；Actor 圆形挤开避免叠模
- Fireball 命中扣血；BoomShot 只走爆炸范围伤（不双算直击）
- 配置表 `ability.xlsx` / `effect.xlsx` / `cue.xlsx` 覆盖 AOE、穿透、护盾、DOT、消耗、Cue 表现
- 技能可配 `cooldown_group` / `asset_tags` / `owned_tags` / `cancel_tags`；`CancelAbilitiesWithTag` 按 Tag 打断
- 战斗演示 `BattleCuePresenter` 按 `tbcue` 播放球体 Cue（Add 跟随 Actor）

## 依赖关系

```
GamePlay
 ├── Config      (Tables 只读)
 ├── Data        (Tables → GAS Def 装配)
 ├── Core / GAS / ECS
 └── BehaviourTree / FixedMath（AI Agent）
```

## 被谁使用

- `Assets/Scripts/Launch.cs` — `GamePlayModule`
- 业务层 — `GamePlayModule.Instance.Framework` 或自行创建实例
