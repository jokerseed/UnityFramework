using Framework.Logging;
using Framework.Res;
using Framework.UI;
using UnityEngine.UI;

/// <summary>
/// 首页窗口：展示「进入游戏」按钮，点击后关闭首页并启动战斗演示。
/// </summary>
[UIWindow(UILayer.UI, fullScreen: true, location: ResourceAddresses.MainPrefab)]
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
