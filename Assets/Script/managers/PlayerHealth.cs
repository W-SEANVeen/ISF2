using UnityEngine;
using System;
using System.Collections;
using UnityEngine.UI;


/// <summary>
/// 玩家受伤与死亡系统。
/// - 被箭击中 → 屏幕闪红。
/// - 被死士攻击 → 缴弩 + 拿刀 + 闪红 + 倒地 + 结算场景。
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance;

    [Header("中箭闪红")]
    [Tooltip("把你的红色半透明图片拖到这里，留空则用纯红色")]
    public Sprite damageFlashSprite;
    [Tooltip("闪红持续时间（秒）")]
    public float flashDuration = 0.25f;
    [Tooltip("闪红最大不透明度")]
    [Range(0f, 1f)]
    public float flashMaxAlpha = 0.5f;

    [Header("物体")]
    public StickyGrabInteractable crossbowGrab;
    public StickyGrabInteractable knifeGrab;
    [Tooltip("右手控制器的 XR 交互器（DirectInteractor / RayInteractor），用于强制抓起刀")]
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor rightHandInteractor;

    [Header("倒地")]
    [Tooltip("拖入场景中的 Main Camera")]
    public Camera mainCamera;
    [Tooltip("闪红后等待秒数再加载结算")]
    public float delayBeforeGameEnd = 1.5f;

    private Image flashImage;
    private CanvasGroup flashCanvasGroup;
    private Coroutine flashCoroutine;

    private EnemyAssault enemyAssault;

    // ======================================================================
    // 生命周期
    // ======================================================================

    void Awake()
    {
        if (Instance == null) Instance = this;
        EnsureDamageFlashCanvas();
    }

    void OnEnable()
    {
        EnemyAssault.onClimbEnd += OnAssassinClimbEnd;
    }

    void OnDisable()
    {
        EnemyAssault.onClimbEnd -= OnAssassinClimbEnd;
    }

    void Start()
    {
        enemyAssault = FindObjectOfType<EnemyAssault>();
        if (enemyAssault != null)
            enemyAssault.onPlayerHit.AddListener(OnAssassinHitPlayer);
        else
            Debug.LogError("PlayerHealth: 场景中找不到 EnemyAssault！");
    }

    void OnDestroy()
    {
        if (enemyAssault != null)
            enemyAssault.onPlayerHit.RemoveListener(OnAssassinHitPlayer);
    }

    // ======================================================================
    // 被箭矢击中
    // ======================================================================

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("EnemyArrow"))
        {
            FlashDamage();
            StickArrowInPlayer(other);
        }
    }

    /// <summary>让箭插在玩家身上（停物理 + 挂到玩家骨骼下）。</summary>
    void StickArrowInPlayer(Collider arrowCollider)
    {
        var rb = arrowCollider.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        arrowCollider.transform.SetParent(transform, true);

        var trail = arrowCollider.GetComponent<TrailRenderer>();
        if (trail != null) trail.enabled = false;

        Destroy(arrowCollider.gameObject, 5f);
    }

    // ======================================================================
    // 屏幕闪红
    // ======================================================================

    void EnsureDamageFlashCanvas()
    {
        if (flashImage != null) return;

        var canvasObj = new GameObject("DamageFlashCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasObj);

        var canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 1;

        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var overlay = new GameObject("DamageFlashOverlay",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        overlay.transform.SetParent(canvasObj.transform, false);

        var rt = overlay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        flashImage = overlay.GetComponent<Image>();
        flashImage.color = Color.red;
        flashImage.raycastTarget = false;

        flashCanvasGroup = overlay.GetComponent<CanvasGroup>();
        flashCanvasGroup.alpha = 0f;
        flashCanvasGroup.blocksRaycasts = false;
    }

    public void FlashDamage()
    {
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        if (damageFlashSprite != null)
            flashImage.sprite = damageFlashSprite;

        flashCanvasGroup.alpha = flashMaxAlpha;

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            flashCanvasGroup.alpha = Mathf.Lerp(flashMaxAlpha, 0f, elapsed / flashDuration);
            yield return null;
        }

        flashCanvasGroup.alpha = 0f;
    }

    // ======================================================================
    // 死士击杀序列
    // ======================================================================

    /// <summary>死士翻墙完成 → 缴弩 + 强制抓起刀</summary>
    void OnAssassinClimbEnd(object sender, EventArgs e)
    {
        Debug.Log("🗡️ [PlayerHealth] 死士翻墙完毕 → 缴弩、拿刀！");

        if (crossbowGrab != null)
        {
            crossbowGrab.ForceDrop();
            Debug.Log("   ✅ 弩已脱手");
        }

        if (knifeGrab != null && rightHandInteractor != null)
        {
            // 把刀挪到右手 attach 点，再通过 XR Interaction System 强制抓起
            var attach = rightHandInteractor.GetAttachTransform(knifeGrab);
            knifeGrab.transform.SetPositionAndRotation(attach.position, attach.rotation);

            // 用 XR Interaction Manager 执行 SelectEnter，走完整的抓取流程
            UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor sel = rightHandInteractor;
            UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable selGrab = knifeGrab;
            knifeGrab.interactionManager.SelectEnter(sel, selGrab);
            Debug.Log("   ✅ 刀已通过 XR 系统抓到右手");
        }
    }

    /// <summary>死士攻击命中 → 闪红 + 倒地 + 结算</summary>
    void OnAssassinHitPlayer()
    {
        Debug.Log("💀 [PlayerHealth] 死士击中玩家！");

        FlashDamage();
        knifeGrab?.ForceDrop();
        StartCoroutine(DelayedGameEnd());
    }

    IEnumerator DelayedGameEnd()
    {
        yield return new WaitForSeconds(delayBeforeGameEnd);

        if (SceneChanger.Instance != null)
            SceneChanger.Instance.LoadGameEndScene();
        else
            Debug.LogError("❌ SceneChanger.Instance 为空，无法加载结算场景！");
    }
}
