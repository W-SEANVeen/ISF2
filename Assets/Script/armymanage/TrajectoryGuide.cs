using UnityEngine;

/// <summary>
/// 最佳应对轨迹可视化指引 —— 在敌人身上画一条**弧线**，
/// 提示玩家朝这个弧线方向挥砍能打出重击。
///
/// 弧线 = 二次贝塞尔曲线，弯曲幅度由 EnemyCombatController 的 arc 参数控制。
/// 不同状态显示不同颜色。
///
/// 用法：添加到敌人根对象上，会自动找到 EnemyCombatController。
/// </summary>
public class TrajectoryGuide : MonoBehaviour
{
    [Header("指引线外观")]
    [Tooltip("弧线从敌人伸出的总长度（米）")]
    public float lineLength = 2.5f;

    [Tooltip("弧线的宽度")]
    public float lineWidth = 0.12f;

    [Tooltip("弧线由多少段线段组成（越大越平滑）")]
    [Range(4, 32)]
    public int segments = 16;

    [Tooltip("攻击态颜色")]
    public Color color_Attack = new Color(1f, 0.3f, 0.3f, 0.7f);

    [Tooltip("迟滞态颜色")]
    public Color color_Stagger = new Color(1f, 1f, 0.3f, 0.7f);

    [Tooltip("受击后退态颜色")]
    public Color color_HitBack = new Color(0.3f, 1f, 0.3f, 0.7f);

    [Header("位置偏移")]
    [Tooltip("指引线起点抬高（米），敌人脚底到胸口")]
    public float heightOffset = 1.0f;

    [Header("开关")]
    [Tooltip("运行时是否显示指引线")]
    public bool showGuide = true;

    // —— 内部 ——
    private EnemyCombatController combat;
    private LineRenderer line;

    void Start()
    {
        // 自动找 EnemyCombatController（自身或父级）
        combat = GetComponent<EnemyCombatController>();
        if (combat == null)
            combat = GetComponentInParent<EnemyCombatController>();

        // 自动创建 LineRenderer
        line = GetComponent<LineRenderer>();
        if (line == null)
            line = gameObject.AddComponent<LineRenderer>();

        line.positionCount = segments + 1;
        line.useWorldSpace = true;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth * 0.2f; // 末端尖细
        line.material = new Material(Shader.Find("Unlit/Color")) { color = color_Attack };

        line.enabled = false;
    }

    void Update()
    {
        if (combat == null || line == null) return;

        // 没进入战斗时隐藏（行军、爬墙、追击中都不显示）
        if (combat.playerTransform == null)
        {
            line.enabled = false;
            return;
        }

        // 死亡后隐藏
        if (combat.CurrentState == EnemyCombatController.EnemyState.KnockedDown)
        {
            line.enabled = false;
            return;
        }

        if (!showGuide)
        {
            line.enabled = false;
            return;
        }

        // 获取当前状态的轨迹参数（局部方向 → 转世界方向）
        Vector3 localDir = combat.GetCurrentOptimalDirection().normalized;
        if (localDir == Vector3.zero) localDir = Vector3.right;
        Vector3 dir = combat.transform.TransformDirection(localDir);
        float arcDeg = combat.GetCurrentOptimalArc();

        // 更新颜色
        Color col;
        switch (combat.CurrentState)
        {
            case EnemyCombatController.EnemyState.Attack:  col = color_Attack;  break;
            case EnemyCombatController.EnemyState.Stagger: col = color_Stagger; break;
            case EnemyCombatController.EnemyState.HitBack: col = color_HitBack; break;
            default: col = color_Attack; break;
        }
        line.material.color = col;
        line.startColor = col;
        line.endColor = new Color(col.r, col.g, col.b, 0f);

        // 弧线穿过敌人胸口，表示挥砍路径
        Vector3 chest = transform.position + Vector3.up * heightOffset; // 胸口中心
        float halfLen = lineLength * 0.5f;
        Vector3 arcStart = chest - dir * halfLen;  // 入口侧（方向反侧）
        Vector3 arcEnd   = chest + dir * halfLen;  // 出口侧（方向同侧）

        // 控制点向玩家方向弯曲（弧线朝玩家鼓出来）
        Vector3 toPlayer = Vector3.zero;
        if (combat.playerTransform != null)
            toPlayer = (combat.playerTransform.position - chest).normalized;
        if (toPlayer == Vector3.zero) toPlayer = Vector3.forward;

        float arcRad = arcDeg * Mathf.Deg2Rad;
        float bowAmount = lineLength * Mathf.Tan(arcRad * 0.5f);
        Vector3 control = chest + toPlayer * bowAmount;

        // 用贝塞尔曲线填充 LineRenderer
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 point = QuadraticBezier(arcStart, control, arcEnd, t);
            line.SetPosition(i, point);
        }

        line.enabled = true;
    }

    /// <summary>二次贝塞尔插值</summary>
    static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    void OnDestroy()
    {
        if (line != null && line.material != null)
            Destroy(line.material);
    }
}
