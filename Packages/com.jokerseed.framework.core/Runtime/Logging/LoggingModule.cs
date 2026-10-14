using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Framework.Core;
using UnityEngine;

namespace Framework.Logging
{
    /// <summary>
    /// 日志模块：实现 <see cref="IGameModule"/>，初始化选项从 <see cref="LoggingManager"/> 读取。
    /// 按选项挂接滚动文件 Sink 与未处理异常捕获。
    /// </summary>
    public sealed class LoggingModule : IGameModule
    {
        FileLogSink _fileSink;
        bool _crashHooked;

        /// <summary>获取模块名称。</summary>
        public string Name => "Logging";

        /// <summary>获取模块所在启动阶段（基础设施层）。</summary>
        public ModulePhase Phase => ModulePhase.Infrastructure;

        /// <summary>获取模块依赖列表；日志模块无依赖项。</summary>
        public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

        /// <summary>获取初始化执行方式（同步）。</summary>
        public ModuleInitMode InitMode => ModuleInitMode.Synchronous;

        /// <summary>当前文件 Sink；未启用时为 null。</summary>
        public FileLogSink FileSink => _fileSink;

        /// <summary>同步初始化日志系统，从 <see cref="LoggingManager"/> 读取选项并调用 <see cref="GameLog.Configure"/>。</summary>
        public void Initialize()
        {
            var options = LoggingManager.Instance.Options;
            GameLog.Configure(options);

            if (options.FileEnabled && Application.platform != RuntimePlatform.WebGLPlayer)
            {
                var directory = string.IsNullOrWhiteSpace(options.FileDirectory)
                    ? Path.Combine(Application.persistentDataPath, "Logs")
                    : options.FileDirectory;
                _fileSink = new FileLogSink(
                    directory,
                    "game",
                    Math.Max(1, options.FileMaxKilobytes) * 1024L,
                    Math.Max(1, options.FileMaxCount));
                GameLog.AddSink(_fileSink);
            }

            if (options.CaptureUnhandledExceptions)
            {
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
                Application.logMessageReceivedThreaded += OnUnityLog;
                _crashHooked = true;
            }

            var file = _fileSink != null ? _fileSink.CurrentPath : "off";
            GameLog.Info(LogCategories.Bootstrap, $"Logging {LogStyle.Ok("ready")} (file={file})");
        }

        /// <summary>异步初始化协程（内部直接调用同步 <see cref="Initialize"/>）。</summary>
        /// <returns>协程枚举器。</returns>
        public IEnumerator InitializeAsync()
        {
            Initialize();
            yield break;
        }

        /// <summary>解除异常捕获、关闭文件 Sink 并调用 <see cref="GameLog.Shutdown"/>。</summary>
        public void Shutdown()
        {
            if (_crashHooked)
            {
                AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
                Application.logMessageReceivedThreaded -= OnUnityLog;
                _crashHooked = false;
            }

            GameLog.Shutdown();
            _fileSink?.Dispose();
            _fileSink = null;
        }

        static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                GameLog.Exception(LogCategories.Bootstrap, ex, "Unhandled exception");
            }
        }

        static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            GameLog.Exception(LogCategories.Bootstrap, e.Exception, "Unobserved task exception");
        }

        /// <summary>
        /// Unity 主线程里没被 try/catch 接住的异常只会走 Unity 日志回调，不经过 GameLog。
        /// 这里只转写 <see cref="LogType.Exception"/>，并且只写给文件 Sink，避免 Console 里重复一条。
        /// </summary>
        void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception || _fileSink == null)
            {
                return;
            }

            var entry = new LogEntry(LogLevel.Exception, LogCategories.Bootstrap, $"{condition}\n{stackTrace}", null, DateTime.UtcNow);
            _fileSink.Write(in entry);
        }
    }
}
