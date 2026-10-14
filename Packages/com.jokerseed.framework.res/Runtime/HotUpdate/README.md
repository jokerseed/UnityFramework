# Framework.HotUpdate

HybridCLR 热更程序集加载。程序集 `Framework.HotUpdate`，依赖 `Framework.Res`；HybridCLR 不是必需依赖：

- 安装 `com.code-philosophy.hybridclr` 后，asmdef 的 versionDefines 会定义 `FRAMEWORK_HYBRIDCLR`，启用 AOT 元数据补充
- 没装时可以照常编译；非 Editor 下传入 AOT 元数据会抛 `NotSupportedException`

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
