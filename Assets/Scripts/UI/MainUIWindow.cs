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
        GameSceneFlow.Instance.EnterBattle();
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
