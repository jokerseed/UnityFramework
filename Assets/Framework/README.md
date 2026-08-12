# Framework.Battle — GAS + ECS 战斗框架

## 设计原则

| 原则 | 实现 |
|------|------|
| 单一数据源 | GAS 规则权威；ECS 仅存 `CombatStateComponent.IsAlive` |
| 热路径命令化 | `BattleCommandBuffer` 批量刷；表现走 `IEventBus`（`ZeroGcEventBus`） |
| 配置驱动 | Luban 导表 → `AbilityFactory` / `EffectFactory` |
| 资源管线 | YooAsset 打包加载；配置表走 `ResourceManager` |
| 模块编排 | `GameBootstrap` 按依赖拓扑排序初始化各 `IGameModule` |
| 示例文档化 | 演示与验证用例写在各模块 README，不单独维护 Samples/Tests 程序集 |

## 程序集与文档

| 程序集 | 职责 | 文档 |
|--------|------|------|
| `Framework.Bootstrap` | 模块启动编排（Host + `IGameModule`） | [Bootstrap/README.md](Bootstrap/README.md) |
| `Framework.Core` | CommandBuffer、BattleContext、标识符 | [Core/README.md](Core/README.md) |
| `Framework.Events` | `IEventBus` 契约与零 GC 实现 | [Events/README.md](Events/README.md) |
| `Framework.GAS` | ASC、伤害管线、Effect、Tag | [GAS/README.md](GAS/README.md) |
| `Framework.ECS` | World、System、空间哈希 | [ECS/README.md](ECS/README.md) |
| `Framework.Bridge` | `BattleFramework` 入口 | [Bridge/README.md](Bridge/README.md) |
| `Framework.Config` | Luban 加载、Ability/Effect 工厂 | [Config/README.md](Config/README.md) |
| `Framework.Res` | YooAsset 封装（`ResourceManager`） | [Res/README.md](Res/README.md) |
| `Framework.MemoryPool` | 轻量内存池（`IMemory`） | [MemoryPool/README.md](MemoryPool/README.md) |
| `Framework.ObjectPool` | 对象池（`ObjectBase` / 容量过期） | [ObjectPool/README.md](ObjectPool/README.md) |
| `Framework.Coroutine` | 协程（Global / Scene / GameObject） | [Coroutine/README.md](Coroutine/README.md) |
| `Framework.Editor` | Luban / YooAsset 编辑器工具 | [Editor/README.md](Editor/README.md) |
| `Generated.Luban` | Luban 生成配置代码 | [../Generated/README.md](../Generated/README.md) |

## 示例与验证（写在 README）

| 内容 | 位置 |
|------|------|
| 启动流程 + 配置加载 | `Assets/Scripts/Launch.cs`、[Config/README.md](Config/README.md) |
| 完整战斗演示 | [Bridge/README.md](Bridge/README.md) → 完整示例 |
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
    │       └── Config (ConfigModule) Data
    │               └── Bridge        Gameplay
    │                       ├── GAS
    │                       └── ECS
    │                               └── Core
    │
    └── Editor (Editor only)
```

## 启动流程

```
Launch.Awake
  → GameBootstrap.SetModules([ResourceModule, ConfigModule, ...])
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
// 通过 Bootstrap 初始化后，从各模块入口取用
var tables = BattleConfigBootstrap.Tables;
var manager = ResourceManager.Instance;

// YooAsset 直调
var tables = BattleConfigBootstrap.LoadTables(ResourceManager.Instance);

// Editor 直读（调试用）
var tables = BattleConfigBootstrap.LoadTables();
```

## 编辑器工具

| 菜单 | 功能 |
|------|------|
| Tools/Luban/Generate Client Config | 执行 Luban 打表 |
| Tools/YooAsset/Generate Collector | 生成 `BundleCollectorSetting.asset` |
