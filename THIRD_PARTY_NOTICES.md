# Third-Party Notices / 第三方声明

本仓库自研代码使用 MIT 许可（见 [LICENSE](LICENSE)）。下列第三方组件保留各自的许可，使用、分发前请阅读原始许可文本。

## 一、随框架源码分发的开源组件

这些代码位于框架包内，框架运行时直接依赖。

| 组件 | 位置 | 许可 | 来源 |
|---|---|---|---|
| FixedMath.Net（Fix64） | `Packages/com.jokerseed.framework.battle/Runtime/FixedMath` | Apache-2.0 | https://github.com/asik/FixedMath.Net |
| libfixmath / log2fix 算法 | 同上（经 FixedMath.Net 引入） | MIT | https://github.com/PetteriAimonen/libfixmath |
| Jitter Physics 线性代数（`TSVector` / `TSMatrix` / `TSQuaternion` 等） | 同上 | zlib | Thorben Linneweber，见各文件头 |
| Mono.Xna（`TSVector2`） | 同上 | MIT | The Mono.Xna Team，见文件头 |
| Farseer Physics Engine | `Packages/com.jokerseed.framework.battle/Runtime/LockstepPhysics/Farseer` | Ms-PL | Ian Qvist，见各文件头 |
| Box2D（Farseer 移植来源） | 同上 | zlib | Erin Catto，http://www.box2d.org |
| Jitter Physics 物理引擎 | `Packages/com.jokerseed.framework.battle/Runtime/LockstepPhysics/Jitter` | zlib | Thorben Linneweber |
| Photon TrueSync 数学封装 | `FixedMath`、`Lockstep.Legacy` 部分文件 | 见文件头；**使用方须自行确认授权** | Exit Games |

> Ms-PL 要求：以源码形式分发 Farseer 时须保留原许可声明；以二进制形式分发时不得以与 Ms-PL 冲突的许可再授权该部分。

## 二、工程内引用的开源依赖

| 组件 | 位置 / 获取方式 | 许可 | 来源 |
|---|---|---|---|
| UniTask 2.5.11 | `Packages/manifest.json` Git 依赖 | MIT | https://github.com/Cysharp/UniTask |
| YooAsset 3.0.5 | `LocalPackages/YooAsset-3.0.5`（本地包） | Apache-2.0 | https://github.com/tuyoogame/YooAsset |
| Luban 4.11.0（打表工具） | `ThirdParty/luban-4.11.0` | MIT | https://github.com/focus-creative-games/luban |
| luban_unity（`Luban.Runtime`） | `Packages/manifest.json` Git 依赖 | MIT | https://github.com/focus-creative-games/luban_unity |
| KCP（C# 移植） | `Assets/ThirdParty/KCP` | MIT | skywind3000，https://github.com/skywind3000/kcp |
| Google.Protobuf | `Assets/ThirdParty/Google.Protobuf` | BSD-3-Clause | https://github.com/protocolbuffers/protobuf |
| Newtonsoft.Json | `com.unity.nuget.newtonsoft-json` | MIT | https://www.newtonsoft.com/json |
| Unity MCP | `Packages/manifest.json` Git 依赖（仅编辑器） | MIT | https://github.com/CoplayDev/unity-mcp |

## 三、商业 / Asset Store 组件（重要）

下列目录是从 Unity Asset Store 或其他商业渠道获得的插件，**仅用于本仓库作者本地开发与测试**：

| 组件 | 位置 | 授权 |
|---|---|---|
| Animancer | `Assets/ThirdParty/Animancer` | Unity Asset Store EULA |
| Behavior Designer | `Assets/ThirdParty/Behavior Designer` | Unity Asset Store EULA |
| DOTween / DOTween Pro | `Assets/ThirdParty/DOTween` | Demigiant 许可；Pro 为 Asset Store EULA |
| Odin Inspector（Sirenix） | `Assets/ThirdParty/Sirenix` | Sirenix 商业许可 |
| Spine Runtimes | `Assets/ThirdParty/Spine` | Spine Runtimes License（须持有 Spine 编辑器授权） |

**说明：**

- Unity Asset Store EULA 一般不允许把插件源码公开再分发。这些目录目前仍在仓库里，作者计划取得正式授权后替换；在此之前，**它们不属于本项目的 MIT 授权范围**，任何人克隆本仓库都不因此获得这些插件的使用权。
- 想在自己的项目里使用本框架，请自行购买上述插件，或删除 `Assets/ThirdParty` 下对应目录。框架包（`Packages/com.jokerseed.framework.*`）的运行时代码不依赖这些插件；只有示例场景和部分编辑器工具会用到。
- `Assets/RawResources` 下的美术资源仅用于本地测试，同样不在 MIT 授权范围内。

## 四、完整许可文本

各组件的完整许可以上游仓库或插件包内附带的许可文件为准；源码文件头里的版权声明不得删除。
