using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


/// <summary>
/// XR UI 总控制器 —— 监听 XRI Input Actions 中的 Menu 键（PICO 手柄菜单按钮），
/// 控制暂停面板显隐、时间缩放，以及"继续 / 返回主菜单"按钮逻辑。
/// 按钮通过序列化字段在 Inspector 中拖拽绑定。
///
/// simulatorMenuAction 可选：在 Editor 中拖入 XR Device Controller Controls 的 Menu 动作，
/// 使 XR Device Simulator 的菜单键也能触发暂停，方便调试。
/// </summary>
public class XRUIManager : MonoBehaviour
{
    [Header("输入")]
    [SerializeField] private InputActionReference menuAction;
    [SerializeField] private InputActionReference simulatorMenuAction;

    [Header("暂停面板")]
    [SerializeField] private GameObject pausePanel;

    [Header("Ray Interactor（暂停时启用，恢复时禁用）")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor leftRayInteractor;
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rightRayInteractor;

    [Header("弩（暂停时隐藏，恢复时显示）")]
    [SerializeField] private GameObject crossbow;

    [Header("按钮")]
    [SerializeField] private Button continueBtn;
    [SerializeField] private Button quitToMenuBtn;

    [Header("摄像头距离")]
    [SerializeField] private float canvasDistance = 1f;

    private bool isPaused;

    // ======================================================================
    // 生命周期
    // ======================================================================

    private void Awake()
    {
        if (continueBtn != null)
            continueBtn.onClick.AddListener(OnResumeButton);

        if (quitToMenuBtn != null)
            quitToMenuBtn.onClick.AddListener(OnQuitToMenuButton);
    }

    private void OnEnable()
    {
        if (menuAction == null)
        {
            Debug.LogError("XRUIManager: menuAction（InputActionReference）未赋值！");
            enabled = false;
            return;
        }

        menuAction.action.performed += OnMenuPressed;
        menuAction.action.Enable();

        if (simulatorMenuAction != null)
        {
            simulatorMenuAction.action.performed += OnMenuPressed;
            simulatorMenuAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (menuAction != null)
            menuAction.action.performed -= OnMenuPressed;

        if (simulatorMenuAction != null)
            simulatorMenuAction.action.performed -= OnMenuPressed;
    }

    private void Start()
    {
        // 确保初始状态：面板隐藏，时间正常
        if (pausePanel != null)
            pausePanel.SetActive(false);

        Time.timeScale = 1f;
        isPaused = false;
    }

    // ======================================================================
    // 菜单键响应
    // ======================================================================

    private void OnMenuPressed(InputAction.CallbackContext context)
    {
        TogglePause();
    }

    // ======================================================================
    // 暂停 / 恢复
    // ======================================================================

    public void TogglePause()
    {
        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        if (pausePanel == null)
        {
            Debug.LogWarning("XRUIManager: pausePanel 未赋值，无法暂停。");
            return;
        }

        isPaused = true;
        pausePanel.SetActive(true);
        Time.timeScale = 0f; // 暂停游戏时间

        // 把 Canvas 整体移到视野前方（Canvas 本身挂载此脚本）
        var cam = Camera.main.transform;
        transform.SetPositionAndRotation(
            cam.position + cam.forward * canvasDistance,
            Quaternion.LookRotation(cam.forward));
        // PausePanel 作为子物体保持本地坐标不变
        pausePanel.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        // 注意：不要隐藏弩/刀等已抓取物体，否则 StickyGrab 会松开导致掉落

        // 启用两只手的 Ray Interactor，用于点击暂停 UI
        if (leftRayInteractor != null)  leftRayInteractor.gameObject.SetActive(true);
        if (rightRayInteractor != null) rightRayInteractor.gameObject.SetActive(true);

        Debug.Log("[XRUIManager] 游戏已暂停");
    }

    public void ResumeGame()
    {
        if (pausePanel == null) return;

        isPaused = false;
        pausePanel.SetActive(false);
        Time.timeScale = 1f; // 恢复游戏时间

        // 注意：不需要重新显示弩，暂停时未隐藏它

        // 关闭两只手的 Ray Interactor，回到正常 gameplay
        if (leftRayInteractor != null)  leftRayInteractor.gameObject.SetActive(false);
        if (rightRayInteractor != null) rightRayInteractor.gameObject.SetActive(false);

        Debug.Log("[XRUIManager] 游戏已恢复");
    }

    // ======================================================================
    // PausePanel 按钮回调（在 Inspector 中绑定）
    // ======================================================================

    /// <summary>继续游戏</summary>
    public void OnResumeButton() => ResumeGame();

    /// <summary>返回主菜单</summary>
    public void OnQuitToMenuButton()
    {
        ResumeGame(); // 先恢复时间

        if (SceneChanger.Instance != null)
            SceneChanger.Instance.LoadMenuScene();
        else
        {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        }
    }
}
