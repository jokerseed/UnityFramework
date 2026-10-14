var FrameworkWebSocket = {
  $fwws: {
    sockets: {},
    nextId: 1,
    onOpen: 0,
    onMessage: 0,
    onClose: 0,
    onError: 0
  },

  FwWsSetCallbacks: function (onOpen, onMessage, onClose, onError) {
    fwws.onOpen = onOpen;
    fwws.onMessage = onMessage;
    fwws.onClose = onClose;
    fwws.onError = onError;
  },

  FwWsConnect: function (urlPtr) {
    var id = fwws.nextId++;
    var ws = new WebSocket(UTF8ToString(urlPtr));
    ws.binaryType = 'arraybuffer';
    fwws.sockets[id] = ws;

    ws.onopen = function () {
      {{{ makeDynCall('vi', 'fwws.onOpen') }}}(id);
    };
    ws.onmessage = function (e) {
      if (!(e.data instanceof ArrayBuffer)) {
        // 协议只用二进制帧；文本帧说明两端协议不一致。
        {{{ makeDynCall('vi', 'fwws.onError') }}}(id);
        ws.close();
        return;
      }
      var bytes = new Uint8Array(e.data);
      var ptr = _malloc(bytes.length);
      HEAPU8.set(bytes, ptr);
      {{{ makeDynCall('viii', 'fwws.onMessage') }}}(id, ptr, bytes.length);
      _free(ptr);
    };
    ws.onerror = function () {
      {{{ makeDynCall('vi', 'fwws.onError') }}}(id);
    };
    ws.onclose = function (e) {
      delete fwws.sockets[id];
      {{{ makeDynCall('vii', 'fwws.onClose') }}}(id, e.code);
    };
    return id;
  },

  FwWsSend: function (id, dataPtr, length) {
    var ws = fwws.sockets[id];
    if (!ws || ws.readyState !== 1) {
      return 0;
    }
    ws.send(HEAPU8.slice(dataPtr, dataPtr + length));
    return 1;
  },

  FwWsClose: function (id) {
    var ws = fwws.sockets[id];
    if (!ws) {
      return;
    }
    delete fwws.sockets[id];
    ws.onopen = ws.onmessage = ws.onerror = ws.onclose = null;
    ws.close();
  }
};

autoAddDeps(FrameworkWebSocket, '$fwws');
mergeInto(LibraryManager.library, FrameworkWebSocket);
