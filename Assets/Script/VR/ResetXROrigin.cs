using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Management;

/// <summary>
/// 挂在菜单场景的 XR Origin 上，每次场景加载时强制重置追踪原点位置和旋转，
/// 避免从战斗场景返回菜单时 XR 相机/手柄位置漂移。
/// </summary>
public class ResetXROrigin : MonoBehaviour
{
    [Header("参考：场景编辑器中 XR Origin 的初始位置 / 旋转")]
    [SerializeField] private Vector3 originPosition = new Vector3(33.882645f, 9.343474f, -23.471775f);
    [SerializeField] private Vector3 originEulerAngles = new Vector3(0f, -166.422f, 0f);

    private XROrigin xrOrigin;

    private void Awake()
    {
        xrOrigin = GetComponent<XROrigin>();
        if (xrOrigin == null)
            xrOrigin = GetComponentInChildren<XROrigin>();
    }

    private void Start()
    {
        // 1. 硬复位根位置 —— 确保 Origin 在场景预设点
        transform.SetPositionAndRotation(originPosition, Quaternion.Euler(originEulerAngles));

        // 2. 复位 Camera Offset（眼睛高度），防 XR 子系统地板校准污染
        if (xrOrigin != null && xrOrigin.CameraFloorOffsetObject != null)
        {
            var offsetTransform = xrOrigin.CameraFloorOffsetObject.transform;
            offsetTransform.localPosition = new Vector3(
                offsetTransform.localPosition.x,
                xrOrigin.CameraYOffset,
                offsetTransform.localPosition.z);
        }

        // 3. 尝试通过 XR Input Subsystem 回正追踪原点（效果因 SDK 而异）
        RecenterTracking();
    }

    private static void RecenterTracking()
    {
        var generalSettings = XRGeneralSettings.Instance;
        if (generalSettings == null) return;

        var manager = generalSettings.Manager;
        if (manager == null) return;

        var loader = manager.activeLoader;
        if (loader == null) return;

        var inputSubsystem = loader.GetLoadedSubsystem<XRInputSubsystem>();
        if (inputSubsystem != null)
        {
            // 先切到 Device 再切回 Floor，触发 PICO SDK 重新校准
            inputSubsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
            inputSubsystem.TryRecenter();
            inputSubsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
        }
    }

    /// <summary>
    /// 在 Editor 中点击可记录当前 XR Origin 位置到序列化字段
    /// </summary>
    [ContextMenu("记录当前位置")]
    private void RecordCurrentPosition()
    {
        originPosition = transform.position;
        originEulerAngles = transform.eulerAngles;
        Debug.Log($"[ResetXROrigin] 已记录位置: {originPosition}, 旋转: {originEulerAngles}");
    }
}
