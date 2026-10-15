# 更新日志

格式参照 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。0.x 期间小版本之间也可能有不兼容改动，均在「不兼容改动」中列出。

## [0.1.0] - 未发布

第一个面向开源的版本。框架代码从 `Assets/` 拆成 7 个嵌入式 UPM 包，异步链路统一为 UniTask，补齐客户端常用模块与工具链。

### 不兼容改动

- **目录与包**：框架代码移到 `Packages/com.jokerseed.framework.{core,res,config,ui,audio,network,battle}`，旧的 `Assets/Framework` 不再存在；GUID 保留，场景和 Prefab 引用不受影响。
- **异步**：`IGameModule.InitializeAsync` 改为返回 `UniTask` 并接收 `CancellationToken`；资源加载、场景切换、UI 打开同样改为 UniTask。`GameBootstrap.RunAsync` 不因模块失败而抛异常，结果看 `ModuleGroup.State`。
- **命名空间**：`Framework.Battle.Core` 程序集的命名空间从 `Framework.Core` 改为 `Framework.Battle`（含 `.Commands` / `.Snapshot`）。
- **Config**：`ConfigModule` 不再编译期依赖 `cfg.CfgTables`，改用 `ConfigModule.Create(loader => new cfg.CfgTables(loader))` 注册。
- **Battle 模拟层**：Lockstep、物理、ECS、GAS、行为树、GamePlay 模拟层不再引用 UnityEngine；`CreateActor` 等 API 只收 `FP` / `TSVector`，`Vector3` / `float` 便捷重载移到 `Framework.GamePlay.Unity`。物理接口去掉 `GameObject`，回滚后恢复刚体改用 `OnBodyRestoredEnabled` 回调。旧 TrueSync 调度器移到可选程序集 `Framework.Lockstep.Legacy`。
- **快照**：GAS 快照升到 v4（去掉进程级计数器），旧快照不能直接恢复。
- **Lockstep**：`LockstepHost` 构造函数末尾增加 `simulationVersion` 参数，`LockstepInputMessage` 增加 `SimulationVersion` 字段；版本不一致时对局立即停止，`TryAdvance` 抛异常。
- **Network**：`INetworkChannel` 去掉 `Socket` 属性，只保留在 `TcpNetworkChannel` 上。
- **资源寻址**：示例工程的场景 / Prefab 地址从框架 `ResourceAddresses` 移到 `Game.GameAddresses`。
- **编辑器菜单**：全部移到 `Tools/Framework/...`；Luban 路径和 Collector 规则改在 **Project Settings → Framework** 中配置。
- **打表**：去掉 Python 依赖，删除 `create_excel.py` 与 `validate_codegen_prefix.py`；Excel 是唯一数据源，生成代码前缀改由编辑器内校验。

### 新增

- Core：有限状态机与流程（`ProcedureManager`）、表现层定时器、存档与设置（原子写入、可替换存储与序列化）、本地化（多来源、回退语言、`LocalizedText`）、运行时调试控制台、滚动文件日志与未处理异常捕获。
- Res：`ResourcePatcher`，用于 HostPlayMode 下统计并下载补丁，带进度与取消；可选的 `HotUpdateLoader`，用于 HybridCLR 补充元数据并加载热更程序集。
- Config：`PreloadTableAssetsAsync`，供不允许同步加载的平台（WebGL / 小游戏）预热；`ReloadTables`，改表后热重载。
- UI：返回键分发（`CloseOnBack` / `OnBack` / `BackUnhandled`）、引用计数的输入屏蔽 `BlockInput()`、`UISafeArea`；启用 Input System 时使用 `InputSystemUIInputModule` 并复用场景已有的 EventSystem。
- Network：WebSocket 频道（桌面 / 移动端用 `ClientWebSocket`，WebGL 用 jslib，可替换传输层）、带超时与取消的 `NetworkRpcClient`。
- Network：KCP 频道（UDP，会话号由调用方指定；WebGL / 小游戏创建即失败）。HTTP 短请求 `HttpRequests` 不进入频道。
- Battle：录像记录模拟版本，提供 golden 录像；`BattleSimulationVersion.Current` 写入录像与锁步输入。
- 测试与 CI：`tests/Framework.Sim.Tests` 用纯 .NET 编译模拟层并运行测试（含 golden 录像、确定性扫描和文档相对链接），GitHub Actions 在 Linux / Windows 上跑；Unity EditMode / PlayMode 工作流每天定时跑，未配置许可证 Secret 时跳过。
- Network：KCP 源码随 `com.jokerseed.framework.network` 分发（`Runtime/ThirdParty/KCP`），拷贝该包即可编译 KCP 频道。
- 演示层：Luban 适配程序集改名为 `Game.Luban`，类型命名空间仍是 `Framework.GamePlay.Data`。
- 裁剪：各相关包带 `link.xml`，保留内存池、热更加载和行为树 JSON 反序列化要用的程序集。
- 打表：`Config/Luban/gen_client.sh`，macOS / Linux 可直接命令行打表。
- 工程规范：`.gitattributes`、`.editorconfig`；MIT 许可、第三方声明、贡献指南、行为准则、安全策略、Issue / PR 模板；README 中英双语。

### 修正

- 事件分发过程中先退订再订阅同一回调，最终会被移除；现在按调用顺序回放增删操作。
- Farseer 的 Body / Fixture ID 计数器改为每个世界一份；原来的静态计数让同进程多局或回放对拍的 ID 依赖先后顺序，快照恢复也会改写其它世界的计数。
- 修正 4 个在 EditMode 下必然失败的用例。

### 改进

- Sin / Tan 查找表改为二阶差分变长编码，源码从约 6 MB 降到 0.6 MB；解码结果经 SHA-256 校验，与原表逐位一致。
- 行为树序列化改用 Newtonsoft，与原 JsonUtility 格式兼容，客户端和服务端解析结果一致。
- `GameLog.Debug` / `Trace` 在 Release 包中整条编译剔除。
- Res 句柄内部改存 `object`，业务程序集 await 句柄时不需要引用 YooAsset。
