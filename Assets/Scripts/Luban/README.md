# Framework.GamePlay.Luban

把 Luban 表装进玩法框架。类型仍在 `Framework.GamePlay.Data`，所以原来的 `using` 不用改。`Framework.GamePlay` 不引用本程序集，也不引用 `Generated.Luban`。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.GamePlay.Luban` |
| 命名空间 | `Framework.GamePlay.Data` |
| 依赖 | `Framework.Core`、`Framework.Events`、`Framework.Battle.Core`、`Framework.Config`、`Framework.FixedMath`、`Framework.GamePlay`、`Framework.GAS`、`Generated.Luban`、`Luban.Runtime` |

`Luban.Runtime` 必须直接写在 references 里。`CfgEffectDef` 的基类在那个程序集，Unity 的 asmdef 不传递引用，少了它比较 `def == null` 会 CS0012。`Framework.Events` 留给需要把 `IEventBus` 传进本程序集的调用，不要删。

## 类型

| 类型 | 职责 |
|------|------|
| `LubanAbilityCatalog` / `AbilityConfigFactory` | 技能表到 `GameplayAbilityDef` |
| `LubanEffectCatalog` / `EffectConfigFactory` | 效果表到 `GameplayEffectDef`，嵌套效果会检测环 |
| `BattleConfigApplier` | 按效果 ID 把目录应用到 ASC |
| `GamePlayConfigSetup` | `RegisterActorAbilities` 扩展方法，把表装到 `GamePlayFramework` |

本程序集不推进 Tick，也不持有战斗会话。加载表走 `ConfigManager`。
