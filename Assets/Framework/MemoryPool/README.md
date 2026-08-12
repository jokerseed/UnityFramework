# Framework.MemoryPool

轻量级内存池，参考 [TEngine MemoryPool](https://github.com/Alex-Rachel/TEngine)。用于高频小对象复用，降低 GC。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.MemoryPool` |
| 命名空间 | `Framework.MemoryPool` |
| 依赖 | `Framework.Bootstrap`、`Framework.Logging` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `IMemory` | 可池化对象契约，`Clear()` 归还前复位 |
| `MemoryPool` | 静态 `Acquire` / `Release` / `Add` / `ClearAll` |
| `MemoryPoolModule` | Bootstrap 模块：严格检查开关，Shutdown 清空 |

## 用法

```csharp
public sealed class TempBuffer : IMemory
{
    public List<int> Values = new List<int>();

    public void Clear()
    {
        Values.Clear();
    }
}

var buf = MemoryPool.Acquire<TempBuffer>();
// ... 使用
MemoryPool.Release(buf);
buf = null; // Release 后禁止再访问
```

## Bootstrap

```csharp
new MemoryPoolModule(enableStrictCheck: true), // Editor 建议开
```

依赖：`LoggingModule`。

## `enableStrictCheck`

对应 `MemoryPool.EnableStrictCheck`，由 `MemoryPoolModule` 在初始化时写入。

### 实际作用

- 检测「没有对应 `Acquire` 却 `Release`」——最常见是**重复 Release**
- 一旦发现直接抛异常，而不是默默把对象再塞进池（那会破坏池状态）

### 什么时候开

| 环境 | 建议 |
|------|------|
| Editor / 调试 | `true`（`Launch` 默认如此） |
| 正式包 | 可关，少一次判断、也避免把池错误变成线上崩溃（根因仍应修复） |

### 它不会做的事

- 不会阻止 `Release` 后继续访问对象（须自行把引用置 `null`）
- 不是泄漏扫描器（TEngine 编辑器另有更完整的检查 UI；本模块目前主要防双重 `Release`）

## 与对象池的区别

| | MemoryPool | ObjectPool |
|--|------------|------------|
| 对象 | 实现 `IMemory` 的小 C# 对象 | 继承 `ObjectBase`，常挂 Target |
| API | `Acquire` / `Release` | `Spawn` / `Unspawn` |
| 能力 | 极简复用 | 容量、过期、自动释放、命名池 |
