# Framework.Battle.Core

战斗契约：Actor 标识、命令缓冲、战斗上下文。从 `Framework.Core` 拆出来，这样只引用 Core 的模块不会带上战斗类型。

命名空间保持 `Framework.Core` 和 `Framework.Core.Commands`，现有 `using` 不用改。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Battle.Core` |
| 命名空间 | `Framework.Core`、`Framework.Core.Commands` |
| 依赖 | `Framework.Events`、`Framework.FixedMath` |

## 类型

| 类型 | 职责 |
|------|------|
| `ActorId` | 逻辑层 Actor 标识 |
| `BattleContext` | 命令缓冲、表现事件总线、确定性随机 |
| `BattleConstants` | 属性名、标签和物理默认值 |
| `BattleDamageType` | 伤害类型 |
| `IDeterministicRandom` | 模拟随机契约 |
| `BattleCommandBuffer` | 热路径命令 |

GAS 仍使用这些类型，所以 GAS 引用本程序集。Core 本身不再包含它们。
