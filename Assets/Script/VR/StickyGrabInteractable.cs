using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// 粘性抓取 Interactable —— 用于十字弓等武器。
///
/// 特性：
/// - 抓取后不会因为松开 grip 键而掉落，一直粘在手上
/// - 抓取时自动将物体设为手柄的子级（跟随更紧密、不抖动）
/// - 抓取时自动关闭 Rigidbody 物理（防抖动）
/// - 提供 ForceDrop() 方法供外部调用主动释放（例如扔下武器）
///
/// 原理：
///   OnSelectExited 触发时（松开 grip）不调用 base，而是通过
///   interactionManager.SelectEnter 在下一帧重新选中，从而实现「松不开」的效果。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class StickyGrabInteractable : UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable
{
    [Header("粘性抓取")]
    [Tooltip("开启 = grip 键无法松开；关闭 = 退化为普通 XRGrabInteractable")]
    [SerializeField] private bool sticky = true;

    private UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor m_CurrentInteractor;
    private Rigidbody m_StickyRigidbody;

    // ======================================================================
    // 生命周期
    // ======================================================================

    protected override void Awake()
    {
        base.Awake();
        m_StickyRigidbody = GetComponent<Rigidbody>();
    }

    // ======================================================================
    // 选中事件
    // ======================================================================

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        m_CurrentInteractor = args.interactorObject;

        if (!sticky) return;

        // ── 把手柄作为父级 ──────────────────────────────────────────────
        // 这样即使 XRITK 的 position/rotation 跟踪偶尔延迟，
        // 物体也会因为 Transform 层级关系牢牢跟手
        var attach = args.interactorObject.GetAttachTransform(this);
        transform.SetParent(attach, true);

        // ── 禁用物理，避免 Rigidbody 跟 Transform 打架 ────────────────
        if (m_StickyRigidbody != null)
        {
            m_StickyRigidbody.isKinematic = true;
            m_StickyRigidbody.useGravity = false;
        }
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        if (!sticky)
        {
            base.OnSelectExited(args);
            return;
        }

        // 如果物体已 inactive（例如 OnDisable 中调用本方法），
        // 无法启动协程，也无意义再粘住 —— 正常释放
        if (!gameObject.activeInHierarchy)
        {
            // 场景切换/禁用时保持 kinematic，防止物理重新启用导致物体掉落
            if (m_StickyRigidbody != null)
            {
                m_StickyRigidbody.isKinematic = true;
                m_StickyRigidbody.useGravity = false;
            }
            base.OnSelectExited(args);
            return;
        }

        // ── 粘性模式：拦截释放 ──────────────────────────────────────────
        // 不调用 base.OnSelectExited，让物体保持 held 状态
        // 用协程在下一帧重新选中，避免可能的递归调用
        StartCoroutine(ReGrabCoroutine());
    }


    // ======================================================================
    // 主动释放
    // ======================================================================

    /// <summary>
    /// 主动释放十字弓（例如：玩家通过 UI 或特定操作放下武器时调用）
    /// </summary>
    public void ForceDrop()
    {
        if (!isSelected) return;

        sticky = false; // 临时关闭粘性，允许正常释放

        // 恢复物理
        if (m_StickyRigidbody != null)
        {
            m_StickyRigidbody.isKinematic = false;
            m_StickyRigidbody.useGravity = true;
        }

        // 解除父级
        transform.SetParent(null, true);

        // 通知 Interaction Manager 释放
        if (m_CurrentInteractor != null && m_CurrentInteractor is UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor)
        {
            interactionManager.SelectExit(interactor, this);
        }

        m_CurrentInteractor = null;
    }

    // ======================================================================
    // 内部
    // ======================================================================

    private System.Collections.IEnumerator ReGrabCoroutine()
    {
        yield return null; // 等一帧，确保 OnSelectExited 完成

        if (m_CurrentInteractor != null
            && m_CurrentInteractor is UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor
            && !isSelected)
        {
            interactionManager.SelectEnter(interactor, this);
        }
    }
}
