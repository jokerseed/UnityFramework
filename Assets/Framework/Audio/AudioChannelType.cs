namespace Framework.Audio
{
    /// <summary>音频通道类型。</summary>
    public enum AudioChannelType
    {
        /// <summary>背景音乐通道（单轨，支持交叉淡入淡出）。</summary>
        Bgm = 0,

        /// <summary>音效通道（多轨，对象池播放）。</summary>
        Sfx = 1,
    }
}
