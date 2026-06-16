using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// 敌人（死士）战斗控制器 —— 管理四大核心状态与伤害逻辑。
///
/// 状态流转：
///   Attack ──(被格挡)──→ Stagger ──(1.5s)──→ Attack
///   Attack ──(被轻击)──→ HitBack ──(1s)──→ Attack
///                 (被重击)──→ HitBack ──(1s)──→ Stagger ──(1.5s)──→ Attack
///   Stagger ─(被攻击)──→ HitBack
///   HitBack ─(被攻击)──→ KnockedDown
///   任意态 ──(HP≤0)───→ KnockedDown
///
/// 碰撞检测模式：
///   敌兵武器上挂 WeaponHitDetector(owner=Enemy)，
///   挥舞时碰到"PlayerVital"标签的碰撞体 → 玩家受伤。
///   玩家的武器挂 WeaponHitDetector(owner=Player)，
///   由 PlayerCombatController 统一判断轨迹、伤害等级后调用本类方法。
/// </summary>
public class EnemyCombatController : MonoBehaviour
{
    // ============================================================
    // 状态枚举
    // ============================================================

    /// <summary>敌人四大核心状态</summary>
    public enum EnemyState
    {
        Attack,      // 攻击中（主动挥砍，2种动画随机）
        Stagger,     // 迟滞（被格挡弹开，短时间无法行动）
        HitBack,     // 受击后退（被有效砍中，正在播放受伤动画）
        KnockedDown  // 受击倒地（HitBack中再被攻击 → 倒地死亡，战斗结束）
    }

    // ============================================================
    // Inspector 可配置字段
    // ============================================================

    [Header("当前状态（只读）")]
    [SerializeField] private EnemyState _currentState = EnemyState.Attack;
    public EnemyState CurrentState => _currentState;

    [Header("敌人属性（隐藏血条）")]
    [Tooltip("总血量，默认100")]
    public float maxHP = 100f;

    /// <summary>当前血量（公开只读，供外部日志读取）</summary>
    public float currentHP { get; private set; }

    [Tooltip("轻击伤害（玩家武器碰到要害但未沿最佳轨迹）")]
    public float lightDamage = 30f;

    [Tooltip("重击伤害（玩家武器沿最佳轨迹碰到要害）")]
    public float heavyDamage = 70f;

    [Header("动作持续时间")]
    [Tooltip("攻击动画播完到下次攻击的间隔")]
    public float attackLoopInterval = 1.2f;

    [Tooltip("迟滞状态持续秒数")]
    public float staggerDuration = 1.5f;

    [Tooltip("受击后退持续秒数")]
    public float hitBackDuration = 1.0f;

    [Header("最佳应对轨迹")]
    [Tooltip("敌人 Attack 时，最佳反击挥砍方向（局部坐标：右=1,0,0 上=0,1,0 前=0,0,1）")]
    public Vector3 optimalDir_Attack = Vector3.right;

    [Tooltip("弧线弯曲幅度（度），0=直线，30=中等弧线，60=大弧线")]
    [Range(0f, 80f)]
    public float optimalArc_Attack = 30f;

    [Tooltip("敌人 Stagger 时，最佳反击挥砍方向")]
    public Vector3 optimalDir_Stagger = Vector3.up;

    [Range(0f, 80f)]
    public float optimalArc_Stagger = 20f;

    [Tooltip("敌人 HitBack 时，最佳反击挥砍方向")]
    public Vector3 optimalDir_HitBack = Vector3.forward;

    [Range(0f, 80f)]
    public float optimalArc_HitBack = 15f;

    [Header("战斗引用")]
    [Tooltip("武器拖尾控制器（可选）")]
    public WeaponTrailController weaponTrailController;

    [Header("检测范围")]
    [Tooltip("玩家跑出此距离，敌人放弃战斗去追击")]
    public float chaseRange = 5f;

    [Tooltip("玩家进入此距离，敌人恢复攻击")]
    public float attackRange = 2.5f;

    [Header("死士事件")]
    [Tooltip("敌兵武器碰到玩家身体时触发")]
    public UnityEvent onPlayerHit;

    // ============================================================
    // 内部状态
    // ============================================================

    private Animator anim;
    private EnemyAssault enemyAssault;
    private bool wasLastHitSevere = false;
    private float lastDamageTime = -999f; // 上次受伤时间，防止一刀多判

    // 玩家引用，由 EnemyAssault.BeginCombatPhase 传入
    public Transform playerTransform { get; set; }

    // ============================================================
    // Unity 生命周期
    // ============================================================

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        if (anim == null)
            anim = GetComponent<Animator>();

        enemyAssault = GetComponent<EnemyAssault>();

        currentHP = maxHP;
        // 注意：不在 Start 里攻击！由 EnemyAssault 在战斗开始时调用 BeginCombatPhase
    }

    void Update()
    {
        if (_currentState == EnemyState.KnockedDown) return;
        if (playerTransform == null) return;

        // —— 检测玩家是否超出追击范围 ——
        float dist = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(playerTransform.position.x, 0f, playerTransform.position.z)
        );

        if (dist > chaseRange)
        {
            // 玩家跑远了 → 切回追击模式
            if (enemyAssault != null)
                enemyAssault.ResumeChasing(playerTransform);
            return;
        }

        // 战斗中始终面朝玩家
        Vector3 lookDir = playerTransform.position - transform.position;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(lookDir),
                Time.deltaTime * 10f
            );
        }
    }

    // ============================================================
    // 战斗入口（由 EnemyAssault 在翻墙后调用）
    // ============================================================

    /// <summary>
    /// EnemyAssault 在死士翻过城墙、距离玩家足够近时调用，
    /// 将玩家引用传入并正式进入战斗循环。
    /// </summary>
    public void BeginCombatPhase(Transform player)
    {
        playerTransform = player;
        EnterState_Attack();
    }

    // ============================================================
    // 武器碰撞回报（敌兵武器碰到玩家身体时由 WeaponHitDetector 调用）
    // ============================================================

    /// <summary>
    /// 由敌人武器上的 WeaponHitDetector 调用。
    /// 敌人武器只负责伤害玩家，不需要判断轨迹。
    /// </summary>
    /// <param name="other">被碰到的碰撞体</param>
    /// <param name="weaponVelocity">武器速度向量</param>
    public void OnWeaponHit(Collider other, Vector3 weaponVelocity)
    {
        if (_currentState == EnemyState.KnockedDown)
        {
            Debug.Log($"[死士武器] 碰撞忽略：自己已死亡 | 碰到={other.name}({other.tag})");
            return;
        }

        Debug.Log($"[死士武器] 检测到碰撞 | 碰到={other.name}({other.tag}) | 速度={weaponVelocity.magnitude:F2}");

        // 碰到玩家身体关键点位 → 玩家受伤
        if (other.CompareTag("PlayerVital"))
        {
            Debug.Log("[死士武器] ✅ 命中玩家要害(PlayerVital)！准备扣玩家血...");
            var player = PlayerCombatController.Instance;
            if (player != null)
            {
                player.TakeDamageFromEnemy();
            }
            else
            {
                Debug.LogError("[死士武器] ❌ PlayerCombatController.Instance 为空！");
            }
        }
        else
        {
            Debug.Log($"[死士武器] 忽略：Tag不匹配 ({other.tag} != PlayerVital)");
        }
    }

    // ============================================================
    // 状态进入方法
    // ============================================================

    /// <summary>进入攻击状态：随机播放两种攻击动画中的一种，并循环攻击</summary>
    private void EnterState_Attack()
    {
        Debug.Log($"[死士] → Attack 状态 (HP={currentHP}/{maxHP})");
        _currentState = EnemyState.Attack;
        int attackType = Random.Range(0, 2);
        anim.SetInteger("AttackIndex", attackType);
        SafeResetTrigger("DoHitBack");
        SafeResetTrigger("DoStagger");
        SafeResetTrigger("DoKnockedDown");
        anim.SetTrigger("DoAttack");

        // 等待 attackLoopInterval 秒后再次攻击（如果还在 Attack 状态）
        StartCoroutine(AttackLoopRoutine());
    }

    IEnumerator AttackLoopRoutine()
    {
        yield return new WaitForSeconds(attackLoopInterval);
        if (_currentState == EnemyState.Attack)
            EnterState_Attack();
    }

    /// <summary>进入迟滞状态：播放弹开/硬直动画，定时结束后恢复攻击</summary>
    private void EnterState_Stagger()
    {
        Debug.Log($"[死士] → Stagger 状态 ({staggerDuration}s 后恢复)");
        _currentState = EnemyState.Stagger;
        SafeResetTrigger("DoAttack");
        SafeResetTrigger("DoHitBack");
        SafeResetTrigger("DoStagger");
        anim.SetTrigger("DoStagger");
        StartCoroutine(StaggerTimer());
    }

    /// <summary>进入受击后退状态：播放击退动画，定时结束后根据受伤等级决定下一状态</summary>
    private void EnterState_HitBack()
    {
        Debug.Log($"[死士] → HitBack 状态 ({(wasLastHitSevere ? "重击" : "轻击")}，{hitBackDuration}s 后恢复) | anim={anim != null} | DoHitBack存在={anim != null && HasParam("DoHitBack")}");
        _currentState = EnemyState.HitBack;

        if (anim != null)
        {
            // ★ 先 Reset 所有触发器，防止旧 Trigger 卡住导致新 Trigger 不生效
            SafeResetTrigger("DoAttack");
            SafeResetTrigger("DoHitBack");
            SafeResetTrigger("DoStagger");
            anim.SetTrigger("DoHitBack");
        }
        else
        {
            Debug.LogError("[死士] ❌ Animator 为空！无法播放 HitBack 动画。");
        }

        StartCoroutine(HitBackTimer());
    }

    /// <summary>进入受击倒地状态：播放倒地动画，通知 CombatManager 结算胜利</summary>
    private void EnterState_KnockedDown()
    {
        Debug.Log($"[死士] → KnockedDown 状态 —— 死亡！");
        _currentState = EnemyState.KnockedDown;
        SafeResetTrigger("DoAttack");
        SafeResetTrigger("DoHitBack");
        SafeResetTrigger("DoStagger");
        anim.SetTrigger("DoKnockedDown");

        // 通知全局战斗管理器 —— 玩家赢了
        if (CombatManager.Instance != null)
            CombatManager.Instance.WinCombat();
    }

    // ============================================================
    // 状态超时协程
    // ============================================================

    IEnumerator StaggerTimer()
    {
        yield return new WaitForSeconds(staggerDuration);
        // 如果还未被打断或打死，回到攻击
        if (_currentState == EnemyState.Stagger)
            EnterState_Attack();
    }

    IEnumerator HitBackTimer()
    {
        yield return new WaitForSeconds(hitBackDuration);
        if (_currentState == EnemyState.KnockedDown) yield break;

        // 受击后退结束后的状态转移：
        // - 重击（沿最佳轨迹命中）→ 进入迟滞（Swing stagger）
        // - 轻击 → 直接恢复攻击（像疯狗一样继续砍）
        if (wasLastHitSevere)
        {
            EnterState_Stagger();
        }
        else
        {
            EnterState_Attack();
        }
    }

    // ============================================================
    // 玩家交互方法
    // ============================================================

    /// <summary>
    /// 由 EnemyAssault 在玩家跑出 chaseRange 时调用，重置战斗状态以便后续重新进入。
    /// </summary>
    public void OnChaseStart()
    {
        // 重置战斗状态，取消所有正在播放的特效/协程
        _currentState = EnemyState.Attack;
        playerTransform = null; // 隐藏轨迹指引线
        StopAllCoroutines();
    }
    /// 由 PlayerCombatController.OnWeaponHit 在检测到武器碰撞时调用。
    /// </summary>
    public void OnBlocked()
    {
        // 仅在攻击状态下能被格挡
        if (_currentState != EnemyState.Attack) return;

        EnterState_Stagger();
    }

    /// <summary>
    /// 受到玩家的有效攻击（武器击中身体要害）。
    /// 由 PlayerCombatController.OnWeaponHit 在检测到要害命中时调用。
    /// </summary>
    /// <param name="isSevere">是否重击（玩家沿最佳轨迹挥砍）</param>
    public void TakeDamage(bool isSevere)
    {
        Debug.Log($"[死士] TakeDamage 被调用 | isSevere={isSevere} | 当前状态={_currentState} | HP={currentHP}/{maxHP} | 距上次受伤={Time.time - lastDamageTime:F2}s");

        // 已经死了就不处理
        if (_currentState == EnemyState.KnockedDown) { Debug.Log("[死士] 忽略：已死亡"); return; }

        // ★ 防止一刀多判（同一刀碰到多个碰撞体触发多次 OnTriggerEnter）
        if (Time.time - lastDamageTime < 0.3f) { Debug.Log($"[死士] 忽略：冷却中 ({Time.time - lastDamageTime:F2}s < 0.3s)"); return; }
        lastDamageTime = Time.time;

        // ★ HitBack 中再被砍 → 先扣血再判定，HP>0 就不死，恢复追击
        if (_currentState == EnemyState.HitBack)
        {
            wasLastHitSevere = isSevere;
            float comboDamage = isSevere ? heavyDamage : lightDamage;
            currentHP -= comboDamage;
            Debug.Log($"<color=orange>[死士]</color> HitBack 中再被击中！扣血 {comboDamage}，剩余 {currentHP}/{maxHP}");

            if (currentHP <= 0)
            {
                EnterState_KnockedDown();
            }
            else
            {
                Debug.Log($"[死士] HP>0，不倒地，恢复追击！");
                _currentState = EnemyState.Attack;
                StopAllCoroutines();
                if (enemyAssault != null && playerTransform != null)
                    enemyAssault.ResumeChasing(playerTransform);
            }
            return;
        }

        // 记录受伤等级，供 HitBack 结束后的状态转移使用
        wasLastHitSevere = isSevere;

        // 扣血（隐形血条，Inspector 不可见）
        float damage = isSevere ? heavyDamage : lightDamage;
        currentHP -= damage;

        Debug.Log($"<color=red>[死士]</color> 受到 {(isSevere ? "重击" : "轻击")}，扣血 {damage}，剩余血量 {currentHP}/{maxHP}");

        // 同时输出双方血量对比
        if (PlayerCombatController.Instance != null)
            Debug.Log($"<color=cyan>[血量]</color> 玩家: {PlayerCombatController.Instance.CurrentHP}/{PlayerCombatController.Instance.maxHP} | 敌人: {currentHP}/{maxHP}");

        if (currentHP <= 0)
        {
            EnterState_KnockedDown();
        }
        else
        {
            // 进入受击后退状态
            EnterState_HitBack();
        }
    }

    /// <summary>
    /// 获取敌人当前状态下的最佳应对轨迹方向。
    /// 供 PlayerCombatController 做轨迹匹配判定，和 TrajectoryGuide 画指引线。
    /// </summary>
    public Vector3 GetCurrentOptimalDirection()
    {
        switch (_currentState)
        {
            case EnemyState.Attack:  return optimalDir_Attack;
            case EnemyState.Stagger: return optimalDir_Stagger;
            case EnemyState.HitBack: return optimalDir_HitBack;
            default:                 return Vector3.right;
        }
    }

    /// <summary>
    /// 获取当前状态的最佳弧线弯曲角度。
    /// </summary>
    public float GetCurrentOptimalArc()
    {
        switch (_currentState)
        {
            case EnemyState.Attack:  return optimalArc_Attack;
            case EnemyState.Stagger: return optimalArc_Stagger;
            case EnemyState.HitBack: return optimalArc_HitBack;
            default:                 return 0f;
        }
    }

    // ============================================================
    // Animation Event 方法
    // ============================================================

    /// <summary>
    /// 供攻击动画在刀刃挥出帧调用。
    /// 新版战斗系统中，敌兵武器击中玩家由 WeaponHitDetector → OnWeaponHit 自动处理。
    /// 此方法保留作为保底事件发射通道（UnityEvent 方式）。
    /// </summary>
    public void HitPlayer()
    {
        onPlayerHit?.Invoke();
    }

    /// <summary>开启武器拖尾（Animation Event 调用）</summary>
    public void EnableWeaponTrail()
    {
        Debug.Log("[拖尾] EnableWeaponTrail 被调用" + (weaponTrailController == null ? "，但 weaponTrailController 为空！" : ""));
        if (weaponTrailController != null)
            weaponTrailController.EnableTrail();
    }

    /// <summary>关闭武器拖尾（Animation Event 调用）</summary>
    public void DisableWeaponTrail()
    {
        if (weaponTrailController != null)
            weaponTrailController.DisableTrail();
    }

    /// <summary>重置武器拖尾（Animation Event 调用）</summary>
    public void ResetWeaponTrail()
    {
        if (weaponTrailController != null)
            weaponTrailController.ResetTrail();
    }

    /// <summary>安全 Reset Trigger（参数不存在时静默跳过，不报错）</summary>
    void SafeResetTrigger(string paramName)
    {
        if (anim == null) return;
        foreach (var p in anim.parameters)
            if (p.name == paramName) { anim.ResetTrigger(paramName); return; }
    }

    /// <summary>检查 Animator 中是否存在指定参数</summary>
    bool HasParam(string paramName)
    {
        if (anim == null) return false;
        foreach (var p in anim.parameters)
            if (p.name == paramName) return true;
        return false;
    }
}
