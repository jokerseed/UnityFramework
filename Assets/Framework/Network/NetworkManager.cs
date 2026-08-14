using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;
using UnityEngine;

namespace Framework.Network
{
    /// <summary>
    /// 网络管理器：创建/销毁命名频道，每帧驱动收包分发与心跳。
    /// 参考 TEngine / GameFramework 的 NetworkModule 频道模型。
    /// </summary>
    public sealed class NetworkManager : PersistentSingleton<NetworkManager>
    {
        readonly Dictionary<string, TcpNetworkChannel> _channels =
            new Dictionary<string, TcpNetworkChannel>(StringComparer.Ordinal);
        readonly List<TcpNetworkChannel> _tickScratch = new List<TcpNetworkChannel>(8);

        /// <summary>当前频道数量。</summary>
        public int ChannelCount => _channels.Count;

        void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>驱动全部频道的主线程分发与心跳。</summary>
        /// <param name="elapseSeconds">真实流逝时间（秒）。</param>
        public void Tick(float elapseSeconds)
        {
            _tickScratch.Clear();
            foreach (var pair in _channels)
            {
                _tickScratch.Add(pair.Value);
            }

            for (var i = 0; i < _tickScratch.Count; i++)
            {
                _tickScratch[i].Update(elapseSeconds);
            }
        }

        /// <summary>是否存在指定名称的频道。</summary>
        /// <param name="name">频道名称；为 null 时按空字符串查找。</param>
        /// <returns>存在返回 true。</returns>
        public bool HasChannel(string name)
        {
            return _channels.ContainsKey(name ?? string.Empty);
        }

        /// <summary>获取指定名称的频道。</summary>
        /// <param name="name">频道名称；为 null 时按空字符串查找。</param>
        /// <returns>找到则返回频道，否则返回 null。</returns>
        public INetworkChannel GetChannel(string name)
        {
            return _channels.TryGetValue(name ?? string.Empty, out var channel) ? channel : null;
        }

        /// <summary>使用默认二进制协议创建 TCP 频道。</summary>
        /// <param name="name">频道名称；为 null 时使用空字符串。</param>
        /// <returns>新建频道。</returns>
        /// <exception cref="InvalidOperationException">同名频道已存在。</exception>
        public INetworkChannel CreateTcpChannel(string name = "Default")
        {
            return CreateChannel(name, NetworkServiceType.Tcp, new DefaultNetworkChannelHelper());
        }

        /// <summary>创建网络频道。</summary>
        /// <param name="name">频道名称；为 null 时使用空字符串。</param>
        /// <param name="serviceType">服务类型。</param>
        /// <param name="helper">协议辅助器；为 null 时使用 <see cref="DefaultNetworkChannelHelper"/>。</param>
        /// <returns>新建频道。</returns>
        /// <exception cref="NotSupportedException">不支持的服务类型。</exception>
        /// <exception cref="InvalidOperationException">同名频道已存在。</exception>
        public INetworkChannel CreateChannel(
            string name,
            NetworkServiceType serviceType,
            INetworkChannelHelper helper = null)
        {
            name = name ?? string.Empty;
            if (_channels.ContainsKey(name))
            {
                throw new InvalidOperationException($"Network channel already exists: {name}");
            }

            helper = helper ?? new DefaultNetworkChannelHelper();
            TcpNetworkChannel channel;
            switch (serviceType)
            {
                case NetworkServiceType.Tcp:
                    channel = new TcpNetworkChannel(name, helper);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported network service type: {serviceType}");
            }

            _channels.Add(name, channel);
            GameLog.Info(LogCategories.Network, $"Created channel {LogStyle.Name(name)} type={LogStyle.Value(serviceType)}");
            return channel;
        }

        /// <summary>销毁指定频道。</summary>
        /// <param name="name">频道名称；为 null 时按空字符串查找。</param>
        /// <returns>成功销毁返回 true；不存在返回 false。</returns>
        public bool DestroyChannel(string name)
        {
            name = name ?? string.Empty;
            if (!_channels.TryGetValue(name, out var channel))
            {
                return false;
            }

            channel.Shutdown();
            _channels.Remove(name);
            GameLog.Info(LogCategories.Network, $"Destroyed channel {LogStyle.Name(name)}");
            return true;
        }

        /// <summary>关闭并销毁全部频道。</summary>
        public void DestroyAllChannels()
        {
            foreach (var pair in _channels)
            {
                pair.Value.Shutdown();
            }

            _channels.Clear();
        }

        /// <summary>关闭全部频道。</summary>
        public void Shutdown()
        {
            DestroyAllChannels();
            GameLog.Info(LogCategories.Network, LogStyle.Muted("shut down"));
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            Shutdown();
            base.OnDestroy();
        }
    }
}
