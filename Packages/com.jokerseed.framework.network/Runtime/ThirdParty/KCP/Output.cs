using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace KcpCSharp {
    /// <summary>
    /// KCP 输出回调。<see cref="Kcp"/> 在调用后会立即复用 <c>msg</c> 的缓冲：实现必须在返回前发送或复制数据，不能保留引用。
    /// </summary>
    public abstract class Output {
        abstract public void output(ByteBuf msg, Kcp kcp, System.Object user);
    }
}
