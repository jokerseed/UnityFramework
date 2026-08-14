# Framework.Network

参考 [TEngine](https://github.com/Alex-Rachel/TEngine) / GameFramework 的网络频道模型：命名 Channel、长度前缀拆包、主线程分发、心跳。TEngine 当前仓库的 `Books/3-8-网络模块.md` 仍为「待补充」，本模块按同一套频道 API 落地 TCP 客户端。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Network` |
| 命名空间 | `Framework.Network` |
| 依赖 | `Framework.Core`、`Framework.Logging`、`Framework.MemoryPool`、`Framework.Events` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `NetworkManager` | 创建/销毁命名频道，`Update` 驱动分发与心跳 |
| `INetworkChannel` | 连接、发送、注册消息处理 |
| `TcpNetworkChannel` | TCP 异步收发（`SocketAsyncEventArgs`） |
| `INetworkChannelHelper` | 协议：包头长度、序列化、反序列化、心跳 |
| `DefaultNetworkChannelHelper` | 默认二进制协议 |
| `NetworkPacket` | 可池化消息包（`IMemory`） |
| `NetworkModule` | Bootstrap 模块入口 |

## 默认协议

小端字节序：

```
int32  bodyLength
ushort messageId
byte[] payload
```

`messageId = 0` 为心跳；未单独注册处理器时不会分发给业务。

可实现 `INetworkChannelHelper` 替换为 Protobuf / MemoryPack 等。

## 典型用法

```csharp
using Framework.Events;
using Framework.Network;

var channel = NetworkManager.Instance.CreateTcpChannel("Game");
channel.HeartBeatInterval = 30f;

GameEvent.Listen<NetworkConnectedEvent>(e =>
{
    if (e.ChannelName == "Game")
    {
        channel.Send(1001, System.Text.Encoding.UTF8.GetBytes("hello"));
    }
});

channel.RegisterHandler(1002, (c, packet) =>
{
    // packet 在回调返回后归还内存池，不要缓存
});

channel.Connect("127.0.0.1", 9000);

// 关闭
channel.Close();
NetworkManager.Instance.DestroyChannel("Game");
```

## 事件

通过 `GameEvent` 订阅（均为 `struct`）：

| 事件 | 时机 |
|------|------|
| `NetworkConnectedEvent` | 连接成功 |
| `NetworkClosedEvent` | 主动关闭或对端断开 |
| `NetworkErrorEvent` | 连接/收发/解析失败 |
| `NetworkMissHeartBeatEvent` | 心跳间隔内未收到任何包 |

收发包完成回调都在 **主线程** `NetworkManager.Update` 中执行。

## Bootstrap

```csharp
new LoggingModule(),
new MemoryPoolModule(),
new NetworkModule(), // Dependencies: Logging + MemoryPool
```

## 设计说明

- 不绑定具体业务协议，只提供 Channel + Helper
- IO 用 `SocketAsyncEventArgs`，不用 `Task` / 显式 `Thread`
- 消息包走 `MemoryPool`，热路径避免额外 `IDisposable`
- 心跳默认关闭（`HeartBeatInterval <= 0`）；有服务端再打开
