# Framework.Lockstep.Legacy

自 TrueSync 迁移的旧锁步调度器及其配套类型：`AbstractLockstep` / `DefaultLockstep` / `RollbackLockstep`、`StateTracker`、`SyncedData` / `SyncedInfo`、`TSPlayer`、`ReplayRecord`，以及 OnGUI 统计面板（`TrueSyncStats`、`Drawing`）。

## 定位

- **不参与当前战斗**：联网对局与回滚请用 `Framework.Lockstep` 里的 `LockstepHost` + `ILockstepSimulation`。
- `RollbackLockstep` 只克隆物理世界，不还原 GAS、ECS 和行为树；`StateTracker` 按引用保存字段。两者都标了 `[Obsolete]`。
- 保留它们是为了让已有的 TrueSync 接入代码还能编译，以及保留上面那批调度器修正（暂停推进、校验和锁存、掉线票去重等）的测试。

## 程序集

| 项 | 值 |
|----|---|
| 程序集 | `Framework.Lockstep.Legacy` |
| 命名空间 | `Framework.Lockstep`（与迁移前一致，旧代码不用改 `using`） |
| 依赖 | `Framework.FixedMath`、`Framework.Lockstep` |
| `autoReferenced` | `false`：需要时在自己的 asmdef 里显式引用 |
| 引擎依赖 | 有（OnGUI 统计面板、`ReplayRecord` 等），所以没有和主程序集一起脱离 UnityEngine |
