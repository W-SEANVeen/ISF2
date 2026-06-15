using UnityEngine;

/// <summary>
/// 最佳应对轨迹可视化 —— 在敌人胸口画一条弧线光带 + 箭头，
/// 提示玩家沿此方向挥砍可打出重击。
/// </summary>
public class TrajectoryGuide : MonoBehaviour
{
    [Header("弧线形状")]
    public float lineLength = 2.5f;
    public float lineWidth = 0.15f;
    [Range(8, 48)] public int segments = 24;
    [Tooltip("弧线中心点相对敌人脚底的位置偏移")]
    public Vector3 chestOffset = new Vector3(0f, 1.3f, 0.5f);

    [Header("箭头")]
    public float arrowSize = 0.25f;   // 箭头大小
    public float arrowAngle = 35f;    // 箭头张角（度）

    [Header("颜色（按状态）")]
    public Color color_Attack  = new Color(1f, 0.2f, 0.1f, 0.9f);
    public Color color_Stagger = new Color(1f, 0.8f, 0.1f, 0.9f);
    public Color color_HitBack = new Color(0.2f, 1f, 0.3f, 0.9f);

    [Header("动画")]
    public float pulseSpeed = 4f;
    [Range(0f, 0.5f)] public float pulseAmount = 0.15f;

    [Header("开关")]
    public bool showGuide = true;

    // 内部
    private EnemyCombatController combat;
    private LineRenderer line;
    private LineRenderer arrowLine; // 箭头用的第二条线
    private Material mat;

    void Start()
    {
        combat = GetComponent<EnemyCombatController>();
        if (combat == null) combat = GetComponentInParent<EnemyCombatController>();

        // ── 主线（弧线光带） ──
        line = GetComponent<LineRenderer>();
        if (line == null) line = gameObject.AddComponent<LineRenderer>();

        mat = CreateGlowMaterial();
        line.material = mat;
        // 原点（玩家侧）尖细 → 靠近敌人端变粗
        line.startWidth = 0f;
        line.endWidth   = lineWidth;
        line.positionCount = segments + 1;
        line.useWorldSpace = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        // ── 箭头线（第二个 LineRenderer） ──
        var arrowGo = new GameObject("ArrowHead");
        arrowGo.transform.SetParent(transform);
        arrowLine = arrowGo.AddComponent<LineRenderer>();
        arrowLine.material = mat;
        arrowLine.startWidth = lineWidth * 0.6f;
        arrowLine.endWidth   = 0f;
        arrowLine.positionCount = 4; // tip → left → tip → right
        arrowLine.useWorldSpace = true;
        arrowLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arrowLine.receiveShadows = false;

        line.enabled = false;
        arrowLine.enabled = false;
    }

    Material CreateGlowMaterial()
    {
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var m = new Material(shader);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = 3000;
        return m;
    }

    void Update()
    {
        if (combat == null || line == null) return;

        bool shouldHide = combat.playerTransform == null
                       || combat.CurrentState == EnemyCombatController.EnemyState.KnockedDown
                       || !showGuide;

        line.enabled = !shouldHide;
        arrowLine.enabled = !shouldHide;
        if (shouldHide) return;

        // ── 颜色 ──
        Color col = combat.CurrentState switch
        {
            EnemyCombatController.EnemyState.Attack  => color_Attack,
            EnemyCombatController.EnemyState.Stagger => color_Stagger,
            EnemyCombatController.EnemyState.HitBack => color_HitBack,
            _ => color_Attack
        };
        float pulse = 1f - pulseAmount + Mathf.Sin(Time.time * pulseSpeed * Mathf.PI) * pulseAmount;
        col.a *= pulse;

        // 原点透明 → 敌人端亮色
        mat.color = col;
        Color startC = col; startC.a = 0f;
        line.startColor = startC;
        line.endColor = col;

        // ── 计算弧线 ──
        Vector3 localDir = combat.GetCurrentOptimalDirection().normalized;
        if (localDir == Vector3.zero) localDir = Vector3.right;
        Vector3 dir = combat.transform.TransformDirection(localDir);
        float arcDeg = combat.GetCurrentOptimalArc();

        Vector3 chest = transform.position + chestOffset;
        float halfLen = lineLength * 0.5f;
        Vector3 arcStart = chest - dir * halfLen;
        Vector3 arcEnd   = chest + dir * halfLen;

        Vector3 toPlayer = (combat.playerTransform.position - chest).normalized;
        if (toPlayer == Vector3.zero) toPlayer = Vector3.forward;

        float arcRad = arcDeg * Mathf.Deg2Rad;
        float bowAmount = lineLength * Mathf.Tan(arcRad * 0.5f);
        Vector3 control = chest + toPlayer * bowAmount;

        // ── 弧线各点 ──
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            line.SetPosition(i, QuadraticBezier(arcStart, control, arcEnd, t));
        }

        // ── 箭头（在弧线原点 arcStart，指向挥砍方向） ──
        Vector3 tip = arcStart;
        // 指向弧线前进方向（从 arcStart 到 arcEnd）
        Vector3 arrowDir = (arcEnd - arcStart).normalized;
        if (arrowDir == Vector3.zero) arrowDir = dir;
        Vector3 up = Vector3.up;
        Vector3 right = Vector3.Cross(arrowDir, up).normalized;
        if (right.magnitude < 0.01f) right = Vector3.Cross(arrowDir, Vector3.forward).normalized;

        Vector3 leftDir  = Quaternion.AngleAxis(-arrowAngle * 0.5f, right) * arrowDir;
        Vector3 rightDir = Quaternion.AngleAxis( arrowAngle * 0.5f, right) * arrowDir;

        Vector3 leftPt  = tip + leftDir  * arrowSize;
        Vector3 rightPt = tip + rightDir * arrowSize;

        // 画 V 形箭头：tip → left → tip → right
        arrowLine.SetPosition(0, tip);
        arrowLine.SetPosition(1, leftPt);
        arrowLine.SetPosition(2, tip);
        arrowLine.SetPosition(3, rightPt);

        // 箭头尖亮 → 翼尖淡
        arrowLine.startColor = col;
        arrowLine.endColor = new Color(col.r, col.g, col.b, 0f);
        arrowLine.material = mat;
        arrowLine.startWidth = lineWidth * 0.8f;
        arrowLine.endWidth = 0f;
    }

    static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
