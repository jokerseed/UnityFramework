namespace Framework.MemoryPool
{
    /// <summary>可被内存池复用的对象。</summary>
    public interface IMemory
    {
        /// <summary>归还池前清理内部状态。</summary>
        void Clear();
    }
}
