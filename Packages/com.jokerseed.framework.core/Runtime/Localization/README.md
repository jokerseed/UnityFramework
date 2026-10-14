# Framework.Localization

本地化：当前语言、回退语言、多个文本来源；切换语言时通知 UI 刷新。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Localization` |
| 命名空间 | `Framework.Localization` |
| 依赖 | `Framework.Core`、`Framework.Logging` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `LocalizationManager` | `Get` / `TryGet` / `Format` / `SetLanguage` / `LanguageChanged` |
| `ILocalizationSource` | 文本来源接口（Luban 表、JSON、远端下发……） |
| `DictionaryLocalizationSource` | 内存字典来源 |
| `LocalizationModule` | Bootstrap 模块：注册来源、设置初始语言 |
| `Framework.UI.LocalizedText` | uGUI `Text` 绑定键，切语言自动刷新（在 UI 包） |

## 用法

```csharp
// 把 Luban 文本表灌进字典来源（表结构由项目决定）
var source = new DictionaryLocalizationSource();
foreach (var row in tables.TbText.DataList)
{
    source.Set("zh-Hans", row.Key, row.ZhHans).Set("en", row.Key, row.En);
}

new LocalizationModule(language: null, fallbackLanguage: "en", source),   // null = 跟随系统语言

var loc = LocalizationManager.Instance;
title.text = loc.Get("ui.main.title");
hint.text = loc.Format("ui.level", level);
loc.SetLanguage("en");
```

## 规则

- 查找顺序：当前语言 → 回退语言；来源后注册的优先（便于热更补丁覆盖）
- 都找不到时返回键本身，同一语言同一键只警告一次
- `Format` 按当前语言的区域格式化数字/日期；未知区域用 InvariantCulture
- 语言代码沿用 BCP 47（`zh-Hans`、`zh-Hant`、`en`、`ja`……），`FromSystemLanguage` 负责从 `SystemLanguage` 映射
