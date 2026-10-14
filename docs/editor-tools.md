# 编辑器工具

框架自研的编辑器工具都挂在 **Tools → Framework** 下，只在 Editor 平台编译，不进运行时构建。路径类配置集中在 **Project Settings → Framework**（`ProjectSettings/FrameworkSettings.asset`，随仓库提交）。

## 程序集

| 程序集 | 位置 | 内容 |
|--------|------|------|
| `Framework.Core.Editor` | `Packages/com.jokerseed.framework.core/Editor` | `FrameworkSettings` 与设置页 |
| `Framework.Config.Editor` | `Packages/com.jokerseed.framework.config/Editor` | Luban 打表、生成代码前缀校验 |
| `Framework.Res.Editor` | `Packages/com.jokerseed.framework.res/Editor` | YooAsset Collector 生成、`AddressByPreImportPath` 寻址规则 |
| `Framework.BehaviourTree.Editor` | `Packages/com.jokerseed.framework.battle/Editor/BehaviourTree` | 行为树编辑器 |
| `Framework.Battle.Editor` | `Packages/com.jokerseed.framework.battle/Editor/Determinism` | 确定性扫描 |

## 菜单一览

| 菜单 | 功能 |
|------|------|
| **Tools → Framework → Settings** | 打开 Project Settings → Framework |
| **Tools → Framework → Config → Generate Luban Config** | Luban 打表 + 前缀校验 + 调试 JSON |
| **Tools → Framework → Res → Generate YooAsset Collector** | 按设置重新生成 `BundleCollectorSetting.asset` |
| **Tools → Framework → Behaviour Tree → Editor / Create Tree Asset / Export All Runtime JSON** | 行为树编辑与导出 |
| **Tools → Framework → Battle → Scan Determinism** | 扫描模拟层的非确定性 API。批处理：`Unity -batchmode -executeMethod Framework.Editor.Determinism.SimDeterminismScanner.ScanBatch`，有违规时退出码 1 |

## Luban 打表

菜单直接调用 `dotnet Luban.dll`，不经过 cmd / bash，也不需要 Python，Windows 与 macOS 行为一致。`Luban.dll` 不存在时先用 `LubanProject` 构建（需要 .NET 8 SDK）。流程：

1. 生成 C# 代码（`OutputCodeDir`，默认 `Assets/Generated/Luban/Scripts`）与二进制（`OutputDataDir`，默认 `Assets/Bundles/Configs`）
2. 按 `Config/Luban/codegen.json` 校验生成文件名前缀（`LubanCodegenValidator`）；`enforceOnGenerate: true` 时不通过即中断
3. 导出调试 JSON（`OutputJsonDir`，留空则跳过）
4. `AssetDatabase.Refresh`

Excel 源文件 `Config/Luban/Datas/**.xlsx` 是唯一数据源，直接编辑后重新打表。命令行（CI / 不开 Unity）用 `Config/Luban/gen_client.bat` 或 `gen_client.sh`，它们不做前缀校验；EditMode 测试 `LubanCodegenPrefixTests` 会在 CI 中检查。

dotnet 不在 Unity 进程的 PATH 上时（macOS 从 Dock 启动 Unity 常见），在设置里把 `DotnetExecutable` 填成绝对路径，例如 `/usr/local/share/dotnet/dotnet`。

## YooAsset Collector

按 **Project Settings → Framework → Collector** 的规则清空并重新生成 Collector 配置：

- 每条规则：收集目录、打包粒度（每文件一包 / 整目录一包）、分组名
- `SubfoldersPerFolder` 为 true 时，目录下每个子目录再加一条按目录打包的规则（默认用于 `Assets/Bundles/Configs`）
- 不存在的目录会跳过并打警告
- `PackageName` 须与运行时 `ResourceInitOptions.PackageName` 一致
- 寻址规则 `AddressByPreImportPath`：`bundles/{目录}/{文件名}.unity3d`（例：`MeleeChaser.bt.json` → `bundles/behaviourtrees/meleechaser.unity3d`）

## 第三方编辑器

`Assets/ThirdParty/` 下的插件（Behavior Designer、Odin、Animancer、DOTween、Spine 等）保留各自的菜单，不改动。它们是商业或第三方授权，条款见 `THIRD_PARTY_NOTICES.md`。框架自己的行为树编辑器是 `Framework.BehaviourTree.Editor`，不要与 Behavior Designer 混用。

## 约定

- 新工具的菜单挂在 `Tools/Framework/{模块}/...`，Validate 菜单与执行菜单同一路径（`.cursor/rules/framework-editor-menu.mdc`）
- 工具里的路径不要写死，放进 `FrameworkSettings`
- 运行时程序集不能引用任何 `*.Editor` 程序集
