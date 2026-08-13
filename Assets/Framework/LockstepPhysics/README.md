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
| **与现有 ECS 战斗** | Framework 当前 `Framework.ECS` 弹道仍是 float；未自动替换为本物理 |
| **性能/裁剪** | 未做按需裁剪（整包迁入）；2D/3D 可按项目只引用其一并关系统 |
| **Client 专有补丁核对** | 已机械改命名空间；与主干逐文件 diff/对拍尚未做 |

## 使用注意

- 物理步进必须与锁步 `lockedTimeStep` 一致。  
- 表现层用 float 插值；权威状态留在定点物理世界。  
- 原 `readme.txt`：改动包括 float→FixedPoint、无裸随机、稳定排序、rollback 支持。
