using System.Collections;
using System.Text;
using cfg;
using Framework.Bootstrap;
using Framework.Config;
using Framework.Res;
using UnityEngine;

/// <summary>
/// 启动入口：通过 GameBootstrap 按依赖顺序初始化各模块，加载配置后输出到 Console。
/// 挂到 Launch 场景任意 GameObject 上即可。
/// </summary>
public sealed class Launch : MonoBehaviour
{
    [SerializeField] ResourceInitOptions _resourceOptions = new ResourceInitOptions();

    void Awake()
    {
        var bootstrap = GameBootstrap.Instance;

        var launch = bootstrap.SetModules("launch", new IGameModule[]
        {
            new ResourceModule(_resourceOptions),
            new ConfigModule(),
        });

        launch.StateChanged += OnGroupStateChanged;
        launch.Ready += OnGroupReady;
        launch.Failed += OnGroupFailed;
        launch.ProgressChanged += (group, current, total) =>
            Debug.Log($"[Launch] {group.Name} progress {current}/{total}");
    }

    IEnumerator Start()
    {
        yield return GameBootstrap.Instance.RunAsync("launch");

        var tables = BattleConfigBootstrap.Tables;
        Debug.Log("[Launch] Bootstrap ready, printing config test...");
        PrintConfigTest(tables);
    }

    static void OnGroupStateChanged(ModuleGroup group)
    {
        Debug.Log($"[Launch] Group '{group.Name}' state -> {group.State}");
    }

    static void OnGroupReady(ModuleGroup group)
    {
        Debug.Log($"[Launch] Group '{group.Name}' ready.");
    }

    static void OnGroupFailed(ModuleGroup group, System.Exception error)
    {
        Debug.LogError($"[Launch] Group '{group.Name}' failed: {error.Message}");
    }

    static void PrintConfigTest(Tables tables)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Launch] ===== Config Load Test =====");

        sb.AppendLine($"Ability count: {tables.TbAbility.DataList.Count}");
        foreach (var ability in tables.TbAbility.DataList)
        {
            sb.AppendLine(
                $"  - {ability.Id}: type={ability.Type}, cd={ability.Cooldown}, dmg={ability.Damage}, cue={ability.CueTag}");
        }

        sb.AppendLine($"Effect count: {tables.TbEffect.DataList.Count}");
        foreach (var effect in tables.TbEffect.DataList)
        {
            sb.AppendLine(
                $"  - {effect.Id}: durationType={effect.DurationType}, duration={effect.Duration}, mod={effect.ModAttribute}+{effect.ModMagnitude}");
        }

        sb.AppendLine("[Launch] ===== Done =====");
        Debug.Log(sb.ToString());
    }
}
