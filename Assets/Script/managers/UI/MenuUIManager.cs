using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单 UI 管理——处理开始游戏、退出游戏等按钮逻辑，
/// 通过 SceneChanger 实现黑幕渐隐过渡切换场景。
/// 场景名称由 SceneChanger 统一管理，不在此重复定义。
/// </summary>
public class MenuUIManager : MonoBehaviour
{
    [SerializeField] private Button startBtn;
    [SerializeField] private Button quitBtn;

    private void Awake()
    {
        startBtn.onClick.AddListener(OnStartGame);
        quitBtn.onClick.AddListener(OnQuitGame);
    }

    /// <summary>开始游戏：通过 SceneChanger 加载战斗场景（带黑幕渐隐）。</summary>
    private void OnStartGame()
    {
        if (SceneChanger.Instance == null)
        {
            Debug.LogError("MenuUIManager: SceneChanger.Instance 为空，无法切换场景。");
            return;
        }

        SceneChanger.Instance.LoadBattleScene();
    }

    /// <summary>退出游戏。</summary>
    private void OnQuitGame()
    {
        if (SceneChanger.Instance == null)
        {
            Debug.LogError("MenuUIManager: SceneChanger.Instance 为空，无法退出游戏。");
            return;
        }

        SceneChanger.Instance.QuitGame();
    }
}
