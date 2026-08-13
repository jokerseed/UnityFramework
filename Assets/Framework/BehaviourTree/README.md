# Framework.BehaviourTree

面向帧同步的轻量行为树：**逻辑帧 Tick + 定点时间 + 确定性随机**。  
含 **可视化编辑器**、**ScriptableObject 资产**、**JSON 导出**，工作流参考 Behavior Designer。

## 程序集

| 项 | 值 |
|----|---|
| 运行时 | `Framework.BehaviourTree` |
| 编辑器 | `Framework.BehaviourTree.Editor`（仅 Editor） |
| 依赖 | `Framework.FixedMath` |
| 模块 | **不是** `IGameModule` |

## 编辑器（可视化）

| 菜单 | 功能 |
|------|------|
| **Tools → Behaviour Tree → Editor** | 打开节点图编辑器 |
| **Tools → Behaviour Tree → Create Tree Asset** | 创建 `BtTreeAsset`（默认 `Assets/Bundles/BehaviourTrees/`） |
| **Create → Framework → Behaviour Tree** | 同上，CreateAssetMenu |

### 编辑器操作

- **右键画布**：添加节点（Sequence / Selector / Parallel / 装饰 / 等待 / 黑板条件 / 自定义）
- **拖拽节点**：调整布局（写入 `EditorPosition`，仅 Editor）
- **Inspector 面板**：改参数、设 Root、组合节点增删/排序子节点、装饰节点指定子节点
- **Export JSON**：导出 `{资产名}.bt.json`（与 `.asset` 同目录）
- **Import JSON**：从 JSON 回灌资产
- **Compile Test**：校验配置能否编译为运行时树

## 配置文件

| 格式 | 类型 | 用途 |
|------|------|------|
| Unity 资产 | `BtTreeAsset` | 主编辑载体，可进 YooAsset 打包 |
| JSON | `*.bt.json` | 版本管理、CI、外部工具；字段见 `BtTreeDefinition` |

JSON 示例结构：

```json
{
  "Version": 1,
  "TreeName": "MonsterCommon",
  "RootNodeId": "abc...",
  "Nodes": [
    {
      "Id": "abc...",
      "Kind": 1,
      "DisplayName": "Root",
      "EditorPosition": { "x": 200, "y": 80 },
      "ChildIds": ["..."],
      "IntParam": 0,
      "FloatParam": 0,
      "StringParam": "",
      "ParallelPolicy": 0
    }
  ]
}
```

`Kind` 对应 `BtNodeKind` 枚举整型值。

## 运行时加载

```csharp
using Framework.BehaviourTree;
using Framework.FixedMath;

// 1. ScriptableObject（每个 Agent 单独 Compile 一棵实例）
BtTreeAsset asset = ...; // YooAsset / 引用
var tree = BtTreeLoader.Load(asset, customRegistry);

// 2. JSON 文本
var tree = BtTreeLoader.LoadFromJson(jsonText, customRegistry);

// 3. 逻辑帧 Tick
var ctx = new BtContext(blackboard, owner, TSRandom.New(seed));
ctx.AdvanceFrame(lockedTimeStep);
tree.Tick(ctx);
```

## 自定义节点（类似 Client `HFAction.ClassName`）

实现 `IBtNodeRegistry`，处理 `BtNodeKind.CustomAction` / `CustomCondition`：

```csharp
public sealed class GamePlayBtRegistry : IBtNodeRegistry
{
    public bool TryCreate(BtConfigNode config, out BtNode node)
    {
        node = null;
        if (config.Kind == BtNodeKind.CustomAction && config.StringParam == "Attack")
        {
            node = new BtAction(ctx => { /* 确定性攻击 */ return BtStatus.Success; });
            return true;
        }
        return false;
    }
}
```

编译时注入：`BtTreeLoader.Load(asset, new GamePlayBtRegistry());`

## 内置节点

| Kind | 说明 |
|------|------|
| Sequence / Selector / Parallel | 组合 |
| Inverter / Repeater / ForceSuccess | 装饰 |
| WaitFrames / WaitTime | 等待（逻辑帧 / FP 时间） |
| BlackboardBool | 读黑板 bool 键 |
| CustomAction / CustomCondition | 业务注册 |

## 锁步约定

1. 仅逻辑帧调用 `Tick`  
2. 禁止 `Time.*` / Unity 协程 / async 权威路径  
3. `WaitTime` 配置为秒，编译为 `FP.FromFloat`  
4. 随机走 `ctx.Random`（`TSRandom.New(seed)`）  
5. **一树一 Agent**；本版 **不做回滚快照**

## 与 Behavior Designer 的差异

| BD | 本模块 |
|----|--------|
| 全功能商业编辑器 | 轻量 IMGUI 节点图 + JSON |
| Unity Update 驱动 | 手动逻辑帧 Tick |
| float / 协程常见 | FP + 帧计数等待 |
| 大量内置 Task | 内置组合/等待 + 自定义注册 |

## 资源路径建议

```
Assets/Bundles/BehaviourTrees/MonsterCommon.asset
Assets/Bundles/BehaviourTrees/MonsterCommon.bt.json   ← Export 产出
```

打包进 YooAsset 后，运行时 `LoadAssetSync<BtTreeAsset>(location)` → `BtTreeLoader.Load`。

## 验证

Editor 内 **Compile Test**；运行时固定种子 + 固定 `DeltaTime` 对拍 `LastStatus`。
