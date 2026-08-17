
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading;


namespace KcpCSharp
{
   
    public abstract class KcpOnUdp : Output
    {    
        private long sendPacketCount = 0;
        private long recvPacketCount = 0;
        private long appSendEnqueueCount = 0;
        //private static string BuildShortUdpLog(bool isOutgoing, long cumulativeIndex, int length, DateTime ts)
        //{
        //    string dir = isOutgoing ? "OUT" : "IN";
        //    string key = isOutgoing ? "cumulative_out" : "cumulative_in";
        //    int tick = Environment.TickCount;
        //    return $"[KCP][UDP_{dir}] {key}={cumulativeIndex} sysTick={tick} time={ts:yyyy-MM-dd HH:mm:ss.fff} len={length}";
        //}

        /// <summary>底层 UDP 套接字。</summary>
        protected UdpClient client;
        protected Kcp kcp;
        protected IPEndPoint serverAddr;
        protected Object LOCK = new Object();//加锁访问收到的数据
        protected Object SEND_LOCK = new Object();//加锁访问发送列表
        protected LinkedList<ByteBuf> received;
        protected LinkedList<ByteBuf> sendList;
        protected int nodelay;
        protected int interval = Kcp.IKCP_INTERVAL;
        protected int resend;
        protected int nc;
        protected int sndwnd = Kcp.IKCP_WND_SND;
        protected int rcvwnd = Kcp.IKCP_WND_RCV;
        protected int mtu = Kcp.IKCP_MTU_DEF;
        protected volatile bool needUpdate;
        protected long timeout;//超时
        protected DateTime lastTime;//上次检测时间
        private IPEndPoint curAddr;//当前的客户端地址
        private bool isConnected;
        private volatile bool receiveLoopRunning;
        private Thread receiveThread;
        public KcpOnUdp() : this(0)
        {
        }
        /// <summary>
        /// 指定本地监听端口构造。
        /// </summary>
        public KcpOnUdp(int port)
        {
            client = new UdpClient(port);
            kcp = new Kcp(port, this, null);
            this.received = new LinkedList<ByteBuf>();
            this.sendList = new LinkedList<ByteBuf>();
        }
        /// <summary>
        /// 连接到地址
        /// </summary>
        public void Connect(string host, int port)
        {
            if (host != null)
            {
                serverAddr = new IPEndPoint(IPAddress.Parse(host), port);
            }
            // mode setting：必须在开始收发前设置
            kcp.NoDelay(nodelay, interval, resend, nc);
            kcp.WndSize(sndwnd, rcvwnd);
            kcp.SetMtu(mtu);
            try
            {
                try
                {
                    const int udpBufBytes = 4 * 1024 * 1024;
                    this.client.Client.ReceiveBufferSize = udpBufBytes;
                    this.client.Client.SendBufferSize = udpBufBytes;
                }
                catch
                {
                }
                if (serverAddr != null)
                {
                    this.client.Connect(serverAddr);
                }
                curAddr = new IPEndPoint(IPAddress.Any, 0);
                isConnected = true;
                receiveLoopRunning = true;
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "KcpUdpRecv"
                };
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                this.HandleException(ex);
            }
        }

        /// <summary>停止收包线程标志（在关闭 UdpClient 之前调用）。</summary>
        protected void StopUdpReceiveLoop()
        {
            receiveLoopRunning = false;
        }

        private void EnqueueIncomingDatagram(byte[] data)
        {
            long recvCount = Interlocked.Increment(ref recvPacketCount);
#if KCP_UDP_VERBOSE_LOG
            UnityEngine.Debug.Log(BuildShortUdpLog(false, recvCount, data.Length, DateTime.Now));
#endif
            lock (LOCK)
            {
                this.received.AddLast(new ByteBuf(data));
                this.needUpdate = true;
                this.lastTime = DateTime.Now;
            }
        }

        /// <summary>阻塞 Receive + 尽量排空 Available，缩短数据在内核缓冲中的停留时间。</summary>
        private void ReceiveLoop()
        {
            while (receiveLoopRunning)
            {
                try
                {
                    byte[] data = client.Receive(ref curAddr);
                    EnqueueIncomingDatagram(data);
                    const int maxBurst = 2048;
                    for (int i = 0; i < maxBurst && receiveLoopRunning && client.Available > 0; i++)
                    {
                        data = client.Receive(ref curAddr);
                        EnqueueIncomingDatagram(data);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException ex)
                {
                    if (!receiveLoopRunning)
                    {
                        break;
                    }
                    this.HandleException(ex);
                    break;
                }
                catch (Exception ex)
                {
                    if (receiveLoopRunning)
                    {
                        this.HandleException(ex);
                    }
                    break;
                }
            }
        }
        /// <summary>设置“多久收不到任何 UDP 就判定超时”。</summary>
        public void Timeout(long timeout)
        {
            this.timeout = timeout;
        }
        /// <summary>
        /// 超时设定
        /// 这里记录的是“真正调用 UdpClient.Send 的时间点”。
        /// </summary>
        public override void output(ByteBuf msg, Kcp kcp, Object user)
        {

            try
            {
                if (isConnected)
                {
                    // msg 中是 KCP 组装后的 UDP 负载（可能是控制包、业务包，或两者混合）
                    byte[] raw = msg.GetRaw();
                    int len = msg.ReadableBytes();
                    this.client.Send(raw, len);
                }
            }
            catch (Exception ex)
            {

                this.HandleException(ex);
            }

        }
        /// <summary>
        /// 应用层发送入口：这里只做“入发送队列”。
        /// 真正发 UDP 发生在 output() 回调中。
        /// </summary>
        /// <param name="content"></param>
        public void Send(ByteBuf content)
        {
            lock (this.SEND_LOCK)
            {
                this.sendList.AddLast(content);
                // 通知工作线程尽快 Update，减少入队到发送的等待
                this.needUpdate = true;
            }
        }
        /// <summary>
        /// KCP 主循环一次迭代：
        /// 1) received -> kcp.Input
        /// 2) kcp.Receive -> HandleReceive
        /// 3) sendList -> kcp.Send
        /// 4) kcp.Update / kcp.Check
        /// 5) 超时检测
        /// </summary>
        public void Update()
        {
            int cur = Environment.TickCount;
            kcp.SyncMillis(cur);
            bool hadUdpInput = false;
            // 1) input：把 UDP 收到的原始包全部喂给 KCP
            lock (LOCK)
            {
                while (this.received.Count > 0)
                {
                    hadUdpInput = true;
                    ByteBuf bb = this.received.First.Value;
                    kcp.Input(bb);
                    this.received.RemoveFirst();
                }
            }
            // 2) receive：从 KCP 取“完整上层消息”，不完整消息不会交付
            int len;
            while ((len = kcp.PeekSize()) > 0)
            {
                ByteBuf bb = new ByteBuf(len);
                int n = kcp.Receive(bb);
                if (n > 0)
                {
                    this.HandleReceive(bb);
                }
            }
            // 3) send：把应用层待发送队列交给 KCP
            bool hadAppSend = false;
            lock (this.SEND_LOCK)
            {
                while (this.sendList.Count > 0)
                {
                    hadAppSend = true;
                    ByteBuf item = this.sendList.First.Value;
                    this.kcp.Send(item);
                    this.sendList.RemoveFirst();
                }
            }
            // 有入站解析或刚提交发送时，避免「本轮 slap<0 不 Flush」把 ACK/新数据推迟到下一个 interval
            if (hadUdpInput || hadAppSend)
            {
                cur = Environment.TickCount;
                kcp.SyncMillis(cur);
                kcp.MarkFlushImmediate(cur);
                this.needUpdate = true;
            }
            // 4) update：驱动 KCP 内部时钟与重传逻辑（毫秒：Environment.TickCount，与 KCP interval/RTO 一致）
            if (this.needUpdate || cur >= kcp.GetNextUpdate())
            {
                kcp.Update(cur);
                kcp.SetNextUpdate(kcp.Check(cur));
                this.needUpdate = false;
            }
            // 5) check timeout：长时间收不到任何 UDP 包则触发超时
            if (this.timeout > 0 && lastTime != DateTime.MinValue)
            {
                double del = (DateTime.Now - this.lastTime).TotalMilliseconds;
                if (del > this.timeout)
                {
                    isConnected = false;
                    this.HandleTimeout();
                }
            }
        }

        /// <summary>
        /// 供 KcpClient 工作线程睡眠调度：按 ikcp_check 的下次唤醒时刻与 TickCount 的差值休眠，
        /// 避免原先「至少空转 10ms」叠加在弱网 RTO 上放大体感延迟。
        /// </summary>
        protected int GetWorkerSleepMilliseconds()
        {
            if (needUpdate)
            {
                return 0;
            }
            int cur = Environment.TickCount;
            int next = kcp.GetNextUpdate();
            unchecked
            {
                uint delta = (uint)next - (uint)cur;
                if (delta > 10)
                {
                    return 10;
                }
                return (int)delta;
            }
        }

        /**
         * 处理收到的消息
         */
        protected abstract void HandleReceive(ByteBuf bb);
        /// <summary>
        /// 处理异常
        /// </summary>
        /// <param name="ex"></param>
        protected abstract void HandleException(Exception ex);
        /// <summary>
        /// 超时处理
        /// </summary>
        protected abstract void HandleTimeout();
        /**
         * fastest: ikcp_nodelay(kcp, 1, 20, 2, 1) nodelay: 0:disable(default),
         * 1:enable interval: internal update timer interval in millisec, default is
         * 100ms resend: 0:disable fast resend(default), 1:enable fast resend nc:
         * 0:normal congestion control(default), 1:disable congestion control
         *
         * @param nodelay
         * @param interval
         * @param resend
         * @param nc
         */
        public void NoDelay(int nodelay, int interval, int resend, int nc)
        {
            this.nodelay = nodelay;
            this.interval = interval;
            this.resend = resend;
            this.nc = nc;
        }

        /**
         * set maximum window size: sndwnd=32, rcvwnd=32 by default
         *
         * @param sndwnd
         * @param rcvwnd
         */
        public void WndSize(int sndwnd, int rcvwnd)
        {
            this.sndwnd = sndwnd;
            this.rcvwnd = rcvwnd;
        }

        /**
         * change MTU size, default is 1400
         *
         * @param mtu
         */
        public void SetMtu(int mtu)
        {
            this.mtu = mtu;
        }
        public bool IsStream()
        {
            return this.kcp.IsStream();
        }

        public void SetStream(bool stream)
        {
            this.kcp.SetStream(stream);
        }

        public void SetMinRto(int min)
        {
            this.kcp.SetMinRto(min);
        }

        public void SetMaxRto(int maxMs)
        {
            this.kcp.SetMaxRto(maxMs);
        }

        /// <summary>与 kcp-go UDPSession.SetACKNoDelay 对齐。</summary>
        public void SetACKNoDelay(bool enable)
        {
            this.kcp.SetACKNoDelay(enable);
        }

        public void SetConv(int conv)
        {
            this.kcp.SetConv(conv);
        }
    }
}
