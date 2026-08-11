# Framework.Config

Luban 配置表加载与 GAS 工厂，将策划数据转换为运行时 `GameplayAbility` / `GameplayEffect`。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Config` |
| 命名空间 | `Framework.Config` |
| 依赖 | `Framework.Bootstrap`、`Framework.Core`、`Framework.GAS`、`Framework.Bridge`、`Framework.Res`、`Generated.Luban`、`Luban.Runtime` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `ConfigModule` | `IGameModule` 实现，依赖 `ResourceModule`，加载表并注册 `cfg.Tables` |
| `BattleConfigLoader` | 从 bytes 或 Editor 直读文件加载 Luban 表 |
| `BattleConfigBootstrap` | 对外便捷 API：加载表、注册技能、应用效果 |
| `AbilityFactory` | Luban `Ability` 定义 → `GameplayAbility` |
| `EffectFactory` | Luban `Effect` 定义 → `GameplayEffect` |

## 配置表

| 表 | Excel 路径 | 运行时寻址 |
|----|-----------|-----------|
| 技能 | `Config/Luban/Datas/battle/ability.xlsx` | `bundles/configs/tbability.unity3d` |
| 效果 | `Config/Luban/Datas/battle/effect.xlsx` | `bundles/configs/tbeffect.unity3d` |

表结构定义：`Config/Luban/Defines/battle.xml`

## 打表

| 方式 | 入口 |
|------|------|
| 命令行 | `Config\Luban\gen_client.bat` |
| Unity 菜单 | **Tools → Luban → Generate Client Config** |

产出：
- C# 代码 → `Assets/Generated/Luban/`
- 二进制 → `Assets/Bundles/Configs/*.bytes`

## 加载方式

```csharp
// 方式一：通过 Bootstrap（推荐，Launch 场景）
var tables = bootstrap.Context.GetService<cfg.Tables>();

// 方式二：YooAsset 直调
var tables = BattleConfigBootstrap.LoadTables(ResourceManager.Instance);

// 方式三：Editor 直读文件（未打 Bundle 时调试）
var tables = BattleConfigBootstrap.LoadTables();
```

## 注册技能到战斗框架

```csharp
BattleConfigBootstrap.RegisterActorAbilities(
    framework,
    actorId,
    teamId: 1,
    abilityIds: new[] { "Fireball", "Slash" },
    tables);
```

## Bootstrap 集成

`ConfigModule` 声明依赖 `ResourceModule`，在 `ModulePhase.Data` 阶段初始化：

```
ResourceModule → ConfigModule（加载 cfg.Tables）
```

## 被谁使用

- `Assets/Scripts/Launch.cs` — 通过 `ConfigModule` 加载配置
