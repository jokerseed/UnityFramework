# Framework.HotUpdate

HybridCLR 热更程序集加载。程序集 `Framework.HotUpdate`，依赖 `Framework.Res`；HybridCLR 不是必需依赖：

- 核心程序集不引用 HybridCLR，也不依赖可选包的 versionDefines。未安装 HybridCLR 时可导入和编译基础框架。
- 需要 AOT 元数据补充时，先安装与你的 Unity/IL2CPP 匹配的 `com.code-philosophy.hybridclr`，再从 Package Manager 的 Framework Resource → Samples 导入 **HybridCLR Adapter**。
- 适配器在 `Samples~` 中，未导入时不参与 Unity 编译；导入后才创建引用 `HybridCLR.Runtime` 的独立程序集。卸载 HybridCLR 前也应删除已导入的适配器样例。
- 适配器在 BeforeSceneLoad 注册，核心在 SubsystemRegistration 清除旧注册，兼容关闭 Domain Reload。若你在更早的启动阶段调用加载，先显式调用 `HybridCLRMetadataBackend.Register()`。
- 自定义后端可调用 `HotUpdateLoader.RegisterAotMetadataLoader`。无注册后端时，非 Editor 下加载 AOT 元数据明确抛 `NotSupportedException`；无 AOT 元数据和 Editor 的现有路径不变。
- 迁移：以前仅安装 HybridCLR 的项目，现在还需导入适配器。公共 LoadAsync/InvokeEntry 签名不变。不使用反射寻找 HybridCLR API，避免 IL2CPP 裁剪和签名匹配问题。
- 验证边界：静态包装测试覆盖未安装依赖时的程序集引用；实际 Unity 导入、已安装 HybridCLR 的 IL2CPP 构建仍需在有效许可证环境验证。

## 流程

```
ResourceManager.InitializeAsync → ResourcePatcher 下载 → HotUpdateLoader.LoadAsync → InvokeEntry
```

```csharp
var assemblies = await HotUpdateLoader.LoadAsync(
    aotMetadataLocations: new[] { "mscorlib.dll", "System.dll", "System.Core.dll" },
    assemblies: new[] { new HotUpdateAssemblyInfo("HotUpdate", "HotUpdate.dll") },
    cancellationToken: token);
HotUpdateLoader.InvokeEntry(assemblies[0], "HotUpdate.Entry", "Start");
```

- DLL 以 `.bytes` 形式打进 YooAsset 资源包（HybridCLR 的 `CompileDll` / `GenerateAll` 产物复制后改名），寻址规则与普通资源一致
- 热更程序集按依赖顺序排列，被依赖的在前
- Editor 下热更代码本身就是编译好的程序集，`LoadAsync` 按程序集名返回它们，不读字节，便于直接调试
- 任一步失败直接抛异常，由调用方（通常是启动流程）决定重试或提示

## 不做的事

- 不负责 HybridCLR 的打包设置（Settings、`link.xml`、AOT 泛型补充列表），这些由 HybridCLR 编辑器工具生成
- 不负责热更 DLL 的版本管理：DLL 跟随资源包版本，由 `ResourcePatcher` 一起下载
