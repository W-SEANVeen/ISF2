using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 通用场景切换器——直接切换场景，不带渐隐过渡（渐隐改用 PXR_ScreenFade）。
/// </summary>
public class SceneChanger : MonoBehaviour
{
    public static SceneChanger Instance { get; private set; }

    [Header("场景名称")]
    [SerializeField] private string menuSceneName = "GameMainMenu";
    [SerializeField] private string battleSceneName = "GameMainBattle";
    [SerializeField] private string gameEndSceneName = "GameEnd";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ======================================================================
    // 公开 API
    // ======================================================================

    /// <summary>加载指定场景（等同 LoadSceneDirect，保留兼容性）。</summary>
    public void LoadScene(string sceneName)
    {
        LoadSceneDirect(sceneName);
    }

    /// <summary>直接加载场景，无过渡效果。</summary>
    public void LoadSceneDirect(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError($"SceneChanger: 场景名称为空，无法加载。");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>退出游戏。</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ======================================================================
    // 便利方法（全部直接切场景）
    // ======================================================================

    public void LoadMenuScene()      => LoadSceneDirect(menuSceneName);
    public void LoadBattleScene()    => LoadSceneDirect(battleSceneName);
    public void LoadGameEndScene()   => LoadSceneDirect(gameEndSceneName);
    public void LoadMenuDirect()     => LoadSceneDirect(menuSceneName);
    public void LoadBattleDirect()   => LoadSceneDirect(battleSceneName);
    public void LoadGameEndDirect()  => LoadSceneDirect(gameEndSceneName);
}
