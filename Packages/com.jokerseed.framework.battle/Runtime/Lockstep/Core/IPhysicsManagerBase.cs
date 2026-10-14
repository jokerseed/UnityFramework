namespace Framework.Lockstep
{
    /// <summary>物理世界管理基接口，供锁步核心推进与校验。</summary>
    public interface IPhysicsManagerBase
    {
        /// <summary>初始化物理世界。</summary>
        void Init();

        /// <summary>推进一个锁定时间步。</summary>
        void UpdateStep();

        /// <summary>获取当前物理世界。</summary>
        /// <returns>世界接口。</returns>
        IWorld GetWorld();

        /// <summary>获取用于回滚的世界克隆器。</summary>
        /// <returns>世界克隆接口。</returns>
        IWorldClone GetWorldClone();

        /// <summary>从世界移除刚体。</summary>
        /// <param name="iBody">刚体。</param>
        void RemoveBody(IBody iBody);
    }
}
