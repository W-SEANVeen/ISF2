using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;


/// <summary>
/// 玩家受伤与死亡系统。
/// - 被箭击中 → 屏幕闪红（Mesh sphere 方式，参考 PXR_ScreenFade）。
/// - 被死士攻击 → 缴弩 + 拿刀 + 闪红 + 倒地 + 结算场景。
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance;

    [Header("闪红参数（Mesh ScreenFade 风格）")]
    [Tooltip("闪红颜色")]
    public Color flashColor = new Color(1f, 0f, 0f, 0.6f);
    [Tooltip("闪红持续时间（秒）")]
    public float flashDuration = 0.25f;

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

    // ---- Mesh 闪红（参考 PXR_ScreenFade） ----
    private GameObject flashMeshObject;
    private MeshRenderer flashMeshRenderer;
    private Material flashMaterial;
    private Coroutine flashCoroutine;

    private EnemyAssault enemyAssault;

    // ======================================================================
    // 生命周期
    // ======================================================================

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        // 在 Start 创建 Mesh（此时 mainCamera 已就绪）
        EnsureDamageFlashMesh();

        enemyAssault = FindObjectOfType<EnemyAssault>();
        if (enemyAssault != null)
            enemyAssault.onPlayerHit.AddListener(OnAssassinHitPlayer);
        else
            Debug.LogError("PlayerHealth: 场景中找不到 EnemyAssault！");
    }

    void OnEnable()
    {
        EnemyAssault.onClimbEnd += OnAssassinClimbEnd;
    }

    void OnDisable()
    {
        EnemyAssault.onClimbEnd -= OnAssassinClimbEnd;
    }

    void OnDestroy()
    {
        if (enemyAssault != null)
            enemyAssault.onPlayerHit.RemoveListener(OnAssassinHitPlayer);

        if (flashMaterial != null)
            Destroy(flashMaterial);
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
    // 屏幕闪红（Mesh 方式，参考 PXR_ScreenFade）
    // ======================================================================

    /// <summary>
    /// 创建包围摄像机的球体 Mesh（面向内法线），使用透明红色材质。
    /// 和 PXR_ScreenFade 同样的手法：ZTest Always + Alpha Blend，
    /// 确保在 VR 双目渲染中正确覆盖画面。
    /// </summary>
    void EnsureDamageFlashMesh()
    {
        if (flashMeshObject != null) return;

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("PlayerHealth: 找不到 Main Camera，无法创建闪红 Mesh！");
            return;
        }

        flashMeshObject = new GameObject("DamageFlashMesh");
        flashMeshObject.transform.SetParent(mainCamera.transform, false);

        var mf = flashMeshObject.AddComponent<MeshFilter>();
        flashMeshRenderer = flashMeshObject.AddComponent<MeshRenderer>();

        // ---- 构建面向内的球体网格（同 PXR_ScreenFade） ----
        mf.mesh = CreateInwardSphereMesh();

        // ---- 使用跟 PXR_ScreenFade 同样的 shader（ZTest Always + Alpha Blend） ----
        Shader shader = Shader.Find("PXR_SDK/PXR_Fade");
        // 万一 PXR shader 不在构建中，fallback 到 URP Unlit Transparent
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        flashMaterial = new Material(shader);
        flashMaterial.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        flashMaterial.renderQueue = 4000;

        // URP Unlit 需要手动开启透明度
        if (shader != null && shader.name.Contains("Unlit"))
        {
            flashMaterial.SetFloat("_Surface", 1f);       // Transparent
            flashMaterial.SetFloat("_Blend", 0f);         // Alpha
            flashMaterial.SetFloat("_AlphaClip", 0f);
            flashMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            flashMaterial.SetInt("_Cull", 0);             // Off — 保证内部可见
            flashMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            flashMaterial.EnableKeyword("_ALPHATEST_ON");
        }

        flashMeshRenderer.material = flashMaterial;
        flashMeshRenderer.enabled = false;
    }

    /// <summary>构造向内法线的球体 Mesh（同 PXR_ScreenFade）。</summary>
    static Mesh CreateInwardSphereMesh()
    {
        int N = 5;
        var verts = new List<Vector3>();
        var indices = new List<int>();

        // 六面片，归一化到球体
        for (float i = -N / 2f; i <= N / 2f; i++)
            for (float j = -N / 2f; j <= N / 2f; j++)
                verts.Add(new Vector3(i, j, -N / 2f));
        for (float i = -N / 2f; i <= N / 2f; i++)
            for (float j = -N / 2f; j <= N / 2f; j++)
                verts.Add(new Vector3(N / 2f, j, i));
        for (float i = -N / 2f; i <= N / 2f; i++)
            for (float j = -N / 2f; j <= N / 2f; j++)
                verts.Add(new Vector3(i, N / 2f, j));
        for (float i = -N / 2f; i <= N / 2f; i++)
            for (float j = -N / 2f; j <= N / 2f; j++)
                verts.Add(new Vector3(-N / 2f, j, i));
        for (float i = -N / 2f; i <= N / 2f; i++)
            for (float j = -N / 2f; j <= N / 2f; j++)
                verts.Add(new Vector3(i, j, N / 2f));
        for (float i = -N / 2f; i <= N / 2f; i++)
            for (float j = -N / 2f; j <= N / 2f; j++)
                verts.Add(new Vector3(i, -N / 2f, j));

        // 归一化到半径 0.7
        for (int i = 0; i < verts.Count; i++)
            verts[i] = verts[i].normalized * 0.7f;

        // 前 4 面三角化
        for (int num = 0; num < 4; num++)
        {
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    int idx = j * (N + 1) + (N + 1) * (N + 1) * num + i;
                    int up = (j + 1) * (N + 1) + (N + 1) * (N + 1) * num + i;
                    indices.AddRange(new[] { idx, idx + 1, up + 1 });
                    indices.AddRange(new[] { idx, up + 1, up });
                }
            }
        }
        // 后 2 面（winding 相反）
        for (int num = 4; num < 6; num++)
        {
            for (int i = 0; i < N + 1; i++)
            {
                for (int j = 0; j < N + 1; j++)
                {
                    if (i != N && j != N)
                    {
                        int idx = j * (N + 1) + (N + 1) * (N + 1) * num + i;
                        int up = (j + 1) * (N + 1) + (N + 1) * (N + 1) * num + i;
                        indices.AddRange(new[] { idx, up + 1, idx + 1 });
                        indices.AddRange(new[] { idx, up, up + 1 });
                    }
                }
            }
        }

        var mesh = new Mesh();
        mesh.vertices = verts.ToArray();
        mesh.triangles = indices.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // 法线翻转 → 面向内
        var normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++)
            normals[i] = -normals[i];
        mesh.normals = normals;

        // 三角形 winding 翻转
        var tris = mesh.triangles;
        for (int i = 0; i < tris.Length; i += 3)
        {
            (tris[i], tris[i + 2]) = (tris[i + 2], tris[i]);
        }
        mesh.triangles = tris;

        return mesh;
    }

    public void FlashDamage()
    {
        if (flashMeshObject == null)
            EnsureDamageFlashMesh();
        if (flashMeshObject == null) return;

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        flashMeshRenderer.enabled = true;

        float maxAlpha = flashColor.a;
        float elapsed = 0f;

        while (elapsed < flashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(maxAlpha, 0f, elapsed / flashDuration);
            var c = flashMaterial.color;
            c.a = alpha;
            flashMaterial.color = c;
            yield return null;
        }

        var final = flashMaterial.color;
        final.a = 0f;
        flashMaterial.color = final;

        flashMeshRenderer.enabled = false;
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
