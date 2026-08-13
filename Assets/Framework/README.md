# Framework.Battle — GAS + ECS 战斗框架

## 设计原则

| 原则 | 实现 |
|------|------|
| 单一数据源 | GAS 规则权威；ECS 仅存 `CombatStateComponent.IsAlive` |
| 热路径命令化 | `BattleCommandBuffer` 批量刷；表现走 `IEventBus`（`ZeroGcEventBus`） |
| 配置驱动 | Luban 导表 → `GamePlay.Data` 装配到 GAS |
| 资源管线 | YooAsset 打包；运行时加载/释放统一走 `ResourceManager` |
| 模块编排 | `GameBootstrap` 按依赖拓扑排序初始化各 `IGameModule` |
| 示例文档化 | 演示与验证用例写在各模块 README，不单独维护 Samples/Tests 程序集 |

## 帧同步与回滚（能力边界）

| 项 | 状态 |
|----|------|
| 定点 / 锁步 / 物理 / 行为树基座 | 已迁移或已实现 |
| 战斗 Demo 锁步权威 Tick | 未接（仍 `deltaTime`） |
| **预测回滚 / 状态快照** | **未实现** |
| **反作弊（checksum 接线 / Obscured 适配）** | **未实现**（校验类型已迁；FixedMath 刻意无 Obscured） |

详见根 [README 反作弊](../../README.md#反作弊)、[Lockstep/README.md](Lockstep/README.md)、[FixedMath/README.md](FixedMath/README.md)、[BehaviourTree/README.md](BehaviourTree/README.md)。

## 程序集与文档

| 程序集 | 职责 | 文档 |
|--------|------|------|
| `Framework.Bootstrap` | 模块启动编排（Host + `IGameModule`） | [Bootstrap/README.md](Bootstrap/README.md) |
| `Framework.Core` | CommandBuffer、BattleContext、标识符 | [Core/README.md](Core/README.md) |
| `Framework.Events` | `IEventBus` 契约与零 GC 实现 | [Events/README.md](Events/README.md) |
| `Framework.GAS` | ASC、伤害管线、Effect、Tag | [GAS/README.md](GAS/README.md) |
| `Framework.ECS` | World、System、空间哈希 | [ECS/README.md](ECS/README.md) |
| `Framework.GamePlay` | `GamePlayFramework` 玩法主入口 | [GamePlay/README.md](GamePlay/README.md) |
| `Framework.Config` | Luban 加载与 Tables 缓存 | [Config/README.md](Config/README.md) |
| `Framework.Res` | YooAsset 封装（`ResourceManager`） | [Res/README.md](Res/README.md) |
| `Framework.MemoryPool` | 轻量内存池（`IMemory`） | [MemoryPool/README.md](MemoryPool/README.md) |
| `Framework.ObjectPool` | 对象池（`ObjectBase` / 容量过期） | [ObjectPool/README.md](ObjectPool/README.md) |
| `Framework.Coroutine` | 协程（Global / Scene / GameObject） | [Coroutine/README.md](Coroutine/README.md) |
| `Framework.FixedMath` | 确定性定点数（锁步基座 A） | [FixedMath/README.md](FixedMath/README.md) |
| `Framework.Lockstep` | 帧同步调度与输入抽象（基座 B/C） | [Lockstep/README.md](Lockstep/README.md) |
| `Framework.LockstepPhysics` | 确定性 2D/3D 物理（基座 D） | [LockstepPhysics/README.md](LockstepPhysics/README.md) |
| `Framework.BehaviourTree` | 帧同步友好的 AI 行为树运行时 | [BehaviourTree/README.md](BehaviourTree/README.md) |
| `Framework.UI` | UI 窗口管理（`UIManager` / `UIWindow`） | [UI/README.md](UI/README.md) |
| `Framework.Editor` | Luban / YooAsset 编辑器工具 | [Editor/README.md](Editor/README.md) |
| `Generated.Luban` | Luban 生成配置代码 | [../Generated/README.md](../Generated/README.md) |

## 示例与验证（写在 README）

| 内容 | 位置 |
|------|------|
| 启动流程 + 配置加载 | `Assets/Scripts/Launch.cs`、[Config/README.md](Config/README.md) |
| 完整战斗演示 | [GamePlay/README.md](GamePlay/README.md) → 完整示例 |
| ASC 伤害/效果/标签验证 | [GAS/README.md](GAS/README.md) → 行为验证示例 |

## 模块依赖图

```
Bootstrap (Host)
    │
    ├── Logging (LoggingModule)       Infrastructure
    ├── Coroutine (CoroutineModule)   Infrastructure（依赖 Logging）
    ├── MemoryPool (MemoryPoolModule) Infrastructure
    ├── ObjectPool (ObjectPoolModule) Infrastructure（依赖 MemoryPool）
    ├── Res (ResourceModule)          Infrastructure
    │       ├── UI (UIModule)             Presentation（依赖 Resource + Coroutine）
    │       └── Config (ConfigModule)     Data
    │               └── GamePlay (GamePlayModule)  Gameplay
    │                       ├── GAS
    │                       └── ECS
    │                               └── Core
    │
    ├── FixedMath / Lockstep / LockstepPhysics / BehaviourTree（帧同步与 AI 基座，按需引用，非 IGameModule）
    └── Editor (Editor only)
```

## 启动流程

```
Launch.Awake
  → GameBootstrap.SetModules([ResourceModule, ConfigModule, GamePlayModule, ...])
  → 拓扑排序 → 依次 InitializeAsync
  → IsReady → 业务逻辑
```

启动入口：`Assets/Scripts/Launch.cs`（挂到 `Bundles/Scenes/Launch.unity`）

## Tick 顺序

```
GAS Tick → Flush Spawn → ECS Tick → Flush Damage → Sync Positions
```

## Luban 配置表

策划编辑：

```
Config/Luban/Datas/battle/ability.xlsx
Config/Luban/Datas/battle/effect.xlsx
```

表结构定义：`Config/Luban/Defines/battle.xml`

导表：

| 方式 | 入口 |
|------|------|
| 命令行 | `Config\Luban\gen_client.bat` |
| Unity 菜单 | **Tools → Luban → Generate Client Config** |

产出：

| 类型 | 路径 |
|------|------|
| C# 代码 | `Assets/Generated/Luban/` |
| 二进制 | `Assets/Bundles/Configs/*.bytes` |
| JSON 调试 | `Config/Luban/Output/json/`（不参与运行时） |

## YooAsset 资源

| 步骤 | 菜单 |
|------|------|
| 生成 Collector | **Tools → YooAsset → Generate Collector** |
| 构建 Bundle | **YooAsset → Bundle Builder** |

Collector 规则：
- `Assets/Bundles/Configs/` 下每个 `.bytes` 单独打包
- 寻址：`bundles/configs/{表名}.unity3d`（如 `tbability`）

## 运行时加载配置

```csharp
var tables = ConfigManager.Instance.LoadTables();
// 或 ConfigManager.Instance.LoadLubanTables()（无 CfgTables 单例缓存）
// 单表：ConfigManager.Instance.LoadConfigBytes("tbability", cache: true)
```

## 编辑器工具

| 菜单 | 功能 |
|------|------|
| Tools/Luban/Generate Client Config | 执行 Luban 打表 |
| Tools/YooAsset/Generate Collector | 生成 `BundleCollectorSetting.asset` |
