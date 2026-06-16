using UnityEngine;
using System.Collections;

/// <summary>
/// 玩家战斗控制器 —— 管理玩家血量、武器命中判定、被击硬直与死亡。
///
/// 判定规则（都在 OnWeaponHit 中实现）：
///   1. 玩家武器碰到敌人身体要害（"EnemyVital" 标签）
///      - 若玩家挥砍方向与敌人当前状态的最佳轨迹方向夹角 < 60° → 重击（severe）
///      - 否则 → 轻击
///      - 调用 enemy.TakeDamage(isSevere) 处理后续状态转移
///
///   2. 玩家武器碰到敌人武器（"EnemyWeapon" 标签）
///      - 若敌人处于 Attack 状态 → 格挡！调用 enemy.OnBlocked()
///      - 否则 → 无效攻击，无效果
///
///   3. 敌人武器碰到玩家身体要害（"PlayerVital" 标签）
///      - 由敌人侧的 WeaponHitDetector → EnemyCombatController.OnWeaponHit 处理
///      - 最终调用本类的 TakeDamageFromEnemy() → 扣血 + 闪红 + 0.5s 硬直
///
/// 死亡条件：
///   血量 ≤ 0 → 黑屏，显示"被打死"
/// </summary>
public class PlayerCombatController : MonoBehaviour
{
    // ============================================================
    // 单例
    // ============================================================

    public static PlayerCombatController Instance { get; private set; }

    // ============================================================
    // Inspector 可配置字段
    // ============================================================

    [Header("玩家属性（隐藏血条）")]
    [Tooltip("总血量，默认100")]
    public float maxHP = 100f;

    [Tooltip("被敌人击中时扣除的血量")]
    public float damagePerEnemyHit = 25f;

    [Tooltip("被敌人击中后的行动力丧失时间（秒）")]
    public float stunDuration = 0.5f;

    [Header("挥砍轨迹判定")]
    [Tooltip("玩家挥砍方向与敌人最佳轨迹方向的点积阈值。>此值判定为沿最佳轨迹（重击）")]
    [Range(0f, 1f)]
    public float trajectoryThreshold = 0.5f;

    [Header("死亡结算")]
    [Tooltip("血量归零到黑屏之间的延迟（秒）")]
    public float gameOverDelay = 1.5f;

    // ============================================================
    // 内部状态
    // ============================================================

    private float currentHP;
    public float CurrentHP => currentHP;
    private bool isDead = false;
    public bool IsDead => isDead;

    // 硬直标记：硬直期间玩家无法移动/攻击
    private bool isStunned = false;
    public bool IsStunned => isStunned;

    // 敌人战斗模块引用（场景中最近的那个——只有一个死士）
    private EnemyCombatController enemy;

    // ============================================================
    // Unity 生命周期
    // ============================================================

    void Awake()
    {
        // 单例模式
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        currentHP = maxHP;

        // ★ 确保玩家身体有 PlayerVital 标签（敌人武器靠这个识别玩家）
        if (!gameObject.CompareTag("PlayerVital"))
        {
            gameObject.tag = "PlayerVital";
            Debug.Log("[PlayerCombat] ✅ 已自动将玩家 Tag 设为 PlayerVital");
        }

        // 找到场景里的敌人战斗控制器（由 EnemyCombatController Start 中初始化）
        enemy = FindObjectOfType<EnemyCombatController>();
        if (enemy == null)
        {
            Debug.LogWarning("[PlayerCombat] 场景中未找到 EnemyCombatController，战斗中无法攻击敌人！");
        }

        // ★ 检查武器配置（不修改任何物理属性）
        SetupWeaponHitDetector();

        // 首次输出血量状态
        LogHPStatus("战斗初始化");
    }

    // ============================================================
    // 武器自动配置
    // ============================================================

    /// <summary>
    /// 检查玩家武器是否已挂 WeaponHitDetector。如果没挂，打印明确错误提示。
    /// 不再运行时自动添加（避免干扰 XR 抓取），请在 Editor 里手动挂一次。
    /// </summary>
    void SetupWeaponHitDetector()
    {
        if (PlayerHealth.Instance == null)
        {
            Debug.LogError("[PlayerCombat] ❌ PlayerHealth.Instance 为空！");
            return;
        }
        if (PlayerHealth.Instance.knifeGrab == null)
        {
            Debug.LogError("[PlayerCombat] ❌ knifeGrab 未赋值！请在 PlayerHealth 上把 knife 拖进去。");
            return;
        }

        var knifeObj = PlayerHealth.Instance.knifeGrab.gameObject;
        var detector = knifeObj.GetComponent<WeaponHitDetector>();
        var col = knifeObj.GetComponent<Collider>();

        if (detector == null)
        {
            Debug.LogError($"[PlayerCombat] ❌ knife 上缺少 WeaponHitDetector！" +
                          $"请在 Editor 中选中 '{knifeObj.name}' → Add Component → WeaponHitDetector → Owner 设为 Player");
        }
        else if (detector.owner != WeaponHitDetector.WeaponOwner.Player)
        {
            Debug.LogWarning("[PlayerCombat] ⚠ WeaponHitDetector.owner 不是 Player，已自动修正");
            detector.owner = WeaponHitDetector.WeaponOwner.Player;
        }

        Debug.Log($"[PlayerCombat] 武器检查完成 | WeaponHitDetector={(detector != null ? "✅" : "❌ 缺失！")} | " +
                  $"Collider={col != null} | isTrigger={col?.isTrigger}");
    }

    // ============================================================
    // 血量日志
    // ============================================================

    /// <summary>输出一行日志：玩家和敌人的当前血量对比</summary>
    void LogHPStatus(string reason)
    {
        float enemyHP = (enemy != null) ? enemy.currentHP : -1f;
        float enemyMax = (enemy != null) ? enemy.maxHP : -1f;
        Debug.Log($"<color=cyan>[血量]</color> {reason} | 玩家: {currentHP}/{maxHP} | 敌人: {enemyHP}/{enemyMax}");
    }

    // ============================================================
    // 武器碰撞回报（玩家武器碰到敌人时由 WeaponHitDetector 调用）
    // ============================================================

    /// <summary>
    /// 玩家武器上的 WeaponHitDetector 在 OnTriggerEnter 中调用此方法。
    /// 根据武器碰撞瞬间的速度判定重击/轻击。
    /// </summary>
    /// <param name="other">被玩家武器碰到的碰撞体</param>
    /// <param name="weaponVelocity">武器当前帧的速度向量（含方向 + 大小）</param>
    public void OnWeaponHit(Collider other, Vector3 weaponVelocity)
    {
        Debug.Log($"[PlayerCombat] OnWeaponHit 收到碰撞 | 碰到={other.name}({other.tag}) | 速度={weaponVelocity.magnitude:F2} | enemy={(enemy != null ? enemy.name : "NULL")} | isDead={isDead}");

        if (isDead) { Debug.Log("[PlayerCombat] 忽略：玩家已死"); return; }
        if (enemy == null) { Debug.Log("[PlayerCombat] 忽略：enemy 引用为空"); return; }

        // ---------- 情况 A：碰到敌人身体要害 → 有效攻击 ----------
        if (other.CompareTag("EnemyVital"))
        {
            bool isSevere = CheckOptimalTrajectory(weaponVelocity.normalized);
            Debug.Log($"[PlayerCombat] ✅ 命中 EnemyVital → 调用 TakeDamage(isSevere={isSevere})，敌人当前状态={enemy.CurrentState}");
            enemy.TakeDamage(isSevere);
            LogHPStatus($"玩家{(isSevere ? "重击" : "轻击")}命中敌人");
            return;
        }

        // ---------- 情况 B：碰到敌人武器 → 格挡 ----------
        if (other.CompareTag("EnemyWeapon"))
        {
            Debug.Log($"[PlayerCombat] 碰到敌人武器，敌人状态={enemy.CurrentState}");
            if (enemy.CurrentState == EnemyCombatController.EnemyState.Attack)
            {
                Debug.Log("[PlayerCombat] 格挡成功！敌人迟滞。");
                enemy.OnBlocked();
            }
            return;
        }

        Debug.Log($"[PlayerCombat] 忽略：Tag 不匹配 ({other.tag})");
    }

    // ============================================================
    // 被敌人击中（由 EnemyCombatController.OnWeaponHit 调用）
    // ============================================================

    /// <summary>
    /// 敌人武器击中玩家身体要害时调用。
    /// 扣血 + 闪红 + 行动硬直 0.5 秒 + 血量归零时死亡结算。
    /// </summary>
    public void TakeDamageFromEnemy()
    {
        if (isDead) return;

        // 扣血
        currentHP -= damagePerEnemyHit;
        Debug.Log($"[PlayerCombat] 被敌人击中！扣血 {damagePerEnemyHit}，剩余血量 {currentHP}/{maxHP}");
        LogHPStatus("玩家被敌人击中");

        // 屏幕闪红
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.FlashDamage();

        // 血量归零 → 死亡
        if (currentHP <= 0)
        {
            Die();
            return;
        }

        // 未死 → 行动力丧失 0.5 秒
        StartCoroutine(StunRoutine());
    }

    IEnumerator StunRoutine()
    {
        isStunned = true;

        // TODO: 这里可以禁用玩家移动/攻击输入
        // 例如：locomotion.enabled = false; 或设置移动速度为 0

        yield return new WaitForSeconds(stunDuration);

        isStunned = false;

        // TODO: 恢复玩家移动/攻击能力
    }

    // ============================================================
    // 轨迹判定
    // ============================================================

    /// <summary>
    /// 比较玩家挥砍方向与敌人当前状态的最佳应对轨迹。
    /// 方向一致（点积 > threshold）= 重击，否则轻击。
    /// 场景中 TrajectoryGuide 画的指引线会帮助你对准。
    /// </summary>
    bool CheckOptimalTrajectory(Vector3 playerSwingDir)
    {
        Vector3 optimalDir = enemy.GetCurrentOptimalDirection().normalized;
        float dot = Vector3.Dot(playerSwingDir, optimalDir);
        return dot > trajectoryThreshold;
    }

    // ============================================================
    // 死亡
    // ============================================================

    /// <summary>玩家死亡：黑屏 + 显示"被打死"结算</summary>
    void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("[PlayerCombat] 玩家血量归零，即将黑屏结算（失败）……");

        // 通知全局战斗管理器 —— 玩家输了
        if (CombatManager.Instance != null)
            CombatManager.Instance.LoseCombat();
    }

    // ============================================================
    // 公共辅助
    // ============================================================

    /// <summary>获取当前血量（百分比 0~1）</summary>
    public float GetHPPercent()
    {
        return Mathf.Clamp01(currentHP / maxHP);
    }

    // ============================================================
    // 简易血条（OnGUI 调试用，发布前可删除）
    // ============================================================

    void OnGUI()
    {
        if (isDead) return;

        float barWidth = 300f;
        float barHeight = 24f;
        float margin = 20f;

        // ── 玩家血条（左上）──
        float playerPercent = Mathf.Clamp01(currentHP / maxHP);
        Rect playerBg = new Rect(margin, margin, barWidth, barHeight);
        GUI.Box(playerBg, "");
        Rect playerFill = new Rect(margin + 2, margin + 2, (barWidth - 4) * playerPercent, barHeight - 4);
        GUI.backgroundColor = Color.green;
        GUI.Box(playerFill, "");
        GUI.backgroundColor = Color.white;
        GUI.Label(new Rect(margin, margin, barWidth, barHeight), $" 玩家 HP: {currentHP}/{maxHP}");

        // ── 敌人血条（右上）──
        if (enemy != null && enemy.CurrentState != EnemyCombatController.EnemyState.KnockedDown)
        {
            float enemyPercent = Mathf.Clamp01(enemy.currentHP / enemy.maxHP);
            float enemyX = Screen.width - barWidth - margin;
            Rect enemyBg = new Rect(enemyX, margin, barWidth, barHeight);
            GUI.Box(enemyBg, "");
            Rect enemyFill = new Rect(enemyX + 2, margin + 2, (barWidth - 4) * enemyPercent, barHeight - 4);
            GUI.backgroundColor = Color.red;
            GUI.Box(enemyFill, "");
            GUI.backgroundColor = Color.white;
            GUI.Label(new Rect(enemyX, margin, barWidth, barHeight), $" 敌人 HP: {enemy.currentHP}/{enemy.maxHP}");
        }
    }
}
