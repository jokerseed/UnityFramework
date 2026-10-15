# Framework.Network

参考 [TEngine](https://github.com/Alex-Rachel/TEngine) / GameFramework 的网络频道模型：命名 Channel、长度前缀拆包、主线程分发、心跳。TEngine 当前仓库的 `Books/3-8-网络模块.md` 仍为「待补充」，本模块按同一套频道 API 落地 TCP 客户端。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.Network` |
| 命名空间 | `Framework.Network` |
| 依赖 | `UniTask`、`Framework.Core`、`Framework.Logging`、`Framework.MemoryPool`、`Framework.Events`、`KCP`（本包 `Runtime/ThirdParty/KCP`，MIT） |

## 核心类型

| 类型 | 职责 |
|------|------|
| `NetworkManager` | 创建/销毁命名频道，`Update` 驱动分发与心跳 |
| `INetworkChannel` | 连接、发送、注册消息处理 |
| `TcpNetworkChannel` | TCP 异步收发（`SocketAsyncEventArgs`） |
| `WebSocketNetworkChannel` | WebSocket 二进制帧收发，一帧一包；WebGL 走浏览器 WebSocket |
| `IWebSocketTransport` | WebSocket 底层传输，可替换为平台 SDK 实现 |
| `KcpNetworkChannel` | KCP over UDP，一条 KCP 消息一包；WebGL / 小游戏创建即失败 |
| `IKcpDatagramTransport` | KCP 底层数据报，可替换 |
| `HttpRequests` | HTTP 短请求（GET / POST）。不是频道，没有心跳和消息号 |
| `NetworkRpcClient` | 请求-响应 RPC：请求号匹配、超时、取消、断线失败 |
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
channel.UnregisterHandler(1002);

channel.Connect("127.0.0.1", 9000);

// 关闭
channel.Close();
NetworkManager.Instance.DestroyChannel("Game");
```

## WebSocket

```csharp
var ws = NetworkManager.Instance.CreateWebSocketChannel("Game");
ws.Connect(new Uri("wss://example.com/game"));   // 或 ws.Connect("127.0.0.1", 9000) → ws://127.0.0.1:9000/
```

- 每条二进制消息就是一个完整包（包头 + 包体），格式与 TCP 频道相同，服务端可共用编解码；文本帧、长度对不上的帧都按协议错误处理：先发 `NetworkErrorEvent`，再关闭连接
- 默认传输：非 WebGL 用 `ClientWebSocket`（线程池收发），WebGL 用 `FrameworkWebSocket.jslib`（浏览器 WebSocket，主线程回调）
- 微信小游戏等平台需要用 SDK 的 socket 时，实现 `IWebSocketTransport`，再用 `new WebSocketNetworkChannel(name, helper, listener => new MyTransport(listener))` 创建频道
- 事件、心跳、主线程分发规则与 TCP 频道一致；传输回调只入队，`Update` 时才分发

## 选择传输

长连接用 `NetworkServiceType` 选择，业务侧拿到的都是 `INetworkChannel`（包格式、心跳、`NetworkRpcClient` 相同）：

```csharp
NetworkManager.Instance.CreateTcpChannel("Game");
NetworkManager.Instance.CreateWebSocketChannel("Game");
NetworkManager.Instance.CreateKcpChannel("Game", conversation: 1); // 两端会话号必须相同
// 或 CreateChannel(name, NetworkServiceType.Kcp)
```

## KCP

```csharp
var kcp = NetworkManager.Instance.CreateKcpChannel("Game", conversation: 1);
kcp.Connect("127.0.0.1", 9000);
```

- 一条 KCP 消息就是一个完整包，格式与 TCP / WebSocket 相同
- UDP 没有握手：`Connect` 成功只表示本地套接字已指向远端，随即进入 `Connected`
- 默认 `NoDelay(1, 10, 2, 1)`、窗口 128、RTO 上限 3 秒。时钟用 `Environment.TickCount`，不进战斗模拟
- WebGL / 小游戏没有 UDP。`CreateKcpChannel`、`CreateChannel(..., Kcp)` 和 `new KcpNetworkChannel` 会抛 `PlatformNotSupportedException`，不会改走 WebSocket
- 编辑器里即使构建目标是 WebGL 仍可创建（带 `UNITY_EDITOR`）。自定义 `IKcpDatagramTransport` 同样受这条约束
- 数据报回调只入队，KCP 的 `Input` / `Update` 在主线程 `Update` 里执行

## HTTP

短请求，不进 `NetworkServiceType`：

```csharp
HttpResponse config = await HttpRequests.GetAsync("https://example.com/config", cancellationToken: token);
HttpResponse login = await HttpRequests.PostAsync("https://example.com/login", body, "application/json", timeoutSeconds: 10f, cancellationToken: token);
```

- 只接受绝对的 `http` / `https` 地址
- 连不上或传输失败抛 `InvalidOperationException`；HTTP 4xx / 5xx 不抛，看 `HttpResponse.StatusCode` 和 `IsSuccess`
- 取消抛 `OperationCanceledException`

## RPC

```csharp
var rpc = new NetworkRpcClient(channel, defaultTimeoutSeconds: 10f);
byte[] reply = await rpc.CallAsync(requestMessageId: 2001, responseMessageId: 2002, payload, cancellationToken: token);
rpc.Dispose();
```

- 线上格式：请求载荷前加 4 字节小端请求号；服务端在响应载荷前原样带回这个请求号
- 超时抛 `TimeoutException`，取消抛 `OperationCanceledException`，频道未连接或等待期间关闭抛 `InvalidOperationException`
- 首次使用某个响应消息号时会在频道上注册处理器（覆盖已有的同号处理器）；`Dispose` 时注销
- 超时计时用 `UniTask.Delay(Realtime)`，WebGL 下同样可用

## 事件

通过 `GameEvent` 订阅（均为 `struct`）：

| 事件 | 时机 |
|------|------|
| `NetworkConnectedEvent` | 连接成功 |
| `NetworkClosedEvent` | 主动关闭、对端断开，或收发错误把连接关掉。同一次断线只发一次 |
| `NetworkErrorEvent` | 连接/收发/解析失败 |
| `NetworkMissHeartBeatEvent` | 心跳间隔内未收到任何包 |

收发包完成回调都在 **主线程** `NetworkManager.Update` 中执行。心跳用的是 `Update` 传入的 `Time.unscaledDeltaTime`，不受 `timeScale` 影响。单个处理器抛异常只跳过这一条消息，后面的频道继续 `Update`。IO 线程只入队，不要在完成回调里发布 `GameEvent` 或取内存池。

每次 `BeginConnect` 新建一套 socket 和三个 `SocketAsyncEventArgs`，代号记在这套连接上。旧连接的完成回调只看自己的那套，不会改到新连接的缓冲，也不会把新连接关掉。`Close` 会取消还没开始的排队连接；`BeginConnect` 发现代号已经变了就放弃。关闭时，已经不在进行中的接收和发送参数立刻释放；还挂在套接字上的那一次，等它自己的回调结束再释放。连接参数在连接回调结束时释放。接收在这套连接自己的缓冲里原地解析，不为每次接收拷贝整段已积累的字节。`SendAsync` 只写出一部分时会从剩余偏移继续发，整包发完才增加 `SentPacketCount`。同一消息号再次 `RegisterHandler` 会覆盖并打警告。

套接字、接收偏移、发送中标记、连接状态、排队中的重连地址，以及心跳和收发计数，都在频道自己的 `_sync` 里。解析和反序列化在锁外进行，所以辅助器可以 `Acquire` 内存池；锁顺序是先 `_sync`，再内存池的锁。不要在持有 `_sync` 时调用辅助器或用户回调。同一次断线只投递一次 `Closed`。`Close` 会把心跳丢失次数和收发计数清零。

## Bootstrap

```csharp
new LoggingModule(),
new MemoryPoolModule(),
new NetworkModule(), // Dependencies: Logging + MemoryPool
```

## 设计说明

- 不绑定具体业务协议，只提供 Channel + Helper
- TCP 的 IO 用 `SocketAsyncEventArgs`，不用 `Task` / 显式 `Thread`；`Socket` 只在 `TcpNetworkChannel` 上暴露，`INetworkChannel` 不绑定具体传输
- KCP 协议栈随本包放在 `Runtime/ThirdParty/KCP`（MIT），类型不暴露给业务。UDP 完成回调只入队，不在那条线程上推进 KCP
- TCP 与 KCP 默认是明文。需要加密时用 `wss://`，或在 `INetworkChannelHelper` 里自行封装载荷；框架不提供 TLS 套接字
- HTTP 是短请求，不要塞进 `INetworkChannel`
- 消息包走 `MemoryPool`，热路径避免额外 `IDisposable`
- 心跳默认关闭（`HeartBeatInterval <= 0`）；有服务端再打开
