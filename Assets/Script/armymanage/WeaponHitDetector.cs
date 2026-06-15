using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 武器碰撞检测器 —— 挂在武器物体上。
/// 如果碰撞体在子物体上，会自动给子物体挂转发器，确保 OnTriggerEnter 能收到。
/// </summary>
public class WeaponHitDetector : MonoBehaviour
{
    public enum WeaponOwner { Player, Enemy }

    [Header("所属阵营")]
    [Tooltip("这把武器是谁的")]
    public WeaponOwner owner;

    [Header("攻击判定")]
    [Tooltip("武器速度低于此值视为无效攻击（仅对 Player 生效，Enemy 动画驱动不受限）")]
    public float minSwingSpeed = 1.5f;

    // —— 运动追踪 ——
    private Vector3 lastPosition;
    private Vector3 velocity;

    // —— 缓存引用 ——
    private PlayerCombatController playerCombat;
    private EnemyCombatController enemyCombat;

    // 子级挂载的转发器列表
    private List<TriggerForwarder> forwarders = new List<TriggerForwarder>();

    // 防止同一帧多个子碰撞体重复触发
    private int lastHitFrame = -1;

    void Start()
    {
        // 从父层级往上找对应的 CombatController
        if (owner == WeaponOwner.Player)
        {
            playerCombat = GetComponentInParent<PlayerCombatController>();
            if (playerCombat == null)
                playerCombat = PlayerCombatController.Instance;
        }
        else
        {
            enemyCombat = GetComponentInParent<EnemyCombatController>();
        }

        lastPosition = transform.position;

        // ★ 自动给子级 Trigger 碰撞体挂转发器
        // Unity 的 OnTriggerEnter 只发给碰撞体所在的 GameObject，
        // 如果碰撞体在子物体上、脚本在父物体上，就需要转发。
        SetupTriggerForwarders();

        // 诊断日志
        var cols = GetComponentsInChildren<Collider>();
        int triggerCount = 0;
        foreach (var c in cols) if (c.isTrigger) triggerCount++;
        var rb = GetComponentInParent<Rigidbody>();

        Debug.Log($"[WeaponHitDetector] 初始化完成 | owner={owner} | " +
                  $"挂载对象={gameObject.name} | " +
                  $"子级Collider={cols.Length} | Trigger={triggerCount} | " +
                  $"转发器={forwarders.Count} | " +
                  $"父级Rigidbody={(rb != null ? rb.gameObject.name : "无(!)")} | " +
                  $"CombatController={(owner == WeaponOwner.Player ? (playerCombat != null ? "已找到" : "未找到(!)") : (enemyCombat != null ? "已找到" : "未找到(!)"))}");
    }

    void SetupTriggerForwarders()
    {
        // 获取所有子级碰撞体（不含自身）
        var allCols = GetComponentsInChildren<Collider>();
        foreach (var col in allCols)
        {
            // 跳过自身 GameObject 上的碰撞体（脚本能直接收到 OnTriggerEnter）
            if (col.gameObject == gameObject) continue;
            // 只处理 Trigger 碰撞体
            if (!col.isTrigger) continue;
            // 已经挂了转发器就跳过
            if (col.GetComponent<TriggerForwarder>() != null) continue;

            var fwd = col.gameObject.AddComponent<TriggerForwarder>();
            fwd.parent = this;
            forwarders.Add(fwd);
            Debug.Log($"[WeaponHitDetector] 🔗 已挂转发器到子物体 '{col.gameObject.name}' ({col.GetType().Name})");
        }
    }

    void Update()
    {
        Vector3 currentPos = transform.position;
        velocity = (currentPos - lastPosition) / Time.deltaTime;
        lastPosition = currentPos;
    }

    /// <summary>
    /// 子级碰撞体触发 → 转发器调用此方法
    /// </summary>
    internal void OnChildTriggerEnter(Collider other)
    {
        OnWeaponTrigger(other);
    }

    /// <summary>
    /// 自身碰撞体触发（直接）
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        OnWeaponTrigger(other);
    }

    /// <summary>统一的碰撞处理</summary>
    void OnWeaponTrigger(Collider other)
    {
        // 同一帧内多个子碰撞体触发只处理一次（防止一刀双判）
        if (Time.frameCount == lastHitFrame) return;
        lastHitFrame = Time.frameCount;

        float speed = velocity.magnitude;

        // 玩家需要真的挥刀（速度达标），敌人动画驱动碰了就有效
        if (owner == WeaponOwner.Player && speed < minSwingSpeed)
        {
            Debug.Log($"[WeaponHitDetector] 攻击无效：速度过低 | speed={speed:F2} < min={minSwingSpeed} | 碰到={other.name}({other.tag})");
            return;
        }

        Debug.Log($"[WeaponHitDetector] ⚔ 有效攻击！owner={owner} | 碰到={other.name}({other.tag}) | speed={speed:F2}");

        if (owner == WeaponOwner.Player && playerCombat != null)
        {
            playerCombat.OnWeaponHit(other, velocity);
        }
        else if (owner == WeaponOwner.Enemy && enemyCombat != null)
        {
            enemyCombat.OnWeaponHit(other, velocity);
        }
        else
        {
            Debug.LogWarning($"[WeaponHitDetector] 碰撞但无法转发 | combatController=null");
        }
    }

    void OnDestroy()
    {
        // 清理子物体上的转发器
        foreach (var f in forwarders)
            if (f != null) Destroy(f);
        forwarders.Clear();
    }
}

/// <summary>
/// 内部类：挂在子级 Trigger 碰撞体上，把 OnTriggerEnter 转发给父 WeaponHitDetector。
/// </summary>
internal class TriggerForwarder : MonoBehaviour
{
    public WeaponHitDetector parent;

    void OnTriggerEnter(Collider other)
    {
        if (parent != null)
            parent.OnChildTriggerEnter(other);
    }
}
