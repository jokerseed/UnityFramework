# 更新日志

格式参照 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。0.x 期间小版本之间也可能有不兼容改动，均在「不兼容改动」中列出。

## [0.1.0] - 未发布

第一个面向开源的版本。框架代码从 `Assets/` 拆成 7 个嵌入式 UPM 包，异步链路统一为 UniTask，补齐客户端常用模块与工具链。

### 不兼容改动

- **Unity 版本**：工程升到 2021.3.45f2。2021.3.45f2 之前的版本受 Unity 运行时漏洞 CVE-2025-59489 影响，用旧版本打出的包需要用新版本重新构建并发布。
- **战斗模拟版本升到 2**：配置数值改为按万分之一量化、实体 ID 带代数、投射物扫掠命中、死亡单位不再结算伤害、周期效果刷新默认不重置计时。版本 1 的录像不能再对拍，也不能和旧客户端同场。
- **内置技能**：`ProjectileAbility`、`MeleeStrikeAbility`、`CircleAoeAbility`、`ConeAoeAbility`、`ChanneledProjectileAbility`、`MeleeSweepAbility`、`DashAbility` 的构造函数改收 `FP`；配置值请经 `FixedConfig.FromConfig` 转换。`TSRandom.Next(float, float)` 改为 `Next(FP, FP)`，TSRandom 的静态 API（`instance` / `Init` / `value` / `Range` / `CallNext` / `insideUnitSphere`）已删除，改用实例方法。
- **ECS 实体 ID**：低 20 位是下标、高 12 位是代数，复用下标时 ID 不再与旧实体相同；往被别的实体占用的下标写组件会抛异常。
- **运行时缺效果**：`ApplyEffect` 等命令找不到效果时只记一次错误并跳过，不再抛异常；装配时由 `BattleConfigValidator` 报出缺失的效果引用。
- **LockstepHost**：只接受窗口内的输入（默认回滚 120 步、提前 120 步），窗口外的远端输入丢弃并触发 `InputRejected`，本端越界 `SubmitLocal` 抛异常。
- **包拆分**：`Framework.Lockstep.Legacy` 与 `Framework.LockstepPhysics.Jitter` 移到可选包 `com.jokerseed.framework.battle.legacy`，程序集名与 GUID 不变。
- **INetworkChannel** 增加 `MaxMissHeartBeatCount`；`NetworkErrorCode` 增加 `HeartBeatTimeout`。自定义频道实现需要补上该属性。
- **Package Manager**：manifest 删除未使用的 2d.animation、adaptiveperformance、animation.rigging、collab-proxy、formats.fbx、memoryprofiler、mobile.notifications、visualscripting。
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

- Battle：`FixedConfig.FromConfig`，float / double / decimal 写的同一个十进制配置值得到同一个定点值；`ILockstepCheckpointPruning`，主机随窗口丢弃旧检查点；`BattlePhysicsWorld.RetainRemovedBodies`，检查点之后销毁的实体回滚后仍有刚体；会施放技能的 MinimalBattle golden。
- Network：TCP / KCP 异步解析域名并逐个尝试地址；TCP `ConnectTimeoutSeconds`；KCP `ResponseTimeoutSeconds` 与死链检测；RPC 在频道出错时让等待中的调用全部失败。
- Core：`ISaveBackupStorage`，存档写入保留 `.bak`，读取失败时回退；`FileLogSink.Flush`；`LogStyle.StripRichText`；`RuntimeConsole.Toggle` 与三指轻点开关；对象池 `OnSpawn` / `OnUnspawn`；`FrameworkLinkXmlProcessor`，构建时合并各框架包的 `link.xml`（Unity 不读取包里的 link.xml）。
- UI：`LocalizedTmpText`（装了 TextMeshPro 才编译）；`UIWindow.IsOccluded`；子 Widget 跟随父界面更新与销毁。
- Res：Collector 过滤规则 `CollectTopDirectoryOnly`，父目录与子目录收集器不再重复收集。
- 测试与 CI：dotnet 测试工程加入网络、内存池与存档代码，新增 TCP / KCP 环回、心跳、超时、存档备份、投射物扫掠、锁步窗口与物理回滚等测试；Unity 测试工作流在 PR 上运行（无许可证时跳过）并缓存 Library；CI 新增打表一致性检查。
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

- 日志：多线程写日志时 sink 列表和分类过滤表不再竞争；文件日志写纯文本、缓冲写入，滚动失败时继续追加，单个 sink 抛异常不影响其它 sink。
- 启动：模块初始化完成后才计入进度。协程句柄在宿主停用、销毁或 `HaltAll` 后正确变为已停止，协程内异常会记录日志。
- 资源 / 配置：一个回调抛异常不再影响同一加载的其它等待者；合并加载的键按序号比较；CDN 地址去掉多余的 `/`；`ReloadTables` 失败时保留旧表；`LoadTables` 默认在建表后释放 TextAsset。
- UI：只销毁自己创建的 EventSystem，场景里的 EventSystem 被销毁后会重建；全屏遮挡只关 Canvas 与 Raycaster，不再反复 SetActive；被外部停止的 `ShowAsync` 会释放资源并回调；延迟卸载不受 `timeScale` 影响。
- 音频：循环音效不再被一次性音效抢走音源；Clip 缓存有容量上限并按最近最少使用淘汰；BGM 交叉淡入淡出被打断时不再跳变音量。
- 网络：Windows 上 UDP 收到 ICMP 端口不可达不再断开 KCP；TCP / KCP 收发改用池化缓冲，减少 GC。
- 战斗：已死亡单位不再结算伤害，周期效果中途致死后本帧停止；施法者被移除后范围伤害照常结算；高速投射物不再穿透目标；回滚到实体销毁之前时刚体能恢复，停用过的刚体回滚后宽相不再错乱；校验和缓冲按线程隔离；本地录像有长度上限。
- Farseer：`SimpleWindForce` 改用注入的随机源；多边形质心改用定点除法；`Settings` 中影响求解的参数改为只读。
- 编辑器：地址规则只去掉开头的 `Assets/` 并转小写；打表子进程可取消并有 10 分钟超时；行为树编辑器只在资产变化时重新校验，播放模式下限制重绘频率。
- 事件分发过程中先退订再订阅同一回调，最终会被移除；现在按调用顺序回放增删操作。
- Farseer 的 Body / Fixture ID 计数器改为每个世界一份；原来的静态计数让同进程多局或回放对拍的 ID 依赖先后顺序，快照恢复也会改写其它世界的计数。
- 修正 4 个在 EditMode 下必然失败的用例。

### 改进

- 确定性扫描纳入 Farseer 求解器，并拦截 `MathF`、float / 小数字面量、`Environment.TickCount`、`Stopwatch` 以及 GAS / GamePlay 的 float 形参。
- dotnet 测试工程去掉工程级 NoWarn，第三方目录按路径关掉警告，其余代码警告即错误。
- battle 包的 `license` 改为 SPDX 表达式 `MIT AND MS-PL AND Apache-2.0 AND Zlib`。
- `LocalLockstepHost` 默认不再每步计算校验和（录像或设置 `ChecksumInterval` 时才算）；属性重算复用字典。
- Sin / Tan 查找表改为二阶差分变长编码，源码从约 6 MB 降到 0.6 MB；解码结果经 SHA-256 校验，与原表逐位一致。
- 行为树序列化改用 Newtonsoft，与原 JsonUtility 格式兼容，客户端和服务端解析结果一致。
- `GameLog.Debug` / `Trace` 在 Release 包中整条编译剔除。
- Res 句柄内部改存 `object`，业务程序集 await 句柄时不需要引用 YooAsset。
