# Framework.ECS

轻量 ECS 层，处理空间、移动、投射物等**热路径模拟**。不持有 GAS 规则数据。  
位移、挤开、击退与弹道命中由 `BattlePhysicsWorld` 驱动 **Farseer 2D**（`Framework.LockstepPhysics`）；GAS 扇形/半径查询仍走 `SpatialHashGrid`。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.ECS` |
| 命名空间 | `Framework.ECS` |
| 依赖 | `Framework.Core`、`Framework.FixedMath`、`Framework.Lockstep`、`Framework.LockstepPhysics` |

## 目录结构

```
ECS/
├── World.cs                     ECS 世界，Phase 调度、Singleton、组件存储
├── Entity.cs                    实体与按实体 ID 升序的 ComponentStorage（稠密数组）
├── ISystem.cs                   IComponent / ISystem / EcsSystemPhase
├── EntityQuery.cs               ForEach 组件交集查询
├── BattlePhysicsWorld.cs        Farseer 2D 包装（XZ 圆体、无重力）
├── SpatialHashGrid.cs           空间哈希（HashSet 去重）
├── SpatialIndexService.cs       Actor 空间索引重建
├── Snapshot/                    ISnapshotable：实体与内置组件；物理仅运动学扩展点
├── Components/
│   └── BattleComponents.cs      Transform、Velocity、Projectile 等
└── Systems/
    ├── PhysicsSimulationSystem.cs  速度→刚体→Step→回写；投射物命中
    └── BattleSystems.cs            空间索引、投射物寿命
```

## 核心能力

| 能力 | 类型 |
|------|------|
| System Phase | `EcsSystemPhase.Simulate` / `Cleanup`；`ISystem.Update` / `World.Tick` 的 `deltaTime` 为 `FP` |
| 组件存储 | `ComponentStorage<T>` 按实体 ID 升序迭代，查找走稀疏表；插入/删除 O(n)。`foreach` 不装箱 |
| 组件查询 | `foreach (var pair in world.GetStorage<T>())`；`VisitPairs` 供结构体访问者。`ForEach(Action)` 仍可用，捕获变量的 lambda 会分配委托 |
| 共享单例 | `World.RegisterSingleton` / `GetSingleton<T>` |
| 确定性物理 | `BattlePhysicsWorld`：Actor 动态圆、投射物运动学传感器；步进与逻辑 `FP` 步长一致 |
| 空间索引 | `SpatialHashGrid`（定点格子）供 GAS 目标查询；`QueryNearby` 结果按实体 ID 升序 |
| 快照 | `World.Save/Restore` 写入 `SnapshotBuffer`。不含 Farseer 接触；运动学另见 `BattlePhysicsWorld.SaveKinematics`（刚体须已存在） |

未接：Jitter 3D 刚体、关节、地面/墙壁静态几何。当前 Demo 是 XZ 平面割草，只用 Farseer 2D。

## 组件一览

| 组件 | 字段 | 用途 |
|------|------|------|
| `TransformComponent` | Position / Forward 为 `TSVector`；`ToUnityPosition()` 仅给表现 | 世界坐标（物理步进后回写） |
| `VelocityComponent` | Value 为 `TSVector` | 意图速度，写入刚体 `LinearVelocity` |
| `ProjectileComponent` | Radius / RemainingLifetime / ExplodeRadius / Damage 均为 `FP` | 投射物 |
| `KnockbackComponent` | Velocity 为 `TSVector`；Remaining 为 `FP` | 击退冲量，叠到刚体速度 |
| `ActorLinkComponent` | ActorId | 关联 GAS ASC |
| `CombatStateComponent` | IsAlive | 存活标记；死亡时禁用刚体 |
| `TeamComponent` | TeamId | 阵营 |
| `CollisionComponent` | Radius 为 `FP` | 碰撞半径（创建刚体时使用） |

## System 一览

| System | Phase | 职责 |
|--------|-------|------|
| `PhysicsSimulationSystem` | Simulate | 速度+击退写入 Farseer → `Step` → 只回写有刚体的 Transform（实体 ID 升序）；投射物接触入队伤害/爆炸 |
| `SpatialIndexSystem` | Simulate | 物理步进后重建 Actor 空间索引 |
| `ProjectileLifetimeSystem` | Simulate | 投射物超时销毁（`World.DestroyEntity` 卸刚体） |

注册顺序：`PhysicsSimulation → SpatialIndex → ProjectileLifetime`

## Tick 时序（GamePlay 驱动）

```
GamePlayFramework.Tick
  1. 若 SpatialHashGrid.StructuralDirty：RebuildActors
       （步间 Create / Remove / SetPosition / 复活；死亡不单独置脏，窄相位用 Tag 剔除）
  2. SyncCuePose → 定身/眩晕清速度
  3. 存活 ASC.Tick（无 Active 技能/效果则跳过）
       ← GAS 目标查询读的是「本步物理之前」的格子
  4. Flush Spawn（创建 ECS 实体 + Farseer 刚体）
  5. World.Tick (Simulate Phase)
       PhysicsSimulation → SpatialIndex（每个逻辑步唯一一次必做全量重建）→ ProjectileLifetime
  6. Flush Damage / Heal / GE / Area / Displace
       ← 范围结算读的是「本步物理之后」的格子
  7. SyncDeath → Sync Positions
```

纯移动帧只有步骤 5 的那一次全量重建。格子是宽相位；精确距离仍用查询当下的 `TransformComponent`。

## 与 GAS 的协作

- ECS 通过 `ActorLinkComponent.ActorId` 关联 GAS 的 `AbilitySystemComponent`
- `ActorRegistry` 目标查询走 `SpatialHashGrid` broadphase + GAS Tag narrowphase
- 投射物命中由 Farseer 接触写入 `ApplyDamageCommand` / `ApplyAreaEffectCommand`
- **不在 ECS 组件中存储生命值/攻击力**，避免双源数据

## 被谁使用

- `Framework.GamePlay` — 创建 `World`、注册 `BattlePhysicsWorld` 与 System、驱动 Tick
- `Framework.GamePlay.ActorRegistry` — 创建 Entity / 刚体并绑定 ActorId
- 战斗物理只用 Farseer 2D。Jitter 3D 仍打在 `Framework.LockstepPhysics` 里，本程序集不引用其类型。拆分见 LockstepPhysics README
- `BattlePhysicsWorld` 放在本程序集，所以 ECS 会引用 `Framework.LockstepPhysics`。把它拆成单独的物理程序集要连同 `World.DestroyEntity` 和 System 一起搬，这次只把边界写在这里，没有改引用。
