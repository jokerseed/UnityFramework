# Framework.LockstepPhysics

确定性物理引擎插件（自 Client TrueSync Physics 迁移）：定点化 **Farseer（2D）**。

> **Jitter（3D）已移到可选包** `com.jokerseed.framework.battle.legacy`（`Runtime/Jitter`，程序集名 `Framework.LockstepPhysics.Jitter` 与 GUID 不变）。战斗只用 Farseer，不装那个包也能完整使用本包。下文涉及 Jitter 的说明对那个包同样适用。

## 程序集

| 项 | 值 |
|----|---|
| 程序集 | `Framework.LockstepPhysics`（Farseer 2D 与 `Stubs`） |
| 可选程序集 | `Framework.LockstepPhysics.Jitter`（在可选包 `com.jokerseed.framework.battle.legacy` 里，`autoReferenced` 为 false） |
| 命名空间 | `Framework.Lockstep.Physics2D` / `Framework.Lockstep.Physics3D` |
| 依赖 | 父程序集：`Framework.FixedMath`、`Framework.Lockstep`。Jitter 再引用父程序集，父程序集不引用 Jitter |

## 已迁移（基座 D）

| 内容 | 路径 | 说明 |
|------|------|------|
| 3D | 可选包的 `Runtime/Jitter/` | 刚体、碰撞、岛、约束等（定点） |
| 2D | `Farseer/` | Box2D 系定点移植 + rollback clone 辅助 |
| 许可 | `Farseer/license.txt`、`readme.txt`；Jitter 的许可在可选包里 | 上游许可保留 |

## 明确缺失 / 未迁

| 缺口 | 说明 |
|------|------|
| **Unity 包装** | `PhysicsWorldManager` / `Physics2DWorldManager`、`TS*Collider`、层矩阵编辑器未迁 |
| **开箱管理器** | 需自行实现 `IPhysicsManager` / `IPhysicsManagerBase` 把 World 挂到锁步；`Stubs/ClientBridgeStubs.cs` 仅为编译占位 |
| **Client 专有桥** | `PhysicsWorldManager` / `FrameWriterSystem` 等仅有空壳，完整场景桥见缺失项「Unity 包装」 |
| **与现有 ECS 战斗** | 已接：`BattlePhysicsWorld` 用 Farseer 2D 做 Actor 挤开、位移、击退与弹道传感器；GAS 查询仍走 `SpatialHashGrid`。Jitter 3D / 场景碰撞体未接 |
| **性能/裁剪** | Jitter 已拆到 `Framework.LockstepPhysics.Jitter`。`Framework.ECS` 只引用父程序集，战斗代码不编译 3D 类型。Unity 仍会单独编译 Jitter 程序集；玩家包是否裁掉取决于托管剥离。要用 3D，在自己的 asmdef 里显式引用 `Framework.LockstepPhysics.Jitter` |
| **Client 专有补丁核对** | 已机械改命名空间；与主干逐文件 diff/对拍尚未做 |

## Jitter 可选程序集

战斗只走 Farseer。`BattlePhysicsWorld`、`Framework.GamePlay`、`Assets/Scripts` 都不引用 `Framework.Lockstep.Physics3D`。`Framework.ECS` 的 asmdef 只引用 `Framework.LockstepPhysics`。

可选包里的 `Framework.LockstepPhysics.Jitter.asmdef` 引用 `Framework.FixedMath`、`Framework.Lockstep` 和父程序集（`Stubs/ClientBridgeStubs.cs` 里的 `PhysicsManager` 仍留在父程序集）。父程序集不引用 Jitter，避免循环。`autoReferenced` 为 false，预定义程序集不会自动带上 3D。

这次没有用 Unity 编译确认拆分。若 Jitter 源码实际用了父程序集里的 internal 类型，打开工程后 Jitter 程序集会报错，把对应类型改为 public 或把该文件留在父程序集即可。战斗程序集不应因此失败。

## 使用注意

- 物理步进必须与锁步 `lockedTimeStep` 一致。  
- 表现层用 float 插值；权威状态留在定点物理世界。  
- 原 `readme.txt`：改动包括 float→FixedPoint、无裸随机、稳定排序、rollback 支持。
