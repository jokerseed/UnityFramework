namespace Framework.ECS
{
    /// <summary>实体销毁时卸下物理刚体。物理插件实现它，ECS 核心不引用具体引擎。</summary>
    public interface IEcsBodyRemover
    {
        /// <summary>卸下该实体的刚体。没有刚体时不做任何事。</summary>
        /// <param name="entityId">实体 ID。</param>
        void RemoveBody(uint entityId);
    }
}
