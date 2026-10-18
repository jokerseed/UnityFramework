# KCP

C# 移植，随 `com.jokerseed.framework.network` 一起分发，供 `Framework.Network` 的 KCP 频道引用。

- 许可：MIT
- 上游：https://github.com/skywind3000/kcp
- 作者：skywind3000

文件头中的版权声明请保留。本目录不是框架自研代码。

本地改动（相对上游移植）：

- `Kcp.Flush` 复用输出缓冲（`buffer.Clear()`），不再每次输出后新建；因此 `Output.output` 必须在返回前消费数据。
- 新增只读属性 `Kcp.IsDeadLink`，供频道检测对端不可达。
- `ByteBuf.Wrap` 让同一个包装对象复用外部数组，避免每个数据报分配一次。
- `Output.cs` 去掉了未使用的 `using UnityEngine`。
