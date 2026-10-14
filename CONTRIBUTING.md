# 贡献指南

感谢你愿意参与。下面是提交 Issue 和 Pull Request 前需要知道的事。

## 环境

- Unity **2021.3.21f1**（版本见 `ProjectSettings/ProjectVersion.txt`）
- .NET 8 SDK（跑引擎无关层的单元测试）
- 克隆后先按 README「快速开始」生成 Luban 配置和 YooAsset Collector

> `Assets/ThirdParty` 下有商业插件，不在 MIT 授权范围内，详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。提交 PR 时不要新增任何商业插件文件。

## 目录约定

| 目录 | 内容 |
|---|---|
| `Packages/com.jokerseed.framework.*` | 框架本体（嵌入式 UPM 包），PR 主要改这里 |
| `Assets/Scripts` | 示例游戏与 Luban 样例接入 |
| `Assets/Tests` | Unity EditMode 测试 |
| `tests/` | 纯 .NET 测试（不需要 Unity） |
| `docs/` | 架构文档、ADR |

## 分层规则

- 战斗模拟层（FixedMath、Lockstep、Battle.Core、ECS、GAS、BehaviourTree、GamePlay 等）的 asmdef 开了 `noEngineReferences`，**不得引用 UnityEngine**。需要 Unity 的代码放进对应的 `*.Unity` 适配程序集。
- 模拟层只用定点数（`FP` / `TSVector`），禁止 `float` / `double`、`UnityEngine.Random`、`System.Random`、`DateTime.Now`、`Dictionary` 遍历顺序参与逻辑。编辑器菜单 **Tools/Framework/Battle/Scan Determinism** 可以扫一遍。
- `Framework.Core` 不依赖战斗相关程序集。`Assets/Tests/EditMode/ArchGuardrailTests.cs` 会检查这些约束。

## 代码规范

- 详细规则在 `.cursor/rules/*.mdc`，`.editorconfig` 约束格式。
- 公开 API 必须写中文 XML 文档注释，`<param>`、`<returns>`、`<exception>` 写全。
- 日志只走 `GameLog`；协程只走 `GameCoroutine`；资源只走 `Framework.Res`；配置只走 `ConfigManager`。
- 编辑器菜单统一挂在 `Tools/Framework/...` 下。
- 改了模块行为，同步更新该模块目录下的 `README.md`。

## 测试

提交前请至少跑通：

```bash
# 引擎无关层，几秒钟
cd tests/Framework.Sim.Tests
dotnet test -c Release
```

以及 Unity 里 **Window → General → Test Runner → EditMode → Run All**。命令行等价写法：

```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml
```

修 bug 请附带能复现的测试；改确定性相关代码请补充或更新 golden 录像测试。

## 提交信息

用简体中文，格式「动词: 描述」，一两句说清楚**为什么**改，例如：

```
修正: 事件分发中先退订再订阅会丢掉回调，改为按调用顺序回放
```

常用动词：添加、修改、修正、删除、重构、文档、测试。

## Pull Request

1. 从 `main` 拉分支，一个 PR 只做一件事。
2. 填写 PR 模板里的检查项。
3. CI（`.github/workflows/ci.yml` 的 .NET 测试与模拟层确定性检查）全部通过后再请求评审。

## 行为准则

参与本项目即表示同意遵守 [行为准则](CODE_OF_CONDUCT.md)。
