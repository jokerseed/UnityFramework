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

## 存在、列举与删除契约

- `Exists` / `ListKeys` 只发现存档副本，不验证内容，不恢复文件，也不调用序列化器。主档或备份任一存在就属于已有存档；仅有损坏备份仍可发现，`TryLoad` / `LoadOrDefault` 继续抛出解析异常。
- `TryRead` 仍只读取主档；只剩备份时它返回 `false`，不与“逻辑键存在”冲突。对象读取应使用 `SaveManager.TryLoad`。
- `ListKeys` 合并 `.sav` 与 `.sav.bak`，剥除完整后缀，按键字符串 Ordinal 去重并排序；`.sav.tmp` 和更长伪后缀不计入存档。后缀匹配沿用平台通配规则，大小写查找遵循文件系统，调用同一键时应保持大小写一致，不承诺跨平台大小写别名合并。
- `Delete` 删除主档和备份，不检查内容是否有效；任一副本原先存在且删除正常完成就返回 `true`。两者均不存在返回 `false`，临时文件不计入，也不在此清理。
- `SaveManager.Deleted` 仅在后端成功删除后触发一次；删除仅备份的键同样触发，重复删除不存在的键不触发。检查或删除异常直接传播，不通知成功。两份文件的删除不是事务，运行途中失败可能只删掉其中一份，修复阻塞后可重试。
- 文件查询/删除前只把明确的缺失异常视为不存在；权限错误或同名目录会抛异常，避免把操作失败伪装成“没有存档”。自定义备份后端应在这三个方法中采用同样的逻辑键契约；接口签名未变。

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
