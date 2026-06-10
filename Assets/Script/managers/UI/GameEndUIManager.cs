using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏结束 UI 管理——退出按钮返回主菜单，Content 文字自动从顶部滚到底部后开启 ScrollView 交互。
/// </summary>
public class GameEndUIManager : MonoBehaviour
{
    [Header("按钮")]
    [SerializeField] private Button exitBtn;

    [Header("滚动视图")]
    [SerializeField] private ScrollRect scrollView;
    [SerializeField] private RectTransform content;          // 滚动内容
    [SerializeField] private Graphic scrollViewGraphic;      // 控制 raycastTarget（ScrollView 上的 Image）

    [Header("滚动参数")]
    [SerializeField] private float scrollSpeed = 0.15f;
    [SerializeField] private float startDelay = 1f;

    private Coroutine autoScrollCoroutine;

    // ======================================================================
    // 生命周期
    // ======================================================================

    private void Awake()
    {
        if (exitBtn != null)
            exitBtn.onClick.AddListener(OnExitButton);

        // 滚动完成前禁止交互，防止误触
        if (scrollViewGraphic != null)
            scrollViewGraphic.raycastTarget = false;
    }

    private void Start()
    {
        if (scrollView != null)
            autoScrollCoroutine = StartCoroutine(AutoScrollRoutine());
        else
            Debug.LogWarning("GameEndUIManager: scrollView 未赋值。");
    }

    private void OnDestroy()
    {
        if (autoScrollCoroutine != null)
            StopCoroutine(autoScrollCoroutine);
    }

    // ======================================================================
    // 按钮回调
    // ======================================================================

    /// <summary>退出按钮：返回主菜单（GameStart）。</summary>
    private void OnExitButton()
    {
        if (SceneChanger.Instance != null)
        {
            SceneChanger.Instance.LoadMenuScene();
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    // ======================================================================
    // 自动滚动
    // ======================================================================

    /// <summary>自动滚动协程：从顶部（1）滚到底部（0），完成时启用 raycastTarget。</summary>
    private IEnumerator AutoScrollRoutine()
    {
        // 延迟
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        // 强制从顶部开始
        scrollView.verticalNormalizedPosition = 1f;

        // 等待一帧让 Canvas 布局与 ContentSizeFitter 完成刷新
        yield return null;

        // 持续滚动直到内容完全显示到底部
        while (scrollView.verticalNormalizedPosition > 0.001f)
        {
            scrollView.verticalNormalizedPosition -= scrollSpeed * Time.deltaTime;
            yield return null;
        }

        // 精确归零
        scrollView.verticalNormalizedPosition = 0f;

        // 再次等待一帧让最终布局稳定
        yield return null;

        // ---------- 滚动完成，开启交互 ----------
        if (scrollViewGraphic != null)
            scrollViewGraphic.raycastTarget = true;

        Debug.Log("[GameEndUIManager] 滚动完成，已启用 ScrollView 交互。");
    }
}
