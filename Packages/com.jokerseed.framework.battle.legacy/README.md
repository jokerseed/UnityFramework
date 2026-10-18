# Framework Battle Legacy

从 `com.jokerseed.framework.battle` 拆出来的可选部分。战斗包不引用这里的任何程序集，不装这个包也能完整使用战斗包。

| 目录 | 程序集 | 内容 |
|---|---|---|
| `Runtime/Lockstep.Legacy` | `Framework.Lockstep.Legacy` | TrueSync 时代的锁步调度器（`AbstractLockstep`、`RollbackLockstep`、`StateTracker` 等），见 [Lockstep.Legacy 的 README](Runtime/Lockstep.Legacy/README.md) |
| `Runtime/Jitter` | `Framework.LockstepPhysics.Jitter` | Jitter 3D 定点物理引擎，引用战斗包里的 `Framework.LockstepPhysics` |

两个程序集的名字、命名空间和 `.meta` GUID 与拆分前相同，已有代码和 asmdef 引用不用改。两者的 `autoReferenced` 都是 false，要用时在自己的 asmdef 里显式引用。

## 为什么单独成包

- 新的对局与回滚走战斗包里的 `LockstepHost` + `ILockstepSimulation`，旧调度器只为兼容已有的 TrueSync 接入代码保留。
- 战斗只用 Farseer 2D，Jitter 3D 没有接入。单独成包后，只用战斗包的项目不会把这两部分一起编译、分发。
- 授权不同：Jitter 是 zlib；旧调度器里有来自 Photon TrueSync 的文件，**使用方须自行确认授权**。所以本包的 `license` 写的是 `SEE LICENSE IN THIRD_PARTY_NOTICES.md`，详见仓库根目录的 [THIRD_PARTY_NOTICES.md](../../THIRD_PARTY_NOTICES.md)。

`Framework.Lockstep` 里 `Core/TrueSync` 下的接口是 Farseer 依赖的基础类型，仍留在战斗包。
