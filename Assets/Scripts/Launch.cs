using Framework.Bootstrap;
using Framework.Coroutine;
using Framework.Logging;
using Framework.MemoryPool;
using Framework.ObjectPool;
using Framework.Res;
using UnityEngine;

/// <summary>
/// 启动入口：通过 GameBootstrap 按依赖顺序初始化各模块。
/// 挂到 Launch 场景任意 GameObject 上即可。
/// 各模块 Inspector 选项在对应 <c>PersistentSingleton</c> Manager 上配置。
/// </summary>
public sealed class Launch : MonoBehaviour
{
    void Awake()
    {
        var bootstrap = GameBootstrap.Instance;

        var launch = bootstrap.SetModules("launch", new IGameModule[]
        {
            new LoggingModule(),
            new CoroutineModule(),
            new MemoryPoolModule(),
            new ObjectPoolModule(),
            new ResourceModule(),
        });
        launch.StateChanged += OnGroupStateChanged;
        launch.Ready += OnGroupReady;
        launch.Failed += OnGroupFailed;
        launch.ProgressChanged += (group, current, total) =>
            GameLog.Debug(LogCategories.Launch, $"{LogStyle.Name(group.Name)} progress {LogStyle.Value($"{current}/{total}")}");
    }

    void Start()
    {
        GameBootstrap.Instance.Run("launch");
        GameLog.Info(LogCategories.Launch, $"Run {LogStyle.Value("requested")}");
    }

    static void OnGroupStateChanged(ModuleGroup group)
    {
        GameLog.Info(LogCategories.Launch, $"Group {LogStyle.Name(group.Name)} state → {FormatState(group.State)}");
    }

    static void OnGroupReady(ModuleGroup group)
    {
        GameLog.Info(LogCategories.Launch, $"Group {LogStyle.Name(group.Name)} {LogStyle.Ok("ready")}");
    }

    static void OnGroupFailed(ModuleGroup group, System.Exception error)
    {
        GameLog.Error(LogCategories.Launch, $"Group {LogStyle.Name(group.Name)} {LogStyle.Fail("failed")}: {error.Message}");
    }

    static string FormatState(ModuleGroupState state)
    {
        switch (state)
        {
            case ModuleGroupState.Ready:
                return LogStyle.Ok(state);
            case ModuleGroupState.Failed:
                return LogStyle.Fail(state);
            case ModuleGroupState.Running:
                return LogStyle.Value(state);
            default:
                return LogStyle.Muted(state);
        }
    }
}
