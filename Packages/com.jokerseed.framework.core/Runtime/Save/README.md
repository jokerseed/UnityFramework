# Framework.Save

存档与设置：按键保存/读取对象，存储后端与序列化器可替换。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Save` |
| 命名空间 | `Framework.Save` |
| 依赖 | `Framework.Core`、`Framework.Logging` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `SaveManager` | 入口：`Save` / `TryLoad` / `LoadOrDefault` / `Delete` / `ListKeys` |
| `ISaveStorage` | 存储后端；默认 `FileSaveStorage`（`persistentDataPath/Saves/{key}.sav`） |
| `ISaveSerializer` | 序列化；默认 `JsonUtilitySaveSerializer` |
| `SettingsStore<T>` | 一份设置对象：`Current` / `Update(mutate)` / `Changed` |
| `SaveModule` | Bootstrap 模块 |

## 用法

```csharp
new SaveModule(),                                     // 默认文件 + JSON
new SaveModule(new MyCloudStorage(), new MyNewtonsoftSerializer()),

var saves = SaveManager.Instance;
saves.Save("profile", profile);
if (saves.TryLoad("profile", out Profile loaded)) { }

var settings = new SettingsStore<GameSettings>(saves, "settings");
settings.Changed += s => AudioManager.Instance.SetBgmVolume(s.BgmVolume);
settings.Update(s => s.BgmVolume = 0.5f);           // 改完立即保存并通知
```

## 规则

- `FileSaveStorage` 先写 `.sav.tmp` 再替换，写到一半崩溃不会损坏旧存档
- 键只允许字母、数字、`_`、`-`、`.`，防止拼出目录外路径
- 存档内容损坏时 `TryLoad` 抛 `FormatException`，不会静默返回默认值覆盖玩家数据
- `JsonUtility` 要求类型标 `[Serializable]`，不支持字典与多态；需要时实现 `ISaveSerializer`
- WebGL / 小游戏平台的本地文件持久化各有限制，建议实现平台存储的 `ISaveStorage`
