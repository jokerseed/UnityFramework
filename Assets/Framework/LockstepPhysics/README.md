# Framework.LockstepPhysics

确定性物理引擎插件（自 Client TrueSync Physics 迁移）：定点化 **Jitter（3D）** 与 **Farseer（2D）**。

## 程序集

| 项 | 值 |
|----|---|
| 程序集 | `Framework.LockstepPhysics` |
| 命名空间 | `Framework.Lockstep.Physics3D` / `Framework.Lockstep.Physics2D` |
| 依赖 | `Framework.FixedMath`、`Framework.Lockstep` |

## 已迁移（基座 D）

| 内容 | 路径 | 说明 |
|------|------|------|
| 3D | `Jitter/` | 刚体、碰撞、岛、约束等（定点） |
| 2D | `Farseer/` | Box2D 系定点移植 + rollback clone 辅助 |
| 许可 | `Jitter/license.txt`、`Farseer/license.txt`、`readme.txt` | 上游许可保留 |

## 明确缺失 / 未迁

| 缺口 | 说明 |
|------|------|
| **Unity 包装** | `PhysicsWorldManager` / `Physics2DWorldManager`、`TS*Collider`、层矩阵编辑器未迁 |
| **开箱管理器** | 需自行实现 `IPhysicsManager` / `IPhysicsManagerBase` 把 World 挂到锁步；`Stubs/ClientBridgeStubs.cs` 仅为编译占位 |
| **Client 专有桥** | `PhysicsWorldManager` / `FrameWriterSystem` 等仅有空壳，完整场景桥见缺失项「Unity 包装」 |
| **与现有 ECS 战斗** | 已接：`BattlePhysicsWorld` 用 Farseer 2D 做 Actor 挤开、位移、击退与弹道传感器；GAS 查询仍走 `SpatialHashGrid`。Jitter 3D / 场景碰撞体未接 |
| **性能/裁剪** | 未做按需裁剪（整包迁入）。Jitter 与 Farseer 在同一个 `Framework.LockstepPhysics` 程序集里，引用 Farseer 的战斗模块仍会编译 3D |
| **Client 专有补丁核对** | 已机械改命名空间；与主干逐文件 diff/对拍尚未做 |

## Jitter 是否可拆成可选程序集

当前战斗只走 Farseer。`BattlePhysicsWorld` 不引用 `Framework.Lockstep.Physics3D` 的类型。仓库里也没有其他程序集的源码引用 Jitter 类型；`PhysicsManager` 占位在 `Stubs/`，和 Jitter 同程序集，供 Jitter 碰撞过滤编译通过。

因此 Jitter 对战斗模拟是死代码，但还不能靠“别的程序集没引用”把它从玩家包里裁掉。建议的拆法是在 `Jitter/` 下放独立 asmdef（例如 `Framework.LockstepPhysics.Jitter`），只引用 `Framework.FixedMath`、`Framework.Lockstep` 和仍包含 Farseer 与 `Stubs` 的 `Framework.LockstepPhysics`。父程序集不能反向引用 Jitter，否则循环。3D 项目再显式引用新程序集。

这次没有落地拆分：Jitter 调用父程序集里的 `PhysicsManager`，且这里没有 Unity 编译环境可以确认拆开后没有漏类型。拆错会让 3D 物理整包编不过，而战斗本身并不因此变快或更确定。

## 使用注意

- 物理步进必须与锁步 `lockedTimeStep` 一致。  
- 表现层用 float 插值；权威状态留在定点物理世界。  
- 原 `readme.txt`：改动包括 float→FixedPoint、无裸随机、稳定排序、rollback 支持。
