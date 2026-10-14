# UnityFramework

[English](README.en.md) · [更新日志](CHANGELOG.md) · [贡献指南](CONTRIBUTING.md) · [MIT License](LICENSE)

Unity **2021.3 LTS** 客户端框架：模块化启动、资源与热更、配置表、UI、网络、音频，以及一套可选的**确定性战斗套件**（定点数、锁步回滚、GAS、ECS、行为树）。异步统一使用 **UniTask**，可以跑在移动端、PC 与 WebGL / 小游戏上。

框架代码按功能拆成 7 个嵌入式 UPM 包（`Packages/com.jokerseed.framework.*`），每个功能一个 asmdef，按需引用。`Assets/Scripts` 是演示业务层，不被框架引用。

[快速开始](#快速开始) · [包与模块](#包与模块) · [启动流程](#启动流程) · [战斗套件](#战斗套件) · [工具链](#工具链) · [测试与-ci](#测试与-ci) · [依赖](#依赖) · [许可](#许可)

---

## 快速开始

### 环境

- Unity **2021.3**（当前 `2021.3.21f1`）
- .NET 8 SDK（打表与 `dotnet test`；只跑游戏可以不装）

### 运行演示

1. 用 Unity 打开仓库根目录
2. 打开 `Assets/Bundles/Scenes/Launch.unity` 并 Play
3. Console 中看到模块组 `launch` Ready，弹出主界面；进入 Battle 可看到 GAS 施法 + ECS 弹道

Editor 下 `ResourceManager` 默认 `EditorSimulateMode`，不需要先打 Bundle。

### 在自己的项目里使用

把需要的包目录拷到你工程的 `Packages/` 下（或用 Git URL 引用），并在 `manifest.json` 中加入它们依赖的 UniTask / YooAsset / Luban：

```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
"com.tuyoogame.yooasset": "3.0.5",
"com.code-philosophy.luban": "https://github.com/focus-creative-games/luban_unity.git"
```

最小启动代码：

```csharp
using Cysharp.Threading.Tasks;
using Framework.Bootstrap;
using Framework.Config;
using Framework.Core;
using Framework.Coroutine;
using Framework.Logging;
using Framework.Res;
using Framework.UI;
using UnityEngine;

public sealed class Launch : MonoBehaviour
{
    async UniTaskVoid Start()
    {
        var bootstrap = GameBootstrap.Instance;
        var group = bootstrap.SetModules("launch", new IGameModule[]
        {
            new LoggingModule(),
            new CoroutineModule(),
            new ResourceModule(),
            new UIModule(),
            ConfigModule.Create(loader => new cfg.CfgTables(loader)),
        });
        await bootstrap.RunAsync("launch", this.GetCancellationTokenOnDestroy());
        if (group.State == ModuleGroupState.Ready)
        {
            UIManager.Instance.Show<MainUIWindow>();
        }
    }
}
```

`RunAsync` 不因模块失败而抛异常，结果看 `ModuleGroup.State`，或订阅它的 `Ready` / `Failed` 事件。完整示例见 `Assets/Scripts/Launch.cs`。

---

## 包与模块

| 包 | 模块 | 说明 |
|----|------|------|
| **core** | [Bootstrap](Packages/com.jokerseed.framework.core/Runtime/Bootstrap/README.md) | `IGameModule` 按依赖拓扑排序，分波并行异步初始化，失败回滚 |
| | [Core](Packages/com.jokerseed.framework.core/Runtime/Core/README.md) | 模块契约、`PersistentSingleton<T>` |
| | [Logging](Packages/com.jokerseed.framework.core/Runtime/Logging/README.md) | `GameLog` 分级 / 分类 / 多 Sink |
| | [Events](Packages/com.jokerseed.framework.core/Runtime/Events/README.md) | 零 GC 事件总线、`GameEvent` |
| | [Coroutine](Packages/com.jokerseed.framework.core/Runtime/Coroutine/README.md) | 协程生命周期（Global / Scene / GameObject） |
| | [MemoryPool](Packages/com.jokerseed.framework.core/Runtime/MemoryPool/README.md) / [ObjectPool](Packages/com.jokerseed.framework.core/Runtime/ObjectPool/README.md) | 内存池、对象池 |
| | [Fsm](Packages/com.jokerseed.framework.core/Runtime/Fsm/README.md) | 通用状态机与流程（Procedure） |
| | [Timer](Packages/com.jokerseed.framework.core/Runtime/Timer/README.md) | 延时 / 重复 / 下一帧定时器，可绑定 owner 自动取消 |
| | [Save](Packages/com.jokerseed.framework.core/Runtime/Save/README.md) | 存档与设置（原子写文件、可替换存储与序列化） |
| | [Localization](Packages/com.jokerseed.framework.core/Runtime/Localization/README.md) | 多语言、回退语言、格式化 |
| | [DebugConsole](Packages/com.jokerseed.framework.core/Runtime/DebugConsole/README.md) | 运行时日志查看与命令行 |
| **res** | [Res](Packages/com.jokerseed.framework.res/Runtime/README.md) | YooAsset 封装：分帧调度加载、场景、作用域、`ResourcePatcher` 补丁下载 |
| | [HotUpdate](Packages/com.jokerseed.framework.res/Runtime/HotUpdate/README.md) | HybridCLR 热更程序集加载（可选） |
| **config** | [Config](Packages/com.jokerseed.framework.config/Runtime/README.md) | Luban 表按需加载与缓存，WebGL 下异步预热 |
| **ui** | [UI](Packages/com.jokerseed.framework.ui/Runtime/README.md) | 五层窗口栈、释放策略、异步打开、返回键、输入屏蔽、安全区、本地化文本 |
| **audio** | [Audio](Packages/com.jokerseed.framework.audio/Runtime/README.md) | BGM / SFX 双通道 |
| **network** | [Network](Packages/com.jokerseed.framework.network/Runtime/README.md) | TCP / WebSocket（含 WebGL）频道、心跳、`NetworkRpcClient` |
| **battle** | 见 [战斗套件](#战斗套件) | 确定性战斗，可以完全不用 |

架构与依赖关系见 [docs/architecture.md](docs/architecture.md)。

---

## 启动流程

`GameBootstrap` 只负责调度：模块在 `Dependencies` 里声明依赖，按拓扑分波初始化；同一波的模块都声明了 `AllowConcurrentInitialization` 时并行初始化；任一模块失败或被取消时，已初始化的模块按逆序 `Shutdown`，模块组进入 `Failed`，`RunAsync` 不抛异常，结果通过 `ModuleGroup` 事件和状态查询。

典型的热更启动顺序：

```
ResourceManager.InitializeAsync      // 请求最新版本 + 加载 Manifest
  → ResourcePatcher.DownloadAsync    // 统计并下载需要更新的 Bundle（可提示大小、显示进度）
  → HotUpdateLoader.LoadAsync        // 可选：HybridCLR 补充元数据 + 加载热更 DLL
  → ConfigManager.PreloadTableAssetsAsync
  → ProcedureManager.StartProcedure<MainMenuProcedure>()
```

---

## 战斗套件

`com.jokerseed.framework.battle` 是一套独立的确定性战斗基座，面向需要「配置驱动战斗 + 可联机锁步」的项目：

| 部分 | 状态 |
|------|------|
| 定点数学 `FP` / `TSVector`（LUT 三角函数） | ✅ 跨平台逐位一致，CI 在 Linux / Windows 上对同一 golden 录像校验 |
| GAS（技能、效果、属性、Tag） | ✅ 规则权威，Luban 装配 |
| ECS（位置、碰撞、投射物） | ✅ 只存模拟数据 |
| 确定性物理（Farseer 2D，Jitter 3D 可选） | ✅ ID 计数器按 World 隔离，可多场并存 |
| 行为树（逻辑帧 Tick，可视化编辑器） | ✅ |
| `LockstepHost` 等输入锁步 + 检查点回滚 | ✅ 检查点含 GAS / ECS / 随机数 / 物理 / 行为树；输入带 `BattleSimulationVersion`，版本不一致立即停止 |
| 录像与对拍 | ✅ `BattleReplayRecorder` / `BattleReplayVerifier` |
| 真实联机传输 | ⚠️ 框架只定义 `ICommunicator`；测试使用内存环回，网关需项目自行接入（可基于 `Framework.Network`） |
| 内存反作弊 | ❌ 不内置；`FixedMath` 保持纯数学，需要时在边界转换 |

改变逐帧结果的改动必须把 `BattleSimulationVersion.Current` 加一并更新 golden 录像，否则 CI 失败。详见 [GamePlay](Packages/com.jokerseed.framework.battle/Runtime/GamePlay/README.md)、[Lockstep](Packages/com.jokerseed.framework.battle/Runtime/Lockstep/README.md)、[GAS](Packages/com.jokerseed.framework.battle/Runtime/GAS/README.md)、[ECS](Packages/com.jokerseed.framework.battle/Runtime/ECS/README.md)、[BehaviourTree](Packages/com.jokerseed.framework.battle/Runtime/BehaviourTree/README.md)、[FixedMath](Packages/com.jokerseed.framework.battle/Runtime/FixedMath/README.md)。

---

## 工具链

编辑器工具都在 **Tools → Framework** 下，路径配置在 **Project Settings → Framework**：

| 菜单 | 作用 |
|------|------|
| Config → Generate Luban Config | 调用 `dotnet Luban.dll` 打表并校验生成代码前缀（Windows / macOS 一致，不需要 Python） |
| Res → Generate YooAsset Collector | 按设置生成 Collector |
| Behaviour Tree → Editor | 行为树编辑器 |
| Battle → Scan Determinism | 扫描模拟层的非确定性 API |

命令行打表：`Config/Luban/gen_client.bat` / `gen_client.sh`。详见 [docs/editor-tools.md](docs/editor-tools.md)。

---

## 测试与 CI

| 套件 | 位置 | 运行 |
|------|------|------|
| 引擎无关层（FixedMath、Lockstep、ECS、GAS、FSM 等）+ golden 录像 | `tests/Framework.Sim.Tests` | `dotnet test tests/Framework.Sim.Tests` |
| Unity EditMode | `Assets/Tests/EditMode` | Test Runner，或 `Unity -batchmode -runTests -testPlatform EditMode` |

`.github/workflows/ci.yml` 在每次 push / PR 上于 Linux 与 Windows 运行 `dotnet test` 和确定性扫描；`editmode-tests.yml` 需要手动触发，并要先在仓库 Secret 中配置 Unity 许可证。

---

## 依赖

| 包 | 用途 | 许可 |
|----|------|------|
| [UniTask](https://github.com/Cysharp/UniTask) 2.5.11 | 异步 | MIT |
| [YooAsset](https://github.com/tuyoogame/YooAsset) 3.0.5 | 资源包管理（`LocalPackages/` 本地包） | Apache-2.0 |
| [luban_unity](https://github.com/focus-creative-games/luban_unity) | 配置表运行时 | MIT |
| [HybridCLR](https://github.com/focus-creative-games/hybridclr) | 热更（可选，未内置） | MIT |
| Newtonsoft.Json（Unity 官方包） | 行为树 JSON | MIT |

完整清单见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

> **注意：** `Assets/ThirdParty/` 下的 Animancer、Odin Inspector、DOTween Pro、Spine、Behavior Designer 等是商业 / Asset Store 插件，仅用于作者本地开发与演示，**不在 MIT 许可范围内**，也不能随本仓库再分发给你使用。框架包本身不依赖它们。

---

## 目录结构

```
├── Packages/com.jokerseed.framework.*/   # 框架包（Runtime / Editor / README）
├── Assets/Scripts/                       # 演示业务层（程序集 Game）
├── Assets/Tests/EditMode/                # Unity 测试
├── Assets/Generated/                     # 生成代码（Luban / Input），勿手改
├── Assets/Bundles/                       # 场景、配置、Prefab（YooAsset 收集）
├── Config/Luban/                         # 策划表（Excel 为唯一数据源）与打表脚本
├── tests/Framework.Sim.Tests/            # dotnet 测试
├── tools/Framework.LutTools/             # 定点 LUT 维护工具
└── docs/                                 # 架构与工具文档
```

---

## 许可

框架自研代码使用 [MIT License](LICENSE)。第三方组件保留各自许可，见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。贡献前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md) 与 [行为准则](CODE_OF_CONDUCT.md)；安全问题请按 [SECURITY.md](SECURITY.md) 私下报告。
