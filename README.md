# Framework

Unity 2021.3 战斗框架：GAS（规则层）+ ECS（模拟层）+ Luban 配置 + YooAsset 资源。

## 目录结构

```
Framework/
├── Assets/
│   ├── Scripts/Launch.cs           # 启动入口（配置加载测试）
│   ├── Framework/                  # 战斗框架源码
│   ├── Generated/Luban/            # Luban 生成 C#（勿手改）
│   ├── Bundles/Configs/            # Luban 二进制（YooAsset 收集）
│   ├── BundleCollectorSetting.asset
│   └── Resources/YooAssetSettings.asset
├── Config/Luban/                   # 配置源数据 & 打表脚本
├── ThirdParty/luban-4.11.0/        # Luban CLI 源码
└── Packages/manifest.json          # UPM：Luban Runtime、YooAsset
```

## 快速开始

### 1. 打表

```bat
Config\Luban\gen_client.bat
```

或在 Unity：**Tools → Luban → Generate Client Config**

产出：
- C#：`Assets/Generated/Luban/`
- 二进制：`Assets/Bundles/Configs/*.bytes`
- JSON（调试）：`Config/Luban/Output/json/`

### 2. YooAsset Collector

**Tools → YooAsset → Generate Collector**

### 3. 构建资源包

**YooAsset → Bundle Builder** → 选 `DefaultPackage` 构建

> Editor 下使用 `EditorSimulateMode` 可跳过真实打 Bundle，由 YooAsset 自动模拟。

### 4. 运行测试

打开 `Assets/Bundles/Scenes/Launch.unity`，Play。`Launch.cs` 会初始化 YooAsset 并加载配置表，Console 输出技能/效果数据。

## 依赖

| 包 | 来源 | 用途 |
|----|------|------|
| `com.code-philosophy.luban` | Git UPM | 运行时 `ByteBuf` |
| `com.tuyoogame.yooasset` 3.0.5 | OpenUPM | 资源包管理 |

## 文档

- 战斗框架详情：[Assets/Framework/README.md](Assets/Framework/README.md)
- Luban 生成代码：[Assets/Generated/README.md](Assets/Generated/README.md)
