# Framework.Editor

编辑器专用工具，仅在 Editor 平台编译。提供 Luban 打表与 YooAsset Collector 生成菜单。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Editor` |
| 命名空间 | `Framework.Editor` |
| 平台 | Editor only |
| 依赖 | `YooAsset.Editor` |

## 菜单一览

| 菜单 | 类 | 功能 |
|------|-----|------|
| **Tools → Luban → Generate Client Config** | `LubanGenMenu` | 执行 `Config/Luban/gen_client.bat` 打表 |
| **Tools → YooAsset → Generate Collector** | `YooAssetCollectorGenerator` | 生成 `BundleCollectorSetting.asset` |

## Luban 打表

`LubanGenMenu` 调用项目根目录下的 `Config/Luban/gen_client.bat`，产出：

| 类型 | 路径 |
|------|------|
| C# 代码 | `Assets/Generated/Luban/` |
| 二进制 | `Assets/Bundles/Configs/*.bytes` |
| JSON 调试 | `Config/Luban/Output/json/` |

打表完成后 Unity 会自动刷新资源。

### 生成代码前缀

类型名（及同名 `.cs` 文件）须带统一前缀，配置见 `Config/Luban/codegen.json`（默认 `Cfg`）。  
打表脚本会调用 `validate_codegen_prefix.py` 校验；细则见 `.cursor/rules/framework-luban.mdc`。

## YooAsset Collector

`YooAssetCollectorGenerator` 根据 `Assets/Bundles/` 目录结构生成 Collector 配置：

- `Assets/Bundles/Configs/*.bytes` — 每个文件单独打包
- `Assets/Bundles/Prefabs/` — 每个 Prefab 单独打包
- `Assets/Bundles/Scenes/` — 每个场景单独打包
- 寻址规则由 `AddressByPreImportPath` 实现：`bundles/{目录}/{文件名}.unity3d`

## 目录结构

```
Editor/
├── LubanGenMenu.cs
└── YooAsset/
    ├── YooAssetCollectorGenerator.cs
    └── AddressByPreImportPath.cs
```

## 注意事项

- 此程序集**不会**进入运行时构建（`includePlatforms: Editor`）
- 运行时模块（Res、Config 等）不应引用 `Framework.Editor`
- 新增编辑器工具时，在此程序集中添加并注册 `[MenuItem]`
- **自研顶栏菜单统一挂在 `Tools/{模块}/...` 下**，禁止新建独立顶栏根菜单（见 `.cursor/rules/framework-editor-menu.mdc`）
