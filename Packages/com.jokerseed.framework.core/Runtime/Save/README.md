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
| `ISaveBackupStorage` / `ISaveRecoveryStorage` | 可选备份读取 / 已验证备份提升能力 |
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

- `FileSaveStorage` 先写 `.sav.tmp` 并 `Flush(true)`，再替换主档；普通覆盖写入保留旧主档为 `.sav.bak`
- 主档缺失、无法读取或无法解析时，尝试解析备份。文件后端将已验证的原始字节提升为主档，保留 `.bak` 不变，再返回成功；后续保存或重新创建存储实例后，不会把已知坏主档轮换到好备份
- 恢复使用同目录临时文件与 `File.Replace(temp, primary, null)`；主档缺失时用 `File.Move`。不做先删除再复制的降级。写权限不足、提升失败或平台不支持替换时传播异常，保留备份，且不发出 `Saved` 事件；读取恢复可能需要写权限
- 写入和恢复按单写入者使用，同一目录/键不要跨线程、实例或进程并发操作。此实现不承诺所有文件系统断电后的目录元数据持久性
- 键只允许字母、数字、`_`、`-`、`.`，防止拼出目录外路径
- 只有主档和备份都不存在时 `TryLoad` 才返回 `false`，`LoadOrDefault` 才调用默认工厂。只剩损坏备份或后端读取抛出 I/O 异常时仍抛 `FormatException`；访问权限等异常直接传播，不会静默创建默认值覆盖玩家数据
- `ISaveStorage` / `ISaveBackupStorage` 原有实现仍可使用。会轮换备份的自定义后端应额外实现 `ISaveRecoveryStorage.RestoreBackup`，安全提升已验证字节并确保备份不被覆盖；仅实现备份读取的后端仍只读取备份，不自动调用普通 `Write` 修复
- `JsonUtility` 要求类型标 `[Serializable]`，不支持字典与多态；需要时实现 `ISaveSerializer`
- WebGL / 小游戏平台的本地文件持久化各有限制，建议实现平台存储的 `ISaveStorage`
