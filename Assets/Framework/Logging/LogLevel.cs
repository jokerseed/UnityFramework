namespace Framework.Logging
{
    /// <summary>日志级别，数值越大越严重。</summary>
    public enum LogLevel
    {
        /// <summary>追踪级，最详细的调试信息。</summary>
        Trace = 0,

        /// <summary>调试级。</summary>
        Debug = 1,

        /// <summary>信息级，常规运行日志。</summary>
        Info = 2,

        /// <summary>警告级。</summary>
        Warning = 3,

        /// <summary>错误级。</summary>
        Error = 4,

        /// <summary>异常级，附带 Exception。</summary>
        Exception = 5,
    }
}
