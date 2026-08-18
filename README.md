# Framework.Battle

Unity **2021.3 LTS** 战斗框架：**GAS 规则权威 + ECS 模拟 + Luban 配置 + YooAsset 资源 + 帧同步基座**。

面向需要「配置驱动战斗、可渐进接入锁步」的 Unity 项目。这是**模块化战斗基座**，不是 ET 式整包服务端框架。

> **当前能力边界：** 支持等输入锁步基座（FixedMath / Lockstep 类型迁移）与逻辑帧行为树；**预测回滚 / 状态快照尚未实现**；**反作弊未接线**（checksum 类型已迁，内存混淆 `Obscured*` 刻意未进 FixedMath）。详见 [帧同步与回滚](#帧同步与回滚)、[反作弊](#反作弊)。

[快速开始](#快速开始) · [架构](#架构) · [帧同步与回滚](#帧同步与回滚) · [反作弊](#反作弊) · [模块](#模块) · [行为树](#行为树-ai) · [AI 选型](#ai-选型说明) · [对比](#与常见方案对比) · [Roadmap](#roadmap) · [文档](#文档)

---

## 为什么用这个框架

| 特点 | 说明 |
|------|------|
| **规则与模拟分离** | GAS 管伤害/技能/Tag；ECS 管位置、碰撞、弹道 |
| **国内项目常用栈** | Luban 打表 + YooAsset 热更；Editor 菜单一键工具链 |
| **模块可组合** | `IGameModule` + `GameBootstrap` 拓扑启动，按 asmdef 按需引用 |
| **帧同步可演进** | FixedMath / Lockstep / LockstepPhysics / BehaviourTree 已铺基座，待接 Host |
| **文档即示例** | 各模块 README 承载演示与验证，不单独维护 Samples 程序集 |

---

## 帧同步与回滚

| 项 | 状态 |
|----|------|
| 定点数学（`Framework.FixedMath`） | ✅ 已迁移 |
| 锁步调度类型（`DefaultLockstep` / `RollbackLockstep` / `StateTracker`） | ✅ 已迁移（底层） |
| `LockstepHost` / 与 GamePlay 对接 | ❌ 未做 |
| 战斗演示（Battle） | ⚠️ 仍为 `Tick(deltaTime)` 可变步长，非锁步权威路径 |
| **预测回滚 / 状态快照** | ❌ **未实现**（GAS / ECS / BehaviourTree 均不可还原） |
| 本地录像对拍 Demo | ❌ 未做 |

说明：Client 原项目亦使用 `rollbackWindow: 0`（等输入锁步，不开回滚）。Framework 当前目标与之对齐；回滚快照在 Roadmap 中单独规划。

详情：[Lockstep/README.md](Assets/Framework/Lockstep/README.md)

## 反作弊

锁步战斗的主权威在「同输入 → 同结果」；反作弊分两层：**同步完整性**（检测不同步/篡改）与 **内存混淆**（防改内存）。

| 项 | 状态 | 说明 |
|----|------|------|
| 帧校验和类型（`ChecksumExtractor` / `SyncedInfo` / `checksumOk`） | ⚠️ 已迁移底层 | 未接 `LockstepHost` / GamePlay；尚无线上对拍 |
| 录像对拍（`ReplayRecord*`） | ⚠️ 类型已迁 | 无 Demo；与 `NullCommunicator` 一并规划 |
| 内存混淆（Client `Obscured*` ↔ `FP`） | ❌ **未接入** | 迁 FixedMath 时刻意剥离，保持数学库无反作弊依赖 |
| 可选 `Obscured` ↔ `FP` 适配层 | ❌ 未做 | 仅在进出定点边界转换；不进热路径 |
| 服务端权威校验 / 风控服务 | ❌ 未做 | 属业务侧；框架不内置 |

设计约定：

- **`Framework.FixedMath` 保持纯数学**：不引用 Anti-Cheat 程序集；数值路径与 Client 定点算法一致。
- 若业务需要内存混淆：在业务或薄适配层用 `Obscured*` 存盘，进出逻辑时 `GetDecrypted()` → `FP`（与 Client 原「边界转换」模式一致），**不要**把混淆塞进 `Sin`/`TSVector` 内部。
- 锁步场景优先靠 **checksum + 录像回放对拍** 发现作弊/不同步；内存混淆是补充，不是替代。

详情：[FixedMath/README.md](Assets/Framework/FixedMath/README.md)、[Lockstep/README.md](Assets/Framework/Lockstep/README.md)

## 快速开始

### 环境要求

- Unity **2021.3 LTS**（项目当前：`2021.3.21f1`）
- 克隆后用 Unity 打开仓库根目录 `Framework/`（含 `Assets/`、`Framework.sln`）

### 1. 生成 Luban 配置

```bat
Config\Luban\gen_client.bat
```

或在 Unity：**Tools → Luban → Generate Client Config**

产出：

| 类型 | 路径 |
|------|------|
| C#（Luban） | `Assets/Generated/Luban/` |
| C#（Input） | `Assets/Generated/Input/` |
| 二进制 | `Assets/Bundles/Configs/*.bytes` |
| JSON（调试） | `Config/Luban/Output/json/` |

### 2. 生成 YooAsset Collector

**Tools → YooAsset → Generate Collector**

### 3. 运行 Launch 场景

1. 打开 `Assets/Bundles/Scenes/Launch.unity`
2. Play
3. Console 应看到模块组 `launch` Ready，并弹出主界面

> Editor 下 `ResourceManager` 可使用 **EditorSimulateMode**，不必先打真实 Bundle。

### 4. （可选）战斗演示

1. 从主界面进入 Battle（或打开 `Assets/Bundles/Scenes/Battle.unity`）
2. 由 `BattleBootstrap` 驱动：`GamePlayFramework` + GAS 施放 Fireball + ECS 弹道 + 表现层跟随

---

## 架构

```
GameBootstrap (Host)
    │
    ├── Logging / Coroutine / MemoryPool / ObjectPool
    ├── ResourceManager (YooAsset 唯一运行时入口)
    │       ├── ConfigManager (Luban)
    │       └── GamePlayFramework
    │               ├── GAS（规则权威）
    │               └── ECS（模拟层）
    │
    └── FixedMath / Lockstep / LockstepPhysics / BehaviourTree（按需引用，非 IGameModule）
```

**战斗 Tick 顺序：**

```
GAS Tick → Flush Spawn → ECS Tick → Flush Damage → Sync Positions
```

更完整的模块依赖图见 [Assets/Framework/README.md](Assets/Framework/README.md)。

---

## 模块

| 程序集 | 职责 |
|--------|------|
| `Framework.Bootstrap` | 多组模块编排、`IGameModule` 拓扑启动 |
| `Framework.GAS` | ASC、技能、Effect、Tag、伤害管线 |
| `Framework.ECS` | World、System、空间哈希、投射物 |
| `Framework.GamePlay` | 玩法入口；Luban → GAS 装配 |
| `Framework.Config` | Luban 按需加载与 Tables 缓存 |
| `Framework.Res` | YooAsset 封装（加载/释放唯一入口） |
| `Framework.Events` | `IEventBus` / 零 GC 事件 |
| `Framework.Logging` | `GameLog` 统一日志 |
| `Framework.Coroutine` | 协程生命周期管理 |
| `Framework.MemoryPool` / `ObjectPool` | 内存池 / 对象池 |
| `Framework.UI` | `UIManager` / `UIWindow` |
| `Framework.FixedMath` | 确定性定点数（锁步基座 A） |
| `Framework.Lockstep` | 帧同步调度与输入抽象（基座 B/C） |
| `Framework.LockstepPhysics` | Jitter 3D + Farseer 2D（基座 D） |
| `Framework.BehaviourTree` | 帧同步行为树运行时 + 可视化编辑器 |
| `Framework.Editor` | Luban / YooAsset Editor 工具 |

---

## 行为树 AI

帧同步友好的轻量行为树：**逻辑帧手动 Tick**，禁止 Unity 时间/协程作为权威路径。

| 能力 | 入口 |
|------|------|
| 可视化编辑 | **Tools → Behaviour Tree → Editor** |
| 创建配置资产 | **Tools → Behaviour Tree → Create Tree Asset** |
| JSON 导出 | 编辑器工具栏 **Export JSON**（`*.bt.json`） |
| 运行时加载 | `BtTreeLoader.Load(asset, customRegistry)` |

自定义业务节点：实现 `IBtNodeRegistry`，在编辑器中使用 **Custom Action / Custom Condition**。

详见 [Assets/Framework/BehaviourTree/README.md](Assets/Framework/BehaviourTree/README.md)。

---

## AI 选型说明

本框架 **当前 AI 权威路径是行为树**（`Framework.BehaviourTree` + 可选第三方 Behavior Designer Manual Tick）。下列 Unity 生态「AI」能力职责不同，**默认不接入**，仅作选型记录，避免与生成式 AI / 寻路 / 训练混为一谈。

| 能力 | 干什么 | 与本框架关系 | 版本注意 |
|------|--------|--------------|----------|
| **BehaviourTree / BD** | 决策：打谁、放技能、切换状态 | ✅ **已用 / 主路径**；逻辑帧手动 Tick，锁步友好 | 2021.3 可用；BD 无定点，数值仍靠 FixedMath/GAS |
| **NavMesh** | 传统寻路：走到目标、绕障 | ❌ 未接；3D 关卡移动时再评 | 各版本均有；**不替代**行为树决策 |
| **ML-Agents** | 强化学习：在编辑器里**训练**智能体 | ❌ 未规划；成本高，商用不如 BT 常见 | 看具体包版本；与锁步确定性冲突大 |
| **Sentis** | 运行时**推理**神经网络（ONNX 等） | ❌ 未规划；有现成模型再评 | 2021.3 / 2022.3 / Unity 6 均可；接替 Barracuda |
| **Unity AI（原 Muse）** | 编辑器内生成式辅助（Chat 等） | ❌ 国内难用正规路径 | **仅 Unity 6.0+**；国内 Unity 6 已下架，对应看 **团结 CodeAI** |
| **Cursor + Unity MCP** | 工程外 AI 写代码 / 控编辑器 | ✅ 开发期可用，与运行时无关 | 不依赖 Unity 大版本 |

对照记忆：

- **NavMesh** = 怎么走过去  
- **行为树** = 要不要打、放哪个技能（本框架默认）  
- **ML-Agents** = 花大力气把决策「练」出来  
- **Sentis** = 把现成神经网络塞进包体里算  
- **Unity AI / 团结 CodeAI** = 帮人写资源与代码，不是战斗 AI  

---

## 与常见方案对比

| | **Framework.Battle** | ET | 纯 GAS 插件 | Behavior Designer |
|--|----------------------|-----|-------------|-------------------|
| **定位** | 战斗 + 锁步基座 | 全栈游戏框架 | 仅 GAS | 仅 AI 编辑器 |
| **配置** | Luban | Luban / 自研 | 各异 | SO / JSON |
| **资源** | YooAsset 统一封装 | 自研 / 多种 | 无 | 无 |
| **帧同步** | 等输入锁步基座已迁移；**回滚快照未实现** | 有 LS 体系 | 通常无 | 需 Manual Tick |
| **预测回滚** | ❌ 未实现 | 视项目而定 | 无 | 无 |
| **上手成本** | 中等（工具链完整） | 高 | 低（仅 GAS） | 低（仅 AI） |

---

## 目录结构

```
Framework/
├── Assets/
│   ├── Scripts/                    # 业务程序集 Game（Launch、BattleBootstrap）
│   ├── Framework/                  # 框架源码（各模块 README）
│   ├── Generated/                  # 工具生成代码（勿手改）
│   │   ├── Luban/                  # Luban 配置 C#
│   │   └── Input/                  # Input System 包装类
│   ├── Settings/Input/             # Input Actions 资产
│   └── Bundles/                    # 场景、配置、Prefab（YooAsset 收集）
├── Config/Luban/                   # 策划表 & 打表脚本
├── Packages/manifest.json          # UPM：Luban Runtime、YooAsset
└── README.md
```

---

## 依赖

| 包 | 来源 | 用途 |
|----|------|------|
| `com.code-philosophy.luban` | Git UPM | 运行时 `ByteBuf` |
| `com.tuyoogame.yooasset` | OpenUPM | 资源包管理 |

---

## Roadmap

- [x] GAS + ECS + Luban + YooAsset 主链路
- [x] Launch 模块启动 + Battle 演示（Fireball / 弹道）
- [x] FixedMath / Lockstep / LockstepPhysics 基座（自 Client TrueSync 迁移）
- [x] BehaviourTree 运行时 + 可视化编辑器 + JSON 导出
- [x] 行为树热更资源：`.bt.json` 导出 / YooAsset 加载 / 模板缓存 / `BattleAgent` 运行时替换
- [x] 单机玩法打磨：控制/死亡/Tag 计数、冷却 GE、伤害管线、AOE/弹道变体、移动、BT 接线
- [ ] `LockstepHost` + `ILockstepSimulation` 门面
- [ ] 本地 `NullCommunicator` + 录像对拍 Demo
- [ ] GamePlay 固定逻辑帧 + FP 战斗对接
- [ ] **预测回滚 / 状态快照**（GAS + ECS + BehaviourTree + Lockstep 接线）
- [ ] **反作弊：帧 checksum 接线**（接 Host，暴露 `checksumOk` / 对拍告警）
- [ ] **反作弊：可选 Obscured ↔ FP 适配**（边界转换；FixedMath 仍保持无依赖）
- [ ] 根目录 LICENSE、CONTRIBUTING、CI 编译
- [ ] （可选）3D 关卡需求出现时评估 **NavMesh** 与 BT/移动对接
- [ ] （可选，默认不做）**Sentis** / **ML-Agents** 选型评审（与锁步确定性冲突需单独设计）
- [ ] （可选）引擎升级路径记录：国际版 **2022.3 LTS** vs 国内 **团结**；Unity AI 仅 6.0+ / 国内看团结 CodeAI

---

## 文档

| 文档 | 说明 |
|------|------|
| [Assets/Framework/README.md](Assets/Framework/README.md) | 框架总览、依赖图、工具链 |
| [GAS](Assets/Framework/GAS/README.md) | 技能、效果、ASC |
| [ECS](Assets/Framework/ECS/README.md) | 模拟层 |
| [GamePlay](Assets/Framework/GamePlay/README.md) | 玩法入口与战斗示例 |
| [Lockstep](Assets/Framework/Lockstep/README.md) | 锁步基座与缺口说明 |
| [BehaviourTree](Assets/Framework/BehaviourTree/README.md) | AI 行为树 |
| [Generated](Assets/Generated/README.md) | Luban / Input 生成代码说明 |

---

## 贡献

欢迎 Issue 与 Pull Request。

- 请先阅读各模块 README 与 `.cursor/rules/` 中的项目约定（若你也在用 Cursor 协作）
- 新增 `IGameModule` 时须同步维护 `Dependencies`
- 运行时资源 / 日志 / 协程请走框架统一入口（`ResourceManager` / `GameLog` / `GameCoroutine`）

> **License 与 CONTRIBUTING 待补充**（计划 MIT，确认后将添加 `LICENSE` 文件）。

---

## 截图（待补充）

建议后续添加：

1. Launch 运行 — 模块 Ready + 主界面
2. Battle Demo — Hero 施放 Fireball
3. Behaviour Tree 编辑器节点图
