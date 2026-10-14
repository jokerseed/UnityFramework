#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;

namespace Framework.Network
{
    /// <summary>WebGL 浏览器 WebSocket 传输（FrameworkWebSocket.jslib）；回调都在主线程。</summary>
    internal sealed class BrowserWebSocketTransport : IWebSocketTransport
    {
        delegate void OpenCallback(int id);
        delegate void MessageCallback(int id, IntPtr data, int length);
        delegate void CloseCallback(int id, int code);
        delegate void ErrorCallback(int id);

        static readonly Dictionary<int, BrowserWebSocketTransport> Active = new Dictionary<int, BrowserWebSocketTransport>();
        static bool _callbacksRegistered;

        readonly IWebSocketTransportListener _listener;
        int _id;
        bool _opened;
        bool _closed;

        public BrowserWebSocketTransport(IWebSocketTransportListener listener)
        {
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        }

        public void Connect(Uri uri)
        {
            if (!_callbacksRegistered)
            {
                FwWsSetCallbacks(OnOpen, OnMessage, OnClose, OnError);
                _callbacksRegistered = true;
            }

            _id = FwWsConnect(uri.AbsoluteUri);
            Active[_id] = this;
        }

        public void Send(byte[] data)
        {
            if (_closed)
            {
                return;
            }

            if (FwWsSend(_id, data, data.Length) == 0)
            {
                _listener.OnTransportError(this, NetworkErrorCode.SendError, "WebSocket is not open.");
                return;
            }

            _listener.OnTransportSent(this);
        }

        public void Close()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            Active.Remove(_id);
            FwWsClose(_id);
        }

        [MonoPInvokeCallback(typeof(OpenCallback))]
        static void OnOpen(int id)
        {
            if (Active.TryGetValue(id, out var transport))
            {
                transport._opened = true;
                transport._listener.OnTransportOpen(transport);
            }
        }

        [MonoPInvokeCallback(typeof(MessageCallback))]
        static void OnMessage(int id, IntPtr data, int length)
        {
            if (Active.TryGetValue(id, out var transport))
            {
                var buffer = new byte[length];
                Marshal.Copy(data, buffer, 0, length);
                transport._listener.OnTransportMessage(transport, buffer, length);
            }
        }

        [MonoPInvokeCallback(typeof(CloseCallback))]
        static void OnClose(int id, int code)
        {
            if (Active.TryGetValue(id, out var transport))
            {
                Active.Remove(id);
                transport._closed = true;
                transport._listener.OnTransportClosed(transport, $"closed (code {code})");
            }
        }

        [MonoPInvokeCallback(typeof(ErrorCallback))]
        static void OnError(int id)
        {
            if (Active.TryGetValue(id, out var transport))
            {
                var code = transport._opened ? NetworkErrorCode.ReceiveError : NetworkErrorCode.ConnectError;
                transport._listener.OnTransportError(transport, code, "Browser WebSocket error.");
            }
        }

        [DllImport("__Internal")]
        static extern void FwWsSetCallbacks(OpenCallback onOpen, MessageCallback onMessage, CloseCallback onClose, ErrorCallback onError);

        [DllImport("__Internal")]
        static extern int FwWsConnect(string url);

        [DllImport("__Internal")]
        static extern int FwWsSend(int id, byte[] data, int length);

        [DllImport("__Internal")]
        static extern void FwWsClose(int id);
    }
}
#endif
