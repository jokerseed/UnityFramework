using System.Collections;
using Framework.Coroutine;
using Framework.Logging;
using Framework.Res;
using Framework.UI;
using UnityEngine.UI;

namespace Game
{
/// <summary>
/// 首页窗口：展示「进入游戏」按钮，点击后关闭首页并加载战斗场景。
/// </summary>
[UIWindow(
    UILayer.UI,
    fullScreen: true,
    location: ResourceAddresses.MainPrefab,
    releasePolicy: UIReleasePolicy.HideAndDelayUnload,
    delayUnloadSeconds: 60f)]
public sealed class MainUIWindow : UIWindow
{
    Button _enterGameButton;

    /// <inheritdoc/>
    public override void ScriptGenerator()
    {
        _enterGameButton = FindChildComponent<Button>("EnterGameButton");
        if (_enterGameButton != null)
        {
            _enterGameButton.onClick.AddListener(OnEnterGameClicked);
        }
    }

    void OnEnterGameClicked()
    {
        GameLog.Info(LogCategories.Launch, "Enter game clicked");
        Manager.Close<MainUIWindow>();
        // 窗口关闭后实例会销毁，场景加载须走全局协程。
        GameCoroutine.StartGlobal(LoadBattleScene());
    }

    static IEnumerator LoadBattleScene()
    {
        GameLog.Info(LogCategories.Launch, $"Loading scene {LogStyle.Name("Battle")}");
        yield return ResourceManager.Instance.LoadMainSceneAsync(ResourceAddresses.BattleScene);
        GameLog.Info(LogCategories.Launch, $"Scene {LogStyle.Name("Battle")} {LogStyle.Ok("opened")}");
    }

    /// <inheritdoc/>
    protected override void OnDestroy()
    {
        if (_enterGameButton != null)
        {
            _enterGameButton.onClick.RemoveListener(OnEnterGameClicked);
        }
    }
}
}
