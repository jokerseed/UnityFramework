namespace Framework.Fsm
{
    /// <summary>
    /// 流程基类：游戏生命周期的一个阶段（检查更新、预加载、登录、主城、战斗……）。
    /// 由 <see cref="ProcedureManager"/> 持有的状态机驱动，同一时刻只有一个流程处于激活状态。
    /// </summary>
    public abstract class ProcedureBase : FsmState<ProcedureManager>
    {
    }
}
