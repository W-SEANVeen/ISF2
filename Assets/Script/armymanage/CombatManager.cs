using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 战斗全局管理器 —— 处理胜利/失败结算、黑屏文字与转场。
/// </summary>
public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    [Header("结算")]
    [Tooltip("黑屏停留时间（秒）")]
    public float deathScreenDuration = 3f;
    [Tooltip("是否自动切场景（关掉则黑屏后停在当前画面）")]
    public bool autoLoadScene = true;

    private bool isGameOver = false;
    private Canvas deathCanvas;
    private Text deathText;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ============================================================
    // 战斗结算
    // ============================================================

    public void WinCombat()
    {
        if (isGameOver) return;
        isGameOver = true;
        Debug.Log("[CombatManager] 死士死亡 —— 玩家胜利！");
        StartCoroutine(DeathScreenRoutine("你击败了敌人", true));
    }

    public void LoseCombat()
    {
        if (isGameOver) return;
        isGameOver = true;
        Debug.Log("[CombatManager] 玩家死亡 —— 战斗失败！");
        StartCoroutine(DeathScreenRoutine("敌人击败了你", false));
    }

    // ============================================================
    // 黑屏 + 文字
    // ============================================================

    IEnumerator DeathScreenRoutine(string message, bool isWin)
    {
        // 等一小会儿让死亡动画播一下
        yield return new WaitForSeconds(0.5f);

        // 创建黑幕 Canvas
        CreateDeathScreen(message, isWin);

        // 停留一段时间
        yield return new WaitForSeconds(deathScreenDuration);

        // 切场景或退出
        if (autoLoadScene && SceneChanger.Instance != null)
            SceneChanger.Instance.LoadGameEndScene();
    }

    void CreateDeathScreen(string message, bool isWin)
    {
        if (deathCanvas != null) return;

        // Canvas
        var go = new GameObject("DeathScreen");
        go.transform.SetParent(transform);
        deathCanvas = go.AddComponent<Canvas>();
        deathCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        deathCanvas.sortingOrder = 9999; // 最顶层

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        go.AddComponent<GraphicRaycaster>();

        // 黑底
        var bg = new GameObject("BlackBG");
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = Color.black;
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 文字
        var txtGo = new GameObject("MessageText");
        txtGo.transform.SetParent(go.transform, false);
        deathText = txtGo.AddComponent<Text>();
        deathText.text = message;
        deathText.fontSize = 72;
        deathText.alignment = TextAnchor.MiddleCenter;
        deathText.color = isWin ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.3f, 0.3f); // 金 / 血红
        deathText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        deathText.raycastTarget = false;

        var txtRect = deathText.GetComponent<RectTransform>();
        txtRect.anchorMin = new Vector2(0.5f, 0.5f);
        txtRect.anchorMax = new Vector2(0.5f, 0.5f);
        txtRect.sizeDelta = new Vector2(1200, 200);
        txtRect.anchoredPosition = Vector2.zero;
    }
}
