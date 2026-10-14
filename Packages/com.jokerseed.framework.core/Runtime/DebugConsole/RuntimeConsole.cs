using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Framework.Core;
using UnityEngine;

namespace Framework.DebugConsole
{
    /// <summary>
    /// 运行时调试控制台（IMGUI）：查看日志（按级别过滤、看堆栈）并执行 <see cref="Commands"/> 中注册的命令。
    /// 打开方式：左上角按钮，或旧输入系统下的 <c>`</c> 键。由 <see cref="DebugConsoleModule"/> 创建。
    /// </summary>
    public sealed class RuntimeConsole : PersistentSingleton<RuntimeConsole>
    {
        struct Entry
        {
            public LogType Type;
            public string Message;
            public string StackTrace;
            public string Time;
        }

        readonly ConcurrentQueue<Entry> _incoming = new ConcurrentQueue<Entry>();
        readonly List<Entry> _entries = new List<Entry>(256);
        readonly List<string> _history = new List<string>(32);
        int _capacity = 1000;
        bool _capturing;
        bool _showLog = true;
        bool _showWarning = true;
        bool _showError = true;
        int _selected = -1;
        int _historyIndex = -1;
        string _input = string.Empty;
        Vector2 _scroll;
        bool _stickToBottom = true;
        int _infoCount;
        int _warningCount;
        int _errorCount;

        /// <summary>命令表；业务在此注册自己的调试命令。</summary>
        public DebugCommandRegistry Commands { get; } = new DebugCommandRegistry();

        /// <summary>面板是否打开。</summary>
        public bool Visible { get; set; }

        /// <summary>是否显示左上角的开关按钮（触屏设备没有键盘时需要）。</summary>
        public bool ShowToggleButton { get; set; } = true;

        /// <summary>保留的日志条数上限；超出后丢弃最早的。</summary>
        public int Capacity
        {
            get => _capacity;
            set => _capacity = Mathf.Max(16, value);
        }

        /// <summary>开始收集日志并注册内置命令（help / clear）。</summary>
        public void StartCapture()
        {
            if (_capturing)
            {
                return;
            }

            _capturing = true;
            Application.logMessageReceivedThreaded += OnLogReceived;
            Commands.Register("help", "列出全部命令", _ => Commands.BuildHelp());
            Commands.Register("clear", "清空日志", _ =>
            {
                Clear();
                return null;
            });
        }

        /// <summary>停止收集日志。</summary>
        public void StopCapture()
        {
            if (!_capturing)
            {
                return;
            }

            _capturing = false;
            Application.logMessageReceivedThreaded -= OnLogReceived;
        }

        /// <summary>清空已收集的日志。</summary>
        public void Clear()
        {
            while (_incoming.TryDequeue(out _))
            {
            }

            _entries.Clear();
            _selected = -1;
            _infoCount = _warningCount = _errorCount = 0;
        }

        void OnLogReceived(string message, string stackTrace, LogType type)
        {
            _incoming.Enqueue(new Entry
            {
                Type = type,
                Message = message,
                StackTrace = stackTrace,
                Time = DateTime.Now.ToString("HH:mm:ss"),
            });
        }

        void Update()
        {
            while (_incoming.TryDequeue(out var entry))
            {
                if (_entries.Count >= _capacity)
                {
                    Count(_entries[0].Type, -1);
                    _entries.RemoveAt(0);
                    _selected = Mathf.Max(-1, _selected - 1);
                }

                _entries.Add(entry);
                Count(entry.Type, 1);
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                Visible = !Visible;
            }
#endif
        }

        void Count(LogType type, int delta)
        {
            switch (type)
            {
                case LogType.Warning:
                    _warningCount += delta;
                    break;
                case LogType.Log:
                    _infoCount += delta;
                    break;
                default:
                    _errorCount += delta;
                    break;
            }
        }

        void OnGUI()
        {
            var scale = Mathf.Max(1f, Screen.height / 720f);
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;

            if (ShowToggleButton && GUI.Button(new Rect(4, 4, 84, 28), Visible ? "Close" : $"Console {(_errorCount > 0 ? "!" : string.Empty)}"))
            {
                Visible = !Visible;
            }

            if (Visible)
            {
                GUILayout.BeginArea(new Rect(4, 36, width - 8, height * 0.7f), GUI.skin.box);
                DrawToolbar();
                DrawEntries();
                DrawDetail();
                DrawInput();
                GUILayout.EndArea();
            }

            GUI.matrix = matrix;
        }

        void DrawToolbar()
        {
            GUILayout.BeginHorizontal();
            _showLog = GUILayout.Toggle(_showLog, $"Log {_infoCount}", GUILayout.Width(90));
            _showWarning = GUILayout.Toggle(_showWarning, $"Warn {_warningCount}", GUILayout.Width(90));
            _showError = GUILayout.Toggle(_showError, $"Error {_errorCount}", GUILayout.Width(90));
            _stickToBottom = GUILayout.Toggle(_stickToBottom, "Follow", GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", GUILayout.Width(70)))
            {
                Clear();
            }

            GUILayout.EndHorizontal();
        }

        void DrawEntries()
        {
            var style = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false };
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!IsShown(entry.Type))
                {
                    continue;
                }

                var color = entry.Type == LogType.Log ? "#DDDDDD" : entry.Type == LogType.Warning ? "#FFD54F" : "#EF5350";
                var firstLine = FirstLine(entry.Message);
                var label = i == _selected ? $"<b>▶ {entry.Time} {firstLine}</b>" : $"<color={color}>{entry.Time}</color> {firstLine}";
                if (GUILayout.Button(label, style))
                {
                    _selected = i == _selected ? -1 : i;
                    _stickToBottom = false;
                }
            }

            if (_stickToBottom && Event.current.type == EventType.Repaint)
            {
                _scroll.y = float.MaxValue;
            }

            GUILayout.EndScrollView();
        }

        void DrawDetail()
        {
            if (_selected < 0 || _selected >= _entries.Count)
            {
                return;
            }

            var entry = _entries[_selected];
            var style = new GUIStyle(GUI.skin.textArea) { richText = true, wordWrap = true };
            GUILayout.Label($"{entry.Message}\n{entry.StackTrace}", style, GUILayout.MaxHeight(160));
        }

        void DrawInput()
        {
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("DebugConsoleInput");
            var e = Event.current;
            if (e.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == "DebugConsoleInput")
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    Submit();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.UpArrow)
                {
                    BrowseHistory(-1);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.DownArrow)
                {
                    BrowseHistory(1);
                    e.Use();
                }
            }

            _input = GUILayout.TextField(_input, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Run", GUILayout.Width(60)))
            {
                Submit();
            }

            GUILayout.EndHorizontal();
        }

        void Submit()
        {
            var line = _input.Trim();
            _input = string.Empty;
            if (line.Length == 0)
            {
                return;
            }

            _history.Add(line);
            _historyIndex = _history.Count;
            Debug.Log($"> {line}");
            try
            {
                var output = Commands.Execute(line);
                if (!string.IsNullOrEmpty(output))
                {
                    Debug.Log(output);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex is KeyNotFoundException ? ex.Message : ex.ToString());
            }

            _stickToBottom = true;
        }

        void BrowseHistory(int step)
        {
            if (_history.Count == 0)
            {
                return;
            }

            _historyIndex = Mathf.Clamp(_historyIndex + step, 0, _history.Count);
            _input = _historyIndex < _history.Count ? _history[_historyIndex] : string.Empty;
        }

        bool IsShown(LogType type)
        {
            switch (type)
            {
                case LogType.Log:
                    return _showLog;
                case LogType.Warning:
                    return _showWarning;
                default:
                    return _showError;
            }
        }

        static string FirstLine(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            var index = message.IndexOf('\n');
            return index < 0 ? message : message.Substring(0, index);
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            StopCapture();
            base.OnDestroy();
        }
    }
}
