using UnityEngine;

/// <summary>
/// VR 中跟随头显视角的 UI 面板
/// 挂到 World Space Canvas 上，自动跟随主 Camera 位置与旋转
/// </summary>
public class VRFaceHUD : MonoBehaviour
{
    [Header("位置偏移（相对 Camera 前方）")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0, -0.2f, 1.5f);

    [Header("平滑参数")]
    [SerializeField] private bool smoothFollow = true;
    [SerializeField] private float positionSmoothSpeed = 5f;
    [SerializeField] private float rotationSmoothSpeed = 8f;

    [Header("旋转模式")]
    [SerializeField] private RotationMode rotationMode = RotationMode.FullFollow;
    private enum RotationMode
    {
        FullFollow,    // 完全跟随 Camera 朝向（转头时 UI 跟着转）
        HorizontalOnly // 只跟随水平旋转，不跟随抬头/低头
    }

    [Header("自动隐藏")]
    [SerializeField] private bool enableAutoHide;
    [SerializeField] private float hideAngle = 30f;  // 低头超过此角度隐藏
    [SerializeField] private float fadeSpeed = 5f;

    private Transform cameraTransform;
    private CanvasGroup canvasGroup;

    void Start()
    {
        cameraTransform = Camera.main?.transform;
        if (cameraTransform == null)
        {
            Debug.LogError("VRFaceHUD: 找不到 Main Camera!", this);
            enabled = false;
            return;
        }

        canvasGroup = GetComponent<CanvasGroup>();

        // 初始直接定位，不插值
        ApplyPosition(false);
        ApplyRotation(false);
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        // 自动隐藏
        if (enableAutoHide && canvasGroup != null)
        {
            float angle = Vector3.Angle(cameraTransform.forward, Vector3.up);
            bool shouldShow = angle < (90f + hideAngle) && angle > (90f - hideAngle);
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, shouldShow ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
            if (canvasGroup.alpha < 0.01f) return;
        }

        ApplyPosition(smoothFollow);
        ApplyRotation(smoothFollow);
    }

    private void ApplyPosition(bool smooth)
    {
        Vector3 targetPos = cameraTransform.position
                          + cameraTransform.forward * positionOffset.z
                          + cameraTransform.up * positionOffset.y
                          + cameraTransform.right * positionOffset.x;

        if (smooth)
            transform.position = Vector3.Lerp(transform.position, targetPos, positionSmoothSpeed * Time.unscaledDeltaTime);
        else
            transform.position = targetPos;
    }

    private void ApplyRotation(bool smooth)
    {
        Quaternion targetRot;

        if (rotationMode == RotationMode.HorizontalOnly)
        {
            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();
            targetRot = Quaternion.LookRotation(forward);
        }
        else
        {
            targetRot = Quaternion.LookRotation(cameraTransform.forward);
        }

        if (smooth)
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSmoothSpeed * Time.unscaledDeltaTime);
        else
            transform.rotation = targetRot;
    }
}
