namespace Framework.Bootstrap
{
    /// <summary>模块粗粒度启动阶段，用于同波次内的排序参考。</summary>
    public enum ModulePhase
    {
        /// <summary>基础设施层（资源、网络等）。</summary>
        Infrastructure = 0,

        /// <summary>数据层（配置表、存档等）。</summary>
        Data = 100,

        /// <summary>玩法层（战斗、关卡等）。</summary>
        Gameplay = 200,

        /// <summary>表现层（UI、音频等）。</summary>
        Presentation = 300,
    }
}
