# Framework.Lockstep

帧同步基座（自 Client TrueSync 迁移）：固定逻辑帧调度、输入对齐抽象、回滚/校验相关**底层类型**。  
确定性数学见 `Framework.FixedMath`；确定性物理引擎见 `Framework.LockstepPhysics`。

> 战斗回滚走 `LockstepHost` + `BattleLockstepSimulation`（GAS、ECS、随机数、Farseer 接触克隆、已绑定 Agent 的行为树）。`RollbackLockstep` / `StateTracker` 已退役：前者只克隆物理世界，后者按引用保存字段，都标了 `[Obsolete]`。单机 Demo 仍走 `LocalLockstepHost`。真实网络未接，测试用 `LoopbackHub`。
>
> 旧调度器上的修正：暂停只在 `RUNNING` 时推进；校验和一旦不一致就锁存，不会被后一条消息改回成功。构造时如果传入了物理管理器，会调用 `ChecksumExtractor.Init`。本地掉线票对每个来源只记一次，不再每帧加一。`Clear` 会清掉各玩家的票，并把本场输入池从静态列表摘掉。只有玩家 id、没有校验和的 198 包会被拒绝，不会把 tick 0 标成失步。

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

> `RollbackLockstep` 和 `StateTracker` 已退役，不要再接到 GamePlay。`NewInstance` 在 `rollbackWindow > 0` 时仍会构造 `RollbackLockstep`，那只为了旧 TrueSync 物理克隆，不还原 GAS、ECS 或行为树。战斗回滚只用 `LockstepHost`。

| 校验/录像类型 | `ChecksumExtractor`、`ReplayRecord*`、`ReplayMode` | `TrueSyncDll` |
| 物理契约 | `IPhysicsManagerBase`、`IPhysicsManager`、`IBody` / `IBody2D` / `IBody3D`、`ICollider` | `TrueSyncDll` + Physics 根接口 |
| 行为钩子 | `ITrueSyncBehaviour*`、`TrueSyncManagedBehaviour`（旧 TrueSync 行为桥） | `TrueSyncDll` |

## 明确缺失 / 未迁（后续补）

| 缺口 | 说明 |
|------|------|
| **Unity 场景桥** | `TrueSyncManager`、`TSCollider*`、`PhysicsWorldManager`、`TrueSyncBehaviour` Mono 组件未迁；需按「模拟/表现分离」重做薄封装 |
| **业务锁步壳** | Client `LockStep/LockStepManager` 等进房开战流程不进基座 |
| **网络适配实现** | 仅有 `ICommunicator`；无 Photon/自研网关实现 |
| **与 GamePlay/GAS 对接** | `LockstepHost` 等输入；`BattleLockstepSimulation` 把一帧输入推进 `BattleSession`。`LocalLockstepHost` 仍是单机意图帧 |
| **预测回滚 / 状态快照** | `LockstepHost` 每隔 N 步让模拟存检查点；迟到或被改写的输入会恢复并重放。战斗检查点含已绑定 Agent 的行为树。`RollbackLockstep` 已退役 |
| **干净 Host API** | `LockstepHost` + `ILockstepSimulation` + `LoopbackHub`。`AbstractLockstep` 仍未接到 GamePlay；它只在 `RUNNING` 时推进，校验和第一次不一致会锁存 |
| **Editor 工具** | TrueSync Editor/调试面板未迁 |
| **Coroutine 引擎** | Client `Engine/Coroutine` 未迁（锁步内协程若需要再补） |
| **文档化示例** | 战斗 Demo 按 F8：影子 Session 重放内存录像并比对 `BattleFrameChecksum` |
| **反作弊接线** | `ChecksumExtractor` / `checksumOk` 未接 Host；无业务侧告警；内存混淆见 FixedMath（刻意无 Obscured） |

## 相关模块

- **A 定点数学**：`Assets/Framework/FixedMath/`
- **D 确定性物理**：`Assets/Framework/LockstepPhysics/`（Jitter 3D + Farseer 2D）

## 等输入与回滚

`LoopbackHub.Open` 得到 `LoopbackCommunicator`。`HoldOutgoing` 先把出向事件留在本端，`Flush` 再按顺序送给其他对端。

`LockstepHost` 在某一步收齐所有玩家的 `LockstepInputMessage` 之前不会调用 `Simulate`。`PredictRemote` 可以先填占位；正式输入若不同，就从最近的检查点重放。检查点间隔至少为 1，第 0 步一定会存。

战斗侧是 `BattleLockstepSimulation`：随机数、ECS、`GasSnapshot`（版本 3）、`BattlePhysicsSnapshot`，以及当时已绑定 Agent 的行为树快照记在同一次检查点里。`World.Restore` 之后要重新绑定 Actor。Farseer 的刚体 ID 仍是进程全局的，同一进程里两场世界同时回滚并不隔离刚体 ID。效果 Spec 的 RuntimeId、技能实例 ID 和活跃效果句柄在每个 ASC 上，随该 ASC 的快照回写。测试只比较已经记下的校验和，开局之后不再创建刚体。

`AbstractLockstep` 每场有自己的 `SyncedData` 池，输入工厂绑在这个池上，不再跟随后创建的另一场去改写已经取出的对象。`SyncedData.pool` 和 `AbstractLockstep.instance` 仍留给 `ReplayRecord` 和没传入池的旧调用。`ReplayRecord.replayMode` 仍是进程级开关。网络事件 199/198/196/197 先做长度和玩家校验，畸形包计入 `RejectedNetworkInputs` 后丢弃。掉线票按来源玩家去重，同一个客户端重复发送不会把票数刷过门槛。`StateTracker` 不再接回战斗。

真实套接字不在这里实现。

## 使用注意

1. 逻辑帧必须使用固定 `lockedTimeStep`，不要用 `Time.deltaTime` 直接驱动权威模拟。  
2. 热路径使用 `Framework.FixedMath`（`FP`/`TSVector`），禁止引入第二套定点库。  
3. 网络只同步输入。新的对局用 `LockstepHost`；旧的 `AbstractLockstep` 仍可接 `ICommunicator`，但没有接到 GamePlay。  
4. 需要刚体物理时引用 `Framework.LockstepPhysics`，并实现 `IPhysicsManagerBase`（Unity 映射可用 `IPhysicsManager`）。

## 典型引用

```csharp
using Framework.Lockstep;
using Framework.FixedMath;

// 自行实现 ICommunicator / IPhysicsManagerBase 后：
// AbstractLockstep.NewInstance(lockedDt, communicator, physics, ...);
```
