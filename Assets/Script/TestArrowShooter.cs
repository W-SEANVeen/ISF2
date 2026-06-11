using UnityEngine;

/// <summary>
/// 测试工具：从玩家头顶丢箭，靠重力下落，测试碰撞体检测。
/// 右键 TestArrowShooter > StartTest / StopTest 控制。
/// </summary>
public class TestArrowShooter : MonoBehaviour
{
    [Header("箭矢预制体")]
    public GameObject arrowPrefab;

    [Header("发射参数")]
    public float interval = 2f;
    [Tooltip("从玩家头顶多高丢下来")]
    public float dropHeight = 15f;

    private Transform player;

    void Start()
    {
        var health = PlayerHealth.Instance;
        if (health != null) player = health.transform;
        if (player == null)
        {
            var camGo = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGo != null) player = camGo.transform.root;
        }
    }

    [ContextMenu("▶ 开始丢箭")]
    public void StartTest()
    {
        if (arrowPrefab == null)
        {
            Debug.LogError("请把 Enemy_Arrow_Prefab 拖到 arrowPrefab 上");
            return;
        }
        if (player == null)
        {
            Debug.LogError("找不到玩家");
            return;
        }

        InvokeRepeating(nameof(DropArrow), 0f, interval);
        Debug.Log("▶ 测试箭已开始");
    }

    [ContextMenu("⏹ 停止丢箭")]
    public void StopTest()
    {
        CancelInvoke(nameof(DropArrow));
        Debug.Log("⏹ 测试箭已停止");
    }

    void DropArrow()
    {
        if (player == null || arrowPrefab == null) return;

        // 在玩家正上方生成，自由落体
        Vector3 spawnPos = player.position + Vector3.up * dropHeight;

        GameObject arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
        arrow.tag = "EnemyArrow";

        // ArrowFlight.OnEnable 会清空速度，但我们只靠重力下落，所以不管它
        // 箭头朝下
        arrow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        Destroy(arrow, 4f);
    }

    void OnDestroy()
    {
        StopTest();
    }
}
