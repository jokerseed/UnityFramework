# Framework.BehaviourTree

面向帧同步的轻量行为树：**逻辑帧 Tick + 定点时间 + 确定性随机**。  
含 **可视化编辑器**、**ScriptableObject 资产**、**JSON 导出**。模板可共享，每个 Agent 持有独立运行时槽。

## 程序集

| 项 | 值 |
|----|---|
| 运行时 | `Framework.BehaviourTree` |
| 编辑器 | `Framework.BehaviourTree.Editor`（仅 Editor） |
| 依赖 | `Framework.FixedMath` |
| 模块 | **不是** `IGameModule` |

## 定义 / 实例

| 类型 | 职责 |
|------|------|
| `BtTreeTemplate` | 共享拓扑；Flatten 后赋 `Index` |
| `BtRuntime` | 按 Index 存 Status / int / FP / Started |
| `BehaviourTree` | 模板 + 本 Agent Runtime；`Instantiate()` 再开实例 |

`new BehaviourTree(root, name)` 与 `BtTreeBuilder.Build` 仍可用：内部先建模板再分配 Runtime。节点禁止再放 per-Agent 可变字段。

## Tick

```csharp
var ctx = new BtContext(blackboard, owner, TSRandom.New(seed));
ctx.AdvanceFrame(lockedTimeStep);
tree.Tick(ctx);
```

`owner is IBtAgent` 时自动赋 `ctx.Agent`；`GetOwner<T>()` / `GetAgent<T>()` 并存。

## 条件打断与 Abort

`BtAbortType`：`None`（默认）/ `Self` / `LowerPriority` / `Both`。

- **Selector**：`LowerPriority` / `Both` 时每帧重评 `0..current-1`，更高优先级 Running/Success 则 Abort 当前
- **Sequence**：`Self` / `Both` 时重评前缀，Failure 则 Abort 正在跑的子节点
- **ActiveSelector**：固定 `LowerPriority`

换分支必须走 `Abort`：`BtAction` 的 `onAbort` 先于 Reset；组合用 `AbortChild` / `AbortChildrenFrom`。

## 快照

`CaptureSnapshot` / `RestoreSnapshot` 还原：FrameIndex、LastStatus、Runtime、权威黑板。  
**不还原** object 袋与 `TSRandom`。

## 类型化黑板

权威槽：`int` / `bool` / `FP(raw)` / `uint`，无装箱。`SetObject` 进非权威袋，**不进 Clone**。保留 `TryGet<T>`。

## 配置（版本 2）

`BtConfigNode` 增加 `Params`、`TypeId`、`AbortType`、`Breakpoint`、`Weights`、`HasFpRaw` / `FpRawParam`。  
旧 JSON（Version &lt; 2）导入时自动迁移。仍保留 Int / Float / StringParam。

时长权威值：`HasFpRaw ? FP.FromRaw : FP.FromFloat`。编辑器 `SetDurationSeconds` 同时写展示 float 和 RawValue。运行时节点只收 `FP`。

## 编译与工厂

顺序：**业务 `IBtNodeRegistry` → `BtNodeFactory.Default` → `BtBuiltinNodeRegistry`**。  
`BtNodeFactory.Register(typeId, factory)`。代码闭包 `BtAction` 仍可用。

`BtNodeKind.Subtree = 40`：`IBtSubtreeResolver` 编译期内联，外包 `BtSubtree`；访问栈检测循环。`SubtreeId` 空则回退 StringParam。

`BtTreeValidator` 产出 Error / Warning / Info。**Compile 遇到 Error 抛异常**。

## 调试

`CollectDebug` 默认 false：权威路径不打 Stopwatch。Editor 下仍会 `BtDebugHub.Publish` 供着色。  
`BtDebugFrame`：Statuses、RunningPath（按 Runtime 往下走）、ElapsedTicks、BreakpointNodeIndex。

## 编辑器

| 菜单 | 功能 |
|------|------|
| **Tools → Behaviour Tree → Editor** | 打开节点图 |
| **Tools → Behaviour Tree → Create Tree Asset** | 创建 `BtTreeAsset`（默认 `Assets/Bundles/BehaviourTrees/`） |
| **Tools → Behaviour Tree → Export All Runtime JSON** | 批量导出旁路 `.bt.json` 供热更 |
| **Create → Framework → Behaviour Tree** | 同上创建资产 |

### 操作

- **右键画布**：添加节点
- **Ctrl / Shift 点击** 或 **框选**：多选；拖动整组
- **非法连线标红**：缺子、自环、装饰器多于 1 个子（装饰器只认第一个 ChildId）
- **Undo**：改图 / 改参走 `Undo.RecordObject`
- **Play**：按 ConfigId 着色（Success 绿 / Failure 红 / Running 黄）；命中断点可暂停
- **Inspector**：AbortType、FailFast / SucceedFast、RepeatOnFailure、Weights、TypeId / SubtreeId、时长、Breakpoint
- **Lint**：图编辑器与资产 Inspector 常驻；点击可选中节点
- **Export / Import JSON**、**Compile Test**

## 运行时加载

```csharp
// Editor / 已持有 ScriptableObject
var tree = BtTreeLoader.Load(asset, customRegistry, subtrees);

// 热更 TextAsset（.bt.json）
var tree2 = BtTreeLoader.LoadFromTextAsset(textAsset, customRegistry);

// 仅编译共享模板，多 Agent Instantiate
var template = BtTreeLoader.CompileToTemplateFromJson(json, customRegistry);
var agentTree = new BehaviourTree(template);
```

自定义节点实现 `IBtNodeRegistry`，处理 `CustomAction` / `CustomCondition`，或 `BtNodeFactory.Default.Register`。

## 热更资源与运行时替换

Player 以 **`.bt.json`（TextAsset）** 为准；`BtTreeAsset` 仅服务编辑器。

| 步骤 | 操作 |
|------|------|
| 1. 编辑 | `Tools → Behaviour Tree → Editor` |
| 2. 生成运行时资源 | `Tools → Behaviour Tree → Export All Runtime JSON`（或单资产 Export） |
| 3. 打包 | YooAsset 收集 `Assets/Bundles/BehaviourTrees/*.bt.json` |
| 4. 加载 | `BtTreeResource.LoadTree("MonsterCommon")`（GamePlay，经 `ResourceManager`） |
| 5. 替换 | `battleAgent.ReplaceFromResource("MonsterCommon")` 或 `ReplaceTree(newTree)` |

寻址：`ResourceAddresses.BehaviourTree(treeId)` → `bundles/behaviourtrees/{treeid}.unity3d`。  
模板缓存：`BtTreeTemplateCache.Shared`；热更换包后 `BtTreeResource.Invalidate` / `ClearCache`。

替换语义：中止旧树 → 新实例干净 Runtime → **默认保留黑板**（`clearBlackboard: true` 可清空）。

```csharp
var agent = new BattleAgent(BtTreeResource.LoadTree("MonsterCommon"), new BtBlackboard());
// 热更后：
BtTreeResource.Invalidate("MonsterCommon");
agent.ReplaceFromResource("MonsterCommon");
```

## 内置节点

| Kind | 说明 |
|------|------|
| Sequence / Selector / Parallel | 组合；Parallel 另有 FailFast / SucceedFast |
| RandomSelector / WeightedSelector / ActiveSelector | 随机 / 加权 / 主动选择 |
| Inverter / Repeater / ForceSuccess / ForceFailure | 装饰；Repeater 可 RepeatOnFailure |
| UntilSuccess / Cooldown / Timeout / TimeLimit | 装饰；Timeout 超时 Failure，TimeLimit 超时 Success |
| WaitFrames / WaitTime | 等待（逻辑帧 / FP 时长） |
| BlackboardBool | 读黑板 bool 键 |
| CustomAction / CustomCondition | 业务注册 |
| Subtree | 编译期内联另一棵树 |

## 锁步约定

1. 仅逻辑帧调用 `Tick`
2. 禁止 `Time.*` / Unity 协程 / async 权威路径
3. 时长用 `FP`（配置优先 FpRaw）
4. 随机走 `ctx.Random`（`TSRandom.New(seed)`）；RandomSelector 在 Random 为 null 时取 0
5. 模板可共享；含闭包捕获单个 Agent 的叶子不要跨 Agent 共享该模板
6. 回滚用 `CaptureSnapshot` / `RestoreSnapshot`（不含 object 袋与随机源）

## 资源路径建议

```
Assets/Bundles/BehaviourTrees/MeleeChaser.bt.json
Assets/Bundles/BehaviourTrees/MonsterCommon.asset   # 仅 Editor
Assets/Bundles/BehaviourTrees/MonsterCommon.bt.json # Player / 热更
```

战斗演示近战树：`MeleeChaser`；自定义叶子 TypeId 见 GamePlay `BattleAiTypeIds`（`Battle.IsAlive` 等），由 `BattleAiNodeRegistry.EnsureRegistered` 注册。

## 验证

Editor 内 **Compile Test** 与 lint 面板；运行时固定种子 + 固定 `DeltaTime` 对拍 `LastStatus`。
