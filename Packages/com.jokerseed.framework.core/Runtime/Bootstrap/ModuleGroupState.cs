namespace Framework.Bootstrap
{
    /// <summary>模块组的初始化生命周期状态。</summary>
    public enum ModuleGroupState
    {
        /// <summary>已配置模块，尚未开始初始化。</summary>
        Idle,

        /// <summary>正在初始化。</summary>
        Running,

        /// <summary>全部模块初始化完成。</summary>
        Ready,

        /// <summary>初始化过程中发生错误。</summary>
        Failed,
    }
}
