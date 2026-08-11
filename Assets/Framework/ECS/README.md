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
├── World.cs                  ECS 世界，持有 System 列表
├── Entity.cs                 实体与 ComponentStorage
├── ISystem.cs                IComponent / ISystem 契约
├── SpatialHashGrid.cs        空间哈希，近邻查询
├── Components/
│   └── BattleComponents.cs   Transform、Velocity、Projectile 等
└── Systems/
    └── BattleSystems.cs      移动、碰撞、生命周期、空间索引
```

## 组件一览

| 组件 | 字段 | 用途 |
|------|------|------|
| `TransformComponent` | Position | 世界坐标 |
| `VelocityComponent` | Velocity | 速度 |
| `ProjectileComponent` | Speed, Lifetime, Damage, Source | 投射物 |
| `ActorLinkComponent` | ActorId | 关联 GAS ASC |
| `CombatStateComponent` | IsAlive | 存活标记（同步自 GAS） |
| `TeamComponent` | TeamId | 阵营 |
| `CollisionComponent` | Radius | 碰撞半径 |

## System 一览

| System | 职责 |
|--------|------|
| `SpatialIndexSystem` | 重建空间哈希 |
| `MovementSystem` | 按速度更新位置 |
| `ProjectileCollisionSystem` | 投射物与 Actor 碰撞检测 |
| `ProjectileLifetimeSystem` | 投射物超时销毁 |

## Tick 顺序

在 `BattleFramework.Tick` 中，ECS 在 GAS Tick 和命令刷写之后执行：

```
GAS Tick → Flush Spawn → ECS Tick → Flush Damage → Sync Positions
```

## 与 GAS 的协作

- ECS 通过 `ActorLinkComponent.ActorId` 关联 GAS 的 `AbilitySystemComponent`
- 碰撞命中时写入 `ApplyDamageCommand` 到 `BattleCommandBuffer`，由 Bridge 刷写后交给 GAS 结算
- **不在 ECS 组件中存储生命值/攻击力**，避免双源数据

## 被谁使用

- `Framework.Bridge` — 创建 `World`、注册 System、驱动 Tick
- `Framework.Bridge.ActorRegistry` — 创建 Entity 并绑定 ActorId
