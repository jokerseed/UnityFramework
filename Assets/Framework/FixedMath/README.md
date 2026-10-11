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
| `TSRandom` | 确定性随机（Mersenne Twister，**必须带种子**）；`GetStateHash()` 不消耗序列；`CopyState` / `LoadState` 供快照 |
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
   - 向量模长在分量绝对值大约超过 46340 时，平方和会超出 `long`。`TSVector` / `TSVector2` / `TSVector4` 的平方和以及四元数 `Normalize` 改用 `OverflowAdd`：不溢出时与原来的 `+` 逐位相同；溢出时饱和，不再变成负数后让 `Sqrt` 抛异常
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
| 除零 / 开方 / 反余弦 | 除数 0：正→`MaxValue`，负→`MinValue`，`0/0`→0。`% 0` 返回 0。`Sqrt` 负数返回 0。`Acos` 先夹到 [-1, 1]。这些只改变原先会抛异常或返回 ±90 的输入；正常商、非负开方、[-1, 1] 内的反余弦逐位不变 |
| `Slerp` / `ClampMagnitude` / `Next(min,max)` | `Sin(halfTheta)==0` 时 `Slerp` 改走 `Lerp`。`ClampMagnitude` 不再把短向量拉长。`Next` 在上下界相等时返回该值且不推进随机序列；其它范围仍是 `Next() % range` |

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

## 三角函数查找表（约 3MB × 3）

`Fix64SinLut.cs`、`Fix64TanLut.cs`、`Fix64AcosLut.cs` 各约 3.0MB、25741 行，内容是 `long[]` 字面量。运行时直接使用这些静态数组，没有加载步骤。

不在这次改成“启动时生成”或“从 TextAsset / 二进制加载”：

- `Fix64` 里留下的生成函数用 `System.Math` 的 double 去写文件。double 三角函数不是这些字面量的稳定来源，换机器或换运行时可能和现表差 1 个 raw，定点 Sin/Tan/Acos 会整体漂移。
- 改成资源加载会让 `Framework.FixedMath` 依赖资源系统或文件路径。表没进包、加载顺序错了，第一次三角函数就会读到空表。
- 正确的迁移是：把**现有数组的原始 long 按固定小端序**倒成二进制，启动时填回同一个 `long[]`，并用测试逐项比对旧字面量。在那个测试存在之前不要换路径。

## 第三方说明

见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
