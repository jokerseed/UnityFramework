# Framework.ECS.Physics

战斗用的 Farseer 2D 世界。程序集单独拆开，是为了让 `Framework.ECS` 不引用物理引擎。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.ECS.Physics` |
| 命名空间 | `Framework.ECS`（系统在 `Framework.ECS.Systems`） |
| 依赖 | `Framework.Core`、`Framework.Battle.Core`、`Framework.ECS`、`Framework.FixedMath`、`Framework.Lockstep`、`Framework.LockstepPhysics` |

## 类型

| 类型 | 职责 |
|------|------|
| `BattlePhysicsWorld` | XZ 平面圆体、无重力。Actor 是动态刚体，投射物是运动学传感器。实现 `IEcsBodyRemover` |
| `BattlePhysicsSnapshot` | 整份 Farseer 世界的内存克隆（接触、宽相、岛、TOI），不是字节流 |
| `PhysicsSimulationSystem` | 把 ECS 速度写入刚体、步进、回写 Transform，并处理投射物命中 |

`GamePlayFramework` 创建物理世界后会 `RegisterSingleton<BattlePhysicsWorld>`，并再注册成 `IEcsBodyRemover`。销毁实体时 ECS 只调这个接口。自己组 World 时也要注册，否则刚体不会跟着实体卸掉。

`SaveFull` 会先 `ProcessChanges`，把还在添加列表里的刚体收进世界再克隆。`RestoreFull` 也先 `ProcessChanges`，再恢复、再丢掉字典里已经不在世界上的刚体。这样快照之后才生成、还没步进的投射物会被恢复逻辑删掉，而不是留在世界上却没有实体管理。

## 不负责

- 生命、技能、伤害。那些在 GAS。
- 扇形和半径查询。那些走 `SpatialHashGrid`。
