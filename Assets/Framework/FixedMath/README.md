# Framework.FixedMath

确定性定点数数学库（Q31.32），自 `D:\Client` 主干 TrueSync 数学层迁移。  
本模块为帧同步基座 **A**；调度见 `Framework.Lockstep`，物理见 `Framework.LockstepPhysics`。

> 源路径：`D:\Client\Assets\Script\Core\TrueSync\Engine\Math\`  
> 命名空间改为 `Framework.FixedMath`；已去掉 Client 侧 `Obscured*` / `IWGames` 依赖。  
> Sin/Tan/Acos **LUT 数组内容与主干逐项一致**（已对拍）。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.FixedMath` |
| 命名空间 | `Framework.FixedMath` |
| 依赖 | UnityEngine（`SerializeField` / `FPConversions`） |
| 模块 | **不是** `IGameModule` |

## 核心类型（与 Client 一致）

| 类型 | 职责 |
|------|------|
| `FP` | Q31.32 定点数（完整 Sin/Tan/Acos LUT，`LUT_SIZE≈205887`） |
| `TSMath` | Mathf 风格静态入口 |
| `TSVector` / `TSVector2` / `TSVector4` | 向量 |
| `TSQuaternion` | 四元数 |
| `TSMatrix` / `TSMatrix4x4` | 矩阵 |
| `TSRandom` | 确定性随机（Mersenne Twister，**必须带种子**） |
| `FPConversions` | 仅表现层 ↔ Unity float |

## 同步硬约定（违反极易导致难查证的不同步）

1. **逻辑帧 / 锁步路径禁止 `float`/`double` 参与运算**  
   - 禁止：`Next(float,float)`、`MoveTowardsAngle(..., float maxDelta)`、`(FP)someRuntimeFloat`（非字面量）  
   - 字面量如 `(FP)0.5f` 在迁移代码里存在（与 Client 相同）；新代码优先写 `FP.Half` / `FP.EN*` / `FromRaw`
2. **`FPConversions` 只用于渲染 / 调试**，结果不得写回模拟状态
3. **`TSRandom` 只用 `TSRandom.New(seed)` / `Init()`**  
   - 无参构造内部用 `DateTime.Now`，**非确定性**，禁止用于同步逻辑
4. **大数值战斗结算不要用 FP**（Q31.32 整数大约 ±2³¹）  
   - Client 已用 `SafeMath`+`long` 处理伤害/HP；物理（坐标/速度）才用 FP
5. **`operator +` / `operator -` / `operator *` 默认不饱和**  
   - 溢出按 `long` 回绕；需要饱和时显式调用 `OverflowAdd` / `OverflowSub` / `OverflowMul`（命名沿用 Client，语义是「带溢出处理」）
6. **禁止再引入第二套定点库**（FixedMathSharp 等），否则无法与 Client 对拍

## Framework 相对主干的刻意差异

| 项 | 说明 |
|----|------|
| 命名空间 | `TrueSync` → `Framework.FixedMath` |
| 去掉 Obscured 转换 | **刻意**无反作弊依赖；数值路径不变。若业务要内存混淆，在进出 `FP` 边界自行适配（见根 README [反作弊](../../../README.md#反作弊)） |
| 去掉 `TSMatrix4x4.TransformToMatrix(TSTransform)` | 依赖未迁移的 Unity 组件 |
| `operator +` 暂存 | 主干曾用 `private static FP result`（多线程危险）；已改为局部变量，**单线程结果与主干一致** |

## 典型用法

```csharp
using Framework.FixedMath;

var pos = new TSVector(-2, 0, 0);
var dir = (new TSVector(2, 0, 0) - pos).normalized;
pos += dir * FP.EN1;

var rng = TSRandom.New(battleSeed);
var roll = rng.Next(0, 100);

// 仅表现
transform.position = FPConversions.ToVector3(pos);
```

## 第三方说明

见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
