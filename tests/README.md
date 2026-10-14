# tests

纯 .NET 测试，不需要 Unity，也不需要 Unity 许可证。

## Framework.Sim.Tests

直接编译 `Packages/` 下引擎无关程序集的源码（与 Unity 里编译的是同一份文件），清单见 `Framework.Sim.Tests.csproj`。

```bash
cd tests/Framework.Sim.Tests
dotnet test -c Release
```

| 测试 | 内容 |
|---|---|
| `SmokeTests` | 不依赖 Unity 建一场战斗并推进 |
| `GoldenReplayTests` | 固定种子与输入的逐帧校验和；与仓库里的 golden 值比对；录像对拍；模拟版本不符时拒绝对拍 |
| `DeterminismScanTests` | 与 Unity 菜单 **Tools/Framework/Battle/Scan Determinism** 相同的规则（`DeterminismRules`）扫描模拟层源码 |
| `EventBusTests` | 事件分发中订阅 / 退订的顺序语义 |
| `FileLogSinkTests` | 滚动日志文件 |

### golden 值失败怎么办

`GoldenReplayTests.FinalChecksum_MatchesGolden` 失败说明同一输入下的逐帧结果变了：

- **有意改动**（改了数值公式、Tick 顺序、校验和覆盖范围等）：把 `BattleSimulationVersion.Current` 加一，测试输出里会给出新的校验和，更新 `GoldenFinalChecksum` 与 `GoldenSimulationVersion`。
- **不是有意改动**：说明引入了非确定性或回归，先查最近的模拟层改动。

### 编译符号

工程定义了 `FRAMEWORK_LOG_VERBOSE`，所以 `GameLog.Debug` / `GameLog.Trace` 在测试里照常输出（Unity 里由 `UNITY_EDITOR` / `DEVELOPMENT_BUILD` 控制）。

## CI

`.github/workflows/ci.yml` 在每次 push 和 PR 上分别在 Linux 与 Windows 运行本工程。Unity EditMode 测试需要许可证，见 `.github/workflows/editmode-tests.yml`（手动触发）。
