# Framework.Lockstep

帧同步基座（自 Client TrueSync 迁移）：固定逻辑帧调度、输入对齐抽象、回滚/校验相关**底层类型**。  
确定性数学见 `Framework.FixedMath`；确定性物理引擎见 `Framework.LockstepPhysics`。

> **战斗回滚不在这个类上。** `RollbackLockstep` / `StateTracker` 仍未接到 GAS、ECS 和行为树。暂停现在只在 `RUNNING` 时推进；校验和一旦不一致就锁存，不会被后一条消息改回成功。构造时如果传入了物理管理器，会调用 `ChecksumExtractor.Init`。等输入和整场战斗回滚在另一条草稿（`LockstepHost` + `BattleLockstepSimulation`）里，不在本分支重复实现。当前 Battle 演示仍走 `LocalLockstepHost`。

## 程序集

| 项 | 值 |
|----|---|
| 程序集 | `Framework.Lockstep` |
| 命名空间 | `Framework.Lockstep` |
| 依赖 | `Framework.FixedMath` |

## 已迁移（对应基座 A–C 中的 B/C，及物理接口）

| 能力 | 内容 | 来源 |
|------|------|------|
| **B 锁步调度** | `AbstractLockstep` / `DefaultLockstep` / `RollbackLockstep`、缓冲窗、玩家帧数据 | `TrueSyncDll` |
| **C 输入/通讯抽象** | `ICommunicator`、`OnEventReceived`、`SyncedData`、`InputDataBase`、输入回调委托 | `TrueSyncDll` |
| 回滚辅助 | `StateTracker`、资源池、`IWorld` / `IWorldClone` | `TrueSyncDll` |

> 上表「回滚辅助」仅为 **TrueSync 底层类型**。`RollbackLockstep` 里的 `StateTracker` 调用仍是注释，**不能**靠它还原 GAS/ECS/行为树。整场回滚用快照接口，不在本分支。

| 校验/录像类型 | `ChecksumExtractor`、`ReplayRecord*`、`ReplayMode` | `TrueSyncDll` |
| 物理契约 | `IPhysicsManagerBase`、`IPhysicsManager`、`IBody` / `IBody2D` / `IBody3D`、`ICollider` | `TrueSyncDll` + Physics 根接口 |
| 行为钩子 | `ITrueSyncBehaviour*`、`TrueSyncManagedBehaviour`（旧 TrueSync 行为桥） | `TrueSyncDll` |

## 明确缺失 / 未迁（后续补）

| 缺口 | 说明 |
|------|------|
| **Unity 场景桥** | `TrueSyncManager`、`TSCollider*`、`PhysicsWorldManager`、`TrueSyncBehaviour` Mono 组件未迁；需按「模拟/表现分离」重做薄封装 |
| **业务锁步壳** | Client `LockStep/LockStepManager` 等进房开战流程不进基座 |
| **网络适配实现** | 仅有 `ICommunicator`；无 Photon/自研网关实现 |
| **与 GamePlay/GAS 对接** | 已有 `LocalLockstepHost` + 意图帧队列；网络 `ICommunicator` 与等输入缓冲未做 |
| **预测回滚 / 状态快照** | `RollbackLockstep` / `StateTracker` 未接业务层。ECS 与 `TSRandom` 已有 `ISnapshotable`；GAS、Farseer 接触、行为树仍不能还原。预测回滚未接线 |
| **干净 Host API** | 仍偏 TrueSync 原 API（回调一长串），未提供 Framework 风格的最小 `LockstepHost` 门面 |
| **Editor 工具** | TrueSync Editor/调试面板未迁 |
| **Coroutine 引擎** | Client `Engine/Coroutine` 未迁（锁步内协程若需要再补） |
| **文档化示例** | 战斗 Demo 按 F8：影子 Session 重放内存录像并比对 `BattleFrameChecksum` |
| **反作弊接线** | `ChecksumExtractor` / `checksumOk` 未接 Host；无业务侧告警；内存混淆见 FixedMath（刻意无 Obscured） |

## 相关模块

- **A 定点数学**：`Assets/Framework/FixedMath/`
- **D 确定性物理**：`Assets/Framework/LockstepPhysics/`（Jitter 3D + Farseer 2D）

## 使用注意

1. 逻辑帧必须使用固定 `lockedTimeStep`，不要用 `Time.deltaTime` 直接驱动权威模拟。  
2. 热路径使用 `Framework.FixedMath`（`FP`/`TSVector`），禁止引入第二套定点库。  
3. 网络只同步输入；实现 `ICommunicator` 后接入 `AbstractLockstep`。  
4. 需要刚体物理时引用 `Framework.LockstepPhysics`，并实现 `IPhysicsManagerBase`（Unity 映射可用 `IPhysicsManager`）。

## 典型引用

```csharp
using Framework.Lockstep;
using Framework.FixedMath;

// 自行实现 ICommunicator / IPhysicsManagerBase 后：
// AbstractLockstep.NewInstance(lockedDt, communicator, physics, ...);
```
