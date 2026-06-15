using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 玩家受伤特效与场景结算系统。
/// - 被箭击中 → 屏幕闪红（Mesh sphere 方式）。
/// - 被死士攻击 → PlayerCombatController 调用本类 FlashDamage() 闪红。
/// - 提供 knifeGrab 引用供 PlayerCombatController 在被击中时强制丢弃武器。
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
    public StickyGrabInteractable knifeGrab;

    // ---- Mesh 闪红（参考 PXR_ScreenFade） ----
    private GameObject flashMeshObject;
    private MeshRenderer flashMeshRenderer;
    private Material flashMaterial;
    private Coroutine flashCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        EnsureDamageFlashMesh();
    }

    void OnDestroy()
    {
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

    void EnsureDamageFlashMesh()
    {
        if (flashMeshObject != null) return;

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("PlayerHealth: 找不到 Main Camera，无法创建闪红 Mesh！");
            return;
        }

        flashMeshObject = new GameObject("DamageFlashMesh");
        flashMeshObject.transform.SetParent(cam.transform, false);

        var mf = flashMeshObject.AddComponent<MeshFilter>();
        flashMeshRenderer = flashMeshObject.AddComponent<MeshRenderer>();
        mf.mesh = CreateInwardSphereMesh();

        Shader shader = Shader.Find("PXR_SDK/PXR_Fade");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        flashMaterial = new Material(shader);
        flashMaterial.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        flashMaterial.renderQueue = 4000;

        if (shader != null && shader.name.Contains("Unlit"))
        {
            flashMaterial.SetFloat("_Surface", 1f);
            flashMaterial.SetFloat("_Blend", 0f);
            flashMaterial.SetFloat("_AlphaClip", 0f);
            flashMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            flashMaterial.SetInt("_Cull", 0);
            flashMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            flashMaterial.EnableKeyword("_ALPHATEST_ON");
        }

        flashMeshRenderer.material = flashMaterial;
        flashMeshRenderer.enabled = false;
    }

    static Mesh CreateInwardSphereMesh()
    {
        int N = 5;
        var verts = new List<Vector3>();
        var indices = new List<int>();

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

        for (int i = 0; i < verts.Count; i++)
            verts[i] = verts[i].normalized * 0.7f;

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

        var normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++)
            normals[i] = -normals[i];
        mesh.normals = normals;

        var tris = mesh.triangles;
        for (int i = 0; i < tris.Length; i += 3)
        {
            (tris[i], tris[i + 2]) = (tris[i + 2], tris[i]);
        }
        mesh.triangles = tris;

        return mesh;
    }

    /// <summary>屏幕闪红（供 PlayerCombatController 和箭矢命中调用）</summary>
    public void FlashDamage()
    {
        if (flashMeshObject == null)
            EnsureDamageFlashMesh();
        if (flashMeshObject == null) return;

        // 确保 renderer 和 alpha 从最高值开始，防止上次未播完
        flashMeshRenderer.enabled = true;
        var c = flashMaterial.color;
        c.a = flashColor.a;
        flashMaterial.color = c;

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
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
}
