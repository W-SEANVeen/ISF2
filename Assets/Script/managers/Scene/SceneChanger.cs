using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 通用场景切换器——只负责带渐隐过渡的场景加载，不包含任何业务逻辑。
/// </summary>
public class SceneChanger : MonoBehaviour
{
    public static SceneChanger Instance { get; private set; }

    [Header("场景名称")]
    [SerializeField] private string menuSceneName = "GameMainMenu";
    [SerializeField] private string battleSceneName = "GameMainBattle";
    [SerializeField] private string gameEndSceneName = "GameEnd";

    [Header("场景切换渐隐")]
    [SerializeField] private float sceneFadeDuration = 1f;

    private CanvasGroup fadeCanvasGroup;
    private Coroutine transitionCoroutine;

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

    /// <summary>加载指定场景（带渐隐过渡）。</summary>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError($"SceneChanger: 场景名称为空，无法加载。");
            return;
        }
        StartTransition(sceneName);
    }

    /// <summary>直接加载场景，不播放任何过渡效果。</summary>
    public void LoadSceneDirect(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError($"SceneChanger: 场景名称为空，无法加载。");
            return;
        }

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
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
    // 便利方法（仅做名称映射，无业务逻辑）
    // ======================================================================

    public void LoadMenuScene()      => StartTransition(menuSceneName);
    public void LoadBattleScene()    => StartTransition(battleSceneName);
    public void LoadGameEndScene()   => StartTransition(gameEndSceneName);
    public void LoadMenuDirect()     => LoadSceneDirect(menuSceneName);
    public void LoadBattleDirect()   => LoadSceneDirect(battleSceneName);
    public void LoadGameEndDirect()  => LoadSceneDirect(gameEndSceneName);

    // ======================================================================
    // 渐隐过渡系统
    // ======================================================================

    private void StartTransition(string sceneName)
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(FadeAndLoadScene(sceneName));
    }

    private IEnumerator FadeAndLoadScene(string sceneName)
    {
        EnsureFadeCanvas();

        // 无渐隐时直接加载
        if (fadeCanvasGroup == null || sceneFadeDuration <= 0f)
        {
            SceneManager.LoadScene(sceneName);
            transitionCoroutine = null;
            yield break;
        }

        // 渐隐
        fadeCanvasGroup.blocksRaycasts = true;
        float elapsed = 0f;
        while (elapsed < sceneFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / sceneFadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 1f;

        SceneManager.LoadScene(sceneName);
        yield return null;

        // 淡入
        elapsed = 0f;
        while (elapsed < sceneFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / sceneFadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        transitionCoroutine = null;
    }

    private void EnsureFadeCanvas()
    {
        if (fadeCanvasGroup != null) return;

        var canvasObj = new GameObject("SceneFadeCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasObj);

        var canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var overlay = new GameObject("FadeOverlay",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        overlay.transform.SetParent(canvasObj.transform, false);

        var rt = overlay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var image = overlay.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;

        fadeCanvasGroup = overlay.GetComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    /// <summary>场景切换前恢复时间缩放，避免自定义时间系统影响过渡表现。</summary>
    // private void ResetTimeScale()
    // {
    //     if (TimeController.Instance != null)
    //     {
    //         TimeController.Instance.ResetForSceneTransition();
    //         return;
    //     }
    //     Time.timeScale = 1f;
    //     Time.fixedDeltaTime = 0.02f;
    // }
}
