# tests

纯 .NET 测试，不需要 Unity，也不需要 Unity 许可证。

## Framework.Sim.Tests

直接编译 `Packages/` 下引擎无关层的源码，清单见 `Framework.Sim.Tests.csproj`。此工程把多个生产程序集的源码合并编译，适合内部回归测试，但不能单独证明程序集边界正确。下面的独立消费者检查补充这一点。

```bash
cd tests/Framework.Sim.Tests
dotnet test -c Release
```

| 测试 | 内容 |
|---|---|
| `SmokeTests` | 不依赖 Unity 建一场战斗并推进 |
| `GoldenReplayTests` | 固定种子与输入的逐帧校验和；与仓库里的 golden 值比对；录像对拍；模拟版本不符时拒绝对拍；多种子战斗逐帧复现；首帧 / 中间帧 / 末帧损坏定位 |
| `DeterminismScanTests` | 与 Unity 菜单 **Tools/Framework/Battle/Scan Determinism** 相同的规则（`DeterminismRules`）扫描模拟层源码 |
| `EventBusTests` | 事件分发中订阅 / 退订的顺序语义 |
| `FileLogSinkTests` | 滚动日志文件 |

### golden 值失败怎么办

`GoldenReplayTests.FinalChecksum_MatchesGolden` 失败说明同一输入下的逐帧结果变了：

- **有意改动**（改了数值公式、Tick 顺序、校验和覆盖范围等）：把 `BattleSimulationVersion.Current` 加一，测试输出里会给出新的校验和，更新 `GoldenFinalChecksum` 与 `GoldenSimulationVersion`。
- **不是有意改动**：说明引入了非确定性或回归，先查最近的模拟层改动。

### 编译符号

工程定义了 `FRAMEWORK_LOG_VERBOSE`，所以 `GameLog.Debug` / `GameLog.Trace` 在测试里照常输出（Unity 里由 `UNITY_EDITOR` / `DEVELOPMENT_BUILD` 控制）。

## 独立程序集与包消费者

```bash
python3 scripts/build_package_consumer.py /tmp/framework-consumer
# 输出目录须不存在；Windows 可改用其他新目录。
dotnet test /tmp/framework-consumer/Consumer/Consumer.csproj -c Release
```

生成器从最近的 `.asmdef` 确定每个 `.cs` 的归属，按原程序集名称分别构建 9 个 `noEngineReferences` 程序集，以及 Events 和 Logging。项目引用只采用各自声明的直接依赖，禁用 .NET 默认的传递项目引用，能发现单程序集测试隐藏的 `internal` 越界和缺失引用。遇到未支持的条件编译程序集或缺失依赖时明确失败，不静默合并。

这是引擎无关投影，不是完整 Unity 编译：Logging 仅排除 `LoggingManager.cs` / `LoggingModule.cs` 两个 Unity 入口及其 Core / UniTask 引用；其余所选程序集无源文件排除，不使用 Unity 替身。源文件复制到独立目录，不引用 `Assets/`。外部消费者通过公开 API 跑 golden 与多种子重放测试，并断言代码确实位于不同生产程序集。

完整包导入另由干净 Unity 工程验证：

```bash
python3 scripts/create_unity_consumer.py Temp/PackageConsumer
# 用 ProjectSettings/ProjectVersion.txt 中的 Unity 版本打开该目录，运行 EditMode 测试。
```

该工程只复制 7 个非 legacy 框架包、固定版本的必要依赖、编辑器版本及公开 API 测试。不复制演示场景、商业插件、生成表、项目设置和本地路径依赖。外部 Git 包首次解析需要网络；此检查并不覆盖 HybridCLR 可选模式、IL2CPP、WebGL 或商业插件组合。

## Unity PlayMode

`Assets/Tests/PlayMode/CoroutinePlayModeTests.cs` 覆盖跨帧完成、幂等停止与其他协程隔离、宿主销毁、场景卸载与全局作用域隔离、Shutdown 后重新启动。测试用例自行清理宿主和场景，不依赖 Launch 场景和资源包；这仍不是完整游戏验收。

## CI

`.github/workflows/ci.yml` 在推送到 `main` 和每个 PR 时于 Linux / Windows 跑模拟测试和独立程序集消费者；另跑 Luban 漂移与 Python 工具测试。Python 工具测试：`python3 -m unittest discover -s scripts/tests -v`。

`.github/workflows/editmode-tests.yml` 在每天 02:00（Asia/Shanghai）、指向 `main` 的 PR 和手动触发时跑 EditMode、PlayMode 和干净包消费者。fork PR 跳过这些需要 Secret 的作业；可信运行缺少 `UNITY_LICENSE` 明确失败，表示未验证，不能视为测试通过。没有更改许可证、Secrets 或分支保护。
