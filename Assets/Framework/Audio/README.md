# Framework.Audio

参考 JSAM / TEngine / GameFramework 的音频频道模型：BGM / SFX 双通道、对象池、音量控制、交叉淡入淡出。Clip 加载统一走 `ResourceManager`。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Audio` |
| 命名空间 | `Framework.Audio` |
| 依赖 | `Framework.Core`、`Framework.Logging`、`Framework.Res`、`Framework.Coroutine` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `AudioManager` | BGM/SFX 播放、音量、Clip 缓存、淡入淡出 |
| `AudioModule` | Bootstrap 模块入口 |
| `AudioVolumeSettings` | Master / BGM / SFX 音量与 PlayerPrefs 持久化 |
| `AudioAddresses` | 音频资源 YooAsset 寻址规则 |
| `AudioChannelType` | 通道类型枚举（BGM / SFX） |

## 典型用法

```csharp
using Framework.Audio;
using Framework.Res;

// Bootstrap 注册 AudioModule 后
AudioManager.Instance.PlayBgm(AudioAddresses.Clip("bgm_main"));
AudioManager.Instance.PlaySfx(AudioAddresses.Clip("sfx_click"));

// 3D 音效
AudioManager.Instance.PlaySfxAt(
    AudioAddresses.Clip("sfx_explosion"),
    transform.position,
    volumeScale: 0.8f);

// 异步 BGM（切场景前预加载）
AudioManager.Instance.PlayBgmAsync(AudioAddresses.Clip("bgm_battle"));

// 循环环境音
var loopId = AudioManager.Instance.PlaySfxLoop(AudioAddresses.Clip("sfx_ambience"));
AudioManager.Instance.StopSfxLoop(loopId);

// 音量
AudioManager.Instance.MasterVolume = 0.8f;
AudioManager.Instance.BgmVolume = 0.6f;
AudioManager.Instance.SaveVolumeSettings();
```

## 资源寻址

默认规则（`AudioAddresses.Clip`）：

```
bgm_main → bundles/audio/bgm_main.unity3d
```

音频 Clip 应放在 `Assets/Bundles/Audio/`，并在 YooAsset Collector 中配置打包。

## Bootstrap

```csharp
new ResourceModule(),
new AudioModule(),   // 须在 ResourceModule 之后
new UIModule(),
```

`Dependencies`：`LoggingModule`、`ResourceModule`、`CoroutineModule`。

## 设计说明

- **BGM**：双 `AudioSource` 交叉淡入淡出，单轨播放
- **SFX**：`AudioSource` 对象池（默认 16），支持 OneShot / 3D / Loop
- **资源**：仅 `ResourceManager` 加载；缓存按 location 只持有一个 `ResourceAssetHandle` 直到 `Shutdown`。同一 Clip 的并发 `LoadAsync` 会等待第一次完成，不会覆盖或泄漏句柄
- **淡入淡出**：走 `GameCoroutine.StartGlobal`
- **Shutdown**：停止全部音频 → 释放 Clip 缓存 → 保存音量 → 销毁 `AudioRoot`
