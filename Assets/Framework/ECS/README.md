# Framework.ECS

轻量 ECS 层，处理空间、移动、投射物碰撞等**热路径模拟**。不持有 GAS 规则数据。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.ECS` |
| 命名空间 | `Framework.ECS` |
| 依赖 | `Framework.Core` |

## 目录结构

```
ECS/
├── World.cs                  ECS 世界，Phase 调度、Singleton、组件存储
├── Entity.cs                 实体与 ComponentStorage
├── ISystem.cs                IComponent / ISystem / EcsSystemPhase
├── EntityQuery.cs            ForEach 组件交集查询
├── SpatialHashGrid.cs        空间哈希（HashSet 去重）
├── SpatialIndexService.cs    Actor 空间索引重建
├── Components/
│   └── BattleComponents.cs   Transform、Velocity、Projectile 等
└── Systems/
    └── BattleSystems.cs      移动、碰撞、生命周期、空间索引
```

## 核心能力

| 能力 | 类型 |
|------|------|
| System Phase | `EcsSystemPhase.Simulate` / `Cleanup` |
| 组件查询 | `world.ForEach<TDriver, TRequired>(...)` |
| 共享单例 | `World.RegisterSingleton` / `GetSingleton<T>` |
| 空间索引 | `SpatialHashGrid`（HashSet 池化） + `SpatialIndexService.RebuildActors` |

## 组件一览

| 组件 | 字段 | 用途 |
|------|------|------|
| `TransformComponent` | Position, Forward | 世界坐标 |
| `VelocityComponent` | Value | 速度 |
| `ProjectileComponent` | Owner, AbilityId, Damage, Radius, RemainingLifetime, TeamId, PierceRemaining, HitEffectId, ExplodeRadius, DamageType | 投射物 |
| `KnockbackComponent` | Velocity, Remaining | 击退冲量 |
| `ActorLinkComponent` | ActorId | 关联 GAS ASC |
| `CombatStateComponent` | IsAlive | 存活标记（同步自 GAS） |
| `TeamComponent` | TeamId | 阵营 |
| `CollisionComponent` | Radius | 碰撞半径 |

## System 一览

| System | Phase | 职责 |
|--------|-------|------|
| `MovementSystem` | Simulate | 按速度更新位置；定身由 GamePlay 在 Tick 前把速度写 0 |
| `KnockbackSystem` | Simulate | 击退冲量叠加位移，到期移除 |
| `ActorSeparationSystem` | Simulate | 存活 Actor 圆 vs 圆挤开 |
| `SpatialIndexSystem` | Simulate | Movement 后重建 Actor 空间索引 |
| `ProjectileCollisionSystem` | Simulate | 圆 vs 圆；支持穿透与命中爆炸（入队范围命令） |
| `ProjectileLifetimeSystem` | Simulate | 投射物超时销毁 |

注册顺序：`Movement → Knockback → Separation → SpatialIndex → ProjectileCollision → ProjectileLifetime`

## Tick 时序（GamePlay 驱动）

```
GamePlayFramework.Tick
  1. SpatialIndexService.RebuildActors   ← 供 GAS 目标查询（SpatialHash broadphase）
  2. SyncCuePose → BT Agent → 定身/眩晕清速度
  3. 存活 ASC.Tick（无 Active 技能/效果则跳过）
  4. Flush Spawn
  5. World.Tick (Simulate Phase)
       Movement → SpatialIndex → ProjectileCollision → ProjectileLifetime
  6. Flush Damage / Heal / GE / Area / Displace
  7. SyncDeath → Sync Positions
```

GAS 查询与 ECS 碰撞各重建一次 Actor 空间索引：查询前用「上帧 Sync 后」的 Transform；碰撞前用 Movement 后的 Transform。

## 与 GAS 的协作

- ECS 通过 `ActorLinkComponent.ActorId` 关联 GAS 的 `AbilitySystemComponent`
- `ActorRegistry` 目标查询走 `SpatialHashGrid` broadphase + GAS Tag narrowphase
- 碰撞命中时写入 `ApplyDamageCommand` 到 `BattleCommandBuffer`，由 GamePlay 刷写后交给 GAS 结算
- **不在 ECS 组件中存储生命值/攻击力**，避免双源数据

## Query 示例

```csharp
// 只遍历同时拥有 Velocity 与 Transform 的实体
world.ForEach<VelocityComponent, TransformComponent>((entityId, velocity, transform) =>
{
    transform.Position += velocity.Value * deltaTime;
    world.GetStorage<TransformComponent>().Add(entityId, transform);
});
```

## 被谁使用

- `Framework.GamePlay` — 创建 `World`、注册 System、驱动 Tick、`ActorRegistry` 空间查询
- `Framework.GamePlay.ActorRegistry` — 创建 Entity 并绑定 ActorId
