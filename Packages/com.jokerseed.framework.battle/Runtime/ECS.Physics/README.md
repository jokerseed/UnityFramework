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
| `PhysicsSimulationSystem` | 把 ECS 速度写入刚体、步进、回写 Transform，并处理投射物命中（接触 + 线段扫掠，见下文） |

`GamePlayFramework` 创建物理世界后会 `RegisterSingleton<BattlePhysicsWorld>`，并再注册成 `IEcsBodyRemover`。销毁实体时 ECS 只调这个接口。自己组 World 时也要注册，否则刚体不会跟着实体卸掉。

`SaveFull` 会先 `ProcessChanges`，把还在添加列表里的刚体收进世界再克隆。`RestoreFull` 也先 `ProcessChanges`，再恢复、再丢掉字典里已经不在世界上的刚体。这样快照之后才生成、还没步进的投射物会被恢复逻辑删掉，而不是留在世界上却没有实体管理。恢复后按世界里的刚体重建实体到刚体的映射。

## 回滚与退役刚体

Farseer 的世界克隆持有刚体实例，不会重建快照之后被删掉的刚体。锁步回滚时把 `RetainRemovedBodies` 打开（`BattleLockstepSimulation` 默认打开）：删除实体时刚体只停用并记进退役表，回滚到删除之前的检查点会由克隆恢复成启用状态，已经销毁又被快照恢复的投射物照常碰撞。`SaveFull` 记下物理步数和退役表；主机丢弃旧检查点时调用 `PurgeRetiredBefore(最老检查点的 StepCount)`，早于它退役的刚体才真正移出世界。单机默认关闭，删除即释放。

世界克隆只还原宽相树，不还原各 Fixture 的代理表。本地修改的 `WorldClone2D.Restore` 会按恢复后的树重建代理表，所以快照之后停用（死亡）或退役的刚体，回滚后再移动、再停用都能正确更新宽相。

## 投射物命中

除了 Farseer 接触，还用“当前位置减去速度乘步长”得到上一步位置，检查这段线段与目标圆（目标半径加投射物半径）是否相交，高速投射物一步跨过目标也能命中。候选按目标中心在飞行方向上的投影排序，取最近的一个；投影相同取实体 ID 小的。爆炸中心取线段上离目标最近的点。

## 不负责

- 生命、技能、伤害。那些在 GAS。
- 扇形和半径查询。那些走 `SpatialHashGrid`。
