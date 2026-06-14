using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using System.Collections;
using System;

public class EnemyAssault : MonoBehaviour
{
    public static event EventHandler onClimbEnd;

    public enum EnemyState { Idle, MarchingToWall, RunningToLadder, Climbing, ClimbingEnd, ChasingPlayer, Attacking }

    public EnemyState currentState = EnemyState.Idle;

    [Header("战斗引用")]
    public Transform playerTransform;
    public WeaponTrailController weaponTrailController;

    [Header("死士事件")]
    /// <summary>攻击到玩家时触发（由 HitPlayer() 方法发射，可在 Animation Event 中调用）</summary>
    public UnityEvent onPlayerHit;

    //  【新增】：移动速度控制面板
    [Header("移动与攀爬参数")]
    [Tooltip("爬上城墙后追玩家的速度")]
    public float runSpeed = 4f;
    public float climbSpeed = 2f;
    public float climbEndTime = 1.5f;
    public float attackDistance = 1.5f;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Animator anim;
    private Vector3 originalScale;

    private Transform myBottomAnchor;

    [Header(" 行军配置（开战即出发）")]
    [Tooltip("死士一开始要行军的目标（城墙位置），由 BattleDirector 在开战时下发")]
    public Transform marchTargetWall;

    //  要跟随的指挥官——死士跟着他走就不会提前跑到城墙
    private SquadCommander followCommander;

    //  诊断用：控制打印频率
    private float lastPosLogTime = -10f;
    private const float POS_LOG_INTERVAL = 2f;

    /// <summary>
    /// 初始化死士的运行依赖和初始状态。
    /// </summary>
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();

        Debug.Log($" [死士Start] {name} 初始位置={transform.position} 原始缩放={transform.localScale} 状态={currentState}");

        agent.enabled = false;

        //  绝杀补丁：锁死物理引擎！不准往下掉！
        rb.isKinematic = true;

        originalScale = transform.localScale;
        transform.localScale = Vector3.zero;

        if (weaponTrailController == null)
        {
            Debug.LogError(" 致命错误：死士没有绑定武器拖尾控制器！请去 Inspector 把 WeaponTrailController 拖给 EnemyAssault！");
        }
        else
        {
            weaponTrailController.ResetTrail();
        }
    }

    /// <summary>
    /// 驱动死士的寻路、攀爬和攻击状态更新。
    /// </summary>
    void Update()
    {
        switch (currentState)
        {
            case EnemyState.MarchingToWall:
                if (agent.enabled && agent.isOnNavMesh)
                {
                    //  修复：如果有指挥官，就跟着指挥官走（不提前跑去城墙）
                    if (followCommander != null)
                    {
                        agent.SetDestination(followCommander.transform.position);
                        // 匹配指挥官速度，保持同步
                        if (followCommander.agent != null)
                            agent.speed = followCommander.agent.speed;
                    }
                    else if (marchTargetWall != null)
                    {
                        // 保底：没有指挥官就自己去城墙（旧逻辑）
                        agent.SetDestination(marchTargetWall.position);
                    }

                    //  每2秒打一次行军位置
                    // if (Time.time - lastPosLogTime >= POS_LOG_INTERVAL)
                    // {
                    //     lastPosLogTime = Time.time;
                    //     string target = followCommander != null ? $"指挥官[{followCommander.name}]" : $"城墙({(marchTargetWall != null ? marchTargetWall.position.ToString("F1") : "无")})";
                    //     Debug.Log($" [死士行军] {name} 位置={transform.position:F1} 跟随目标={target} isOnNavMesh={agent.isOnNavMesh} speed={agent.speed}");
                    // }
                }
                break;

            case EnemyState.RunningToLadder:
                if (agent.enabled && agent.isOnNavMesh && myBottomAnchor != null)
                {
                    agent.SetDestination(myBottomAnchor.position);

                    //  保底：如果距离梯子底足够近但 OnTriggerEnter 没触发（比如 Collider 没对齐），
                    // 直接强行进入攀爬状态
                    Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
                    Vector3 flatAnchor = new Vector3(myBottomAnchor.position.x, 0f, myBottomAnchor.position.z);
                    if (Vector3.Distance(flatPos, flatAnchor) <= agent.stoppingDistance + 0.5f)
                    {
                        Debug.Log(" [保底] 距离梯子足够近，直接攀爬！");
                        StartClimbing();
                    }
                }
                break;

            case EnemyState.Climbing:
                transform.Translate(Vector3.up * climbSpeed * Time.deltaTime);
                break;

            case EnemyState.ChasingPlayer:
                //  修复点 2：追逐玩家时也要加这个判断
                if (agent.enabled && agent.isOnNavMesh && playerTransform != null)
                {
                    //  将目标压低到地面高度（忽略 Y 轴），避免因相机在头顶导致寻路目标悬空
                    Vector3 floorTarget = new Vector3(
                        playerTransform.position.x,
                        transform.position.y,
                        playerTransform.position.z
                    );
                    agent.SetDestination(floorTarget);

                    //  水平距离判断（忽略 Y 轴），防止玩家身高/眼高导致永远达不到攻击距离
                    Vector3 from = new Vector3(transform.position.x, 0f, transform.position.z);
                    Vector3 to   = new Vector3(playerTransform.position.x, 0f, playerTransform.position.z);
                    if (Vector3.Distance(from, to) <= attackDistance)
                        StartAttack();
                }
                break;

            case EnemyState.Attacking:
                if (playerTransform != null)
                {
                    Vector3 lookDir = playerTransform.position - transform.position;
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero)
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 5f);
                }
                break;
        }
    }

    //  导演调用的出击指令
    /// <summary>
    /// 接收导演指令，激活死士并让其冲向指定梯子。
    /// 如果死士已经在行军状态，则直接切换目标到梯子（不重复启用组件）。
    /// </summary>
    public void StartAssaultWithTarget(Transform anchor)
    {
        myBottomAnchor = anchor;

        // 把 stoppingDistance 设小，确保踩到 BottomTrigger
        if (currentState == EnemyState.MarchingToWall)
        {
            //  已经在行军了（跟着指挥官），保持当前速度直接 redirect 去梯子
            currentState = EnemyState.RunningToLadder;
            agent.stoppingDistance = 0.1f;
            anim.SetTrigger("Run");
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(myBottomAnchor.position);
            }

            Debug.Log(" 死士收到梯子信号，转向梯子！");
            return;
        }

        // 从 Idle 唤醒（之前 StopAndWait 待命中）
        transform.localScale = originalScale;

        currentState = EnemyState.RunningToLadder;
        rb.isKinematic = false;
        agent.enabled = true;

        agent.stoppingDistance = 0.1f;
        agent.acceleration = 60f;

        anim.SetTrigger("Run");
        Debug.Log(" 死士出击，目标锁定梯子！");

        //  修复点 3：唤醒的瞬间如果他还没踩实网格，就不强行发命令（Update 循环会等他踩实了再发）
        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(myBottomAnchor.position);
        }
        else
        {
            Debug.LogWarning(" 死士已现身，但双脚暂未接触寻路网格，正在等待物理吸附...");
        }
    }

    /// <summary>
    ///  让死士跟随指定的指挥官行军（而不是自己跑去找城墙）。
    /// 由 BattleDirector 在第一波生成时调用。
    /// </summary>
    public void ActivateAndFollow(SquadCommander commander)
    {
        followCommander = commander;
        marchTargetWall = null; // 不自己跑去城墙

        Debug.Log($" [死士Activate] 跟随指挥官 [{commander.name}] 当前位置={commander.transform.position} 速度={commander.agent.speed}");

        // 从隐藏状态现身
        transform.localScale = originalScale;

        currentState = EnemyState.MarchingToWall;
        rb.isKinematic = false;
        agent.enabled = true;
        agent.speed = commander.agent.speed; // 匹配指挥官速度
        agent.acceleration = 60f;

        //  先在指挥官旁边找位置生成，而不是从远处跑过来
        Vector3 spawnPos = FindSpawnNearCommander(commander);
        if (spawnPos != transform.position)
        {
            agent.Warp(spawnPos);
        }

        // 设置目的地为指挥官位置，跟随行军
        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(commander.transform.position);
            Debug.Log($" 死士在指挥官旁边[{spawnPos:F1}]现身，跟随行军！");
        }
        else
        {
            Debug.LogWarning(" 死士现身位置周围没有 NavMesh，等待 Update 循环重试...");
        }

        anim.SetTrigger("Run");
    }

    /// <summary>
    ///  在指挥官周围半径 3~6 米的随机位置找一个 NavMesh 点作为死士的出生点。
    /// </summary>
    private Vector3 FindSpawnNearCommander(SquadCommander commander)
    {
        Vector3 commanderPos = commander.transform.position;
        float baseRadius = 3f;
        float maxRadius = 6f;

        // 尝试 5 次，找有效 NavMesh 位置
        for (int i = 0; i < 5; i++)
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * UnityEngine.Random.Range(baseRadius, maxRadius);
            Vector3 candidate = commanderPos + new Vector3(randomCircle.x, 0f, randomCircle.y);

            NavMeshHit navHit;
            if (NavMesh.SamplePosition(candidate, out navHit, 2f, NavMesh.AllAreas))
            {
                return navHit.position;
            }
        }

        // 保底：直接放在指挥官位置
        Debug.LogWarning(" 死士找不到指挥官周围的 NavMesh 点，直接放在指挥官位置");
        return commanderPos;
    }

    /// <summary>
    ///  指挥官到达城墙开始架梯子时调用 —— 让死士原地待命，
    /// 不再继续往城墙走，等梯子架好后再被 StartAssaultWithTarget 唤醒。
    /// </summary>
    public void StopAndWait()
    {
        if (currentState == EnemyState.MarchingToWall)
        {
            Debug.Log($" [死士停止] {name} 收到停止指令，原地待命等待梯子（位置={transform.position:F1}）");
            currentState = EnemyState.Idle;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }
        else
        {
            Debug.Log($" [死士停止] {name} 收到停止指令但状态={currentState}，忽略");
        }
    }

    /// <summary>
    ///  保底：直接向城墙行军（当没有指挥官可跟随时使用）。
    /// </summary>
    public void StartMarching(Transform wallTarget)
    {
        marchTargetWall = wallTarget;
        followCommander = null;

        //  诊断：记录出征时的所有状态
        NavMeshHit navHit;
        bool onNavMesh = NavMesh.SamplePosition(transform.position, out navHit, 10f, NavMesh.AllAreas);
        Debug.Log($" [死士出征] {name} 被调用 StartMarching(保底)! wallTarget={(wallTarget != null ? wallTarget.name + " pos=" + wallTarget.position.ToString() : "NULL")} " +
                  $"当前状态={currentState} 当前位置={transform.position} agent启用={agent.enabled} " +
                  $"isOnNavMesh={agent.isOnNavMesh} 附近有NavMesh={onNavMesh} 最近NavMesh点={(onNavMesh ? navHit.position.ToString() : "无")}");

        if (!agent.isOnNavMesh && onNavMesh)
        {
            agent.Warp(navHit.position);
            Debug.Log($" [死士出征] Warp到最近NavMesh点: {navHit.position}");
        }

        transform.localScale = originalScale;
        currentState = EnemyState.MarchingToWall;
        rb.isKinematic = false;
        agent.enabled = true;
        agent.speed = runSpeed;
        agent.acceleration = 60f;
        anim.SetTrigger("Run");

        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(marchTargetWall.position);
            Debug.Log(" 死士随大军出征(保底)，目标城墙！");
        }
        else
        {
            Debug.LogWarning(" 死士出征，但暂未踩实 NavMesh，Update 会等踩实再走...");
        }
    }

    /// <summary>
    ///  进入攀爬状态 —— 抽出来给 OnTriggerEnter 和保底检测共用
    /// </summary>
    void StartClimbing()
    {
        Debug.Log(" 抵达梯子，校准姿态并开爬！");
        currentState = EnemyState.Climbing;

        agent.isStopped = true;
        agent.enabled = false;

        rb.isKinematic = true;
        anim.SetTrigger("Climb");

        if (myBottomAnchor != null)
        {
            transform.rotation = Quaternion.LookRotation(myBottomAnchor.forward, myBottomAnchor.up);
            transform.position = new Vector3(myBottomAnchor.position.x, transform.position.y, myBottomAnchor.position.z);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("BottomTrigger") && currentState == EnemyState.RunningToLadder)
        {
            StartClimbing();
        }

        if (other.CompareTag("TopTrigger") && currentState == EnemyState.Climbing)
        {
            StartCoroutine(HandleClimbEnd(other.transform));
        }
    }

    /// <summary>
    /// 处理翻越城墙结束后的落地和追击恢复。
    /// </summary>
    IEnumerator HandleClimbEnd(Transform topAnchor)
    {
        Debug.Log(" 翻越垛口...");
        currentState = EnemyState.ClimbingEnd;
        anim.SetTrigger("ClimbEnd");

        yield return new WaitForSeconds(climbEndTime);

        transform.position = topAnchor.position;
        transform.rotation = topAnchor.rotation;

        rb.isKinematic = false;
        agent.enabled = true;

        // 翻墙落地后，恢复追杀速度
        agent.speed = runSpeed;
        agent.acceleration = 60f;
        agent.isStopped = false;

        currentState = EnemyState.ChasingPlayer;
        anim.SetTrigger("Run");

        //  翻墙完成，通知外部（ForceDrop 弩、拿起刀等）
        onClimbEnd?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 让死士进入攻击状态并触发攻击动画。
    /// </summary>
    void StartAttack()
    {
        Debug.Log(" 纳命来！");
        currentState = EnemyState.Attacking;

        agent.isStopped = true; // 寻路猛踩刹车
        anim.SetTrigger("Attack");
        //  不再自动恢复追击 —— 击中后直接进入结算
    }

    [Header("死士攻击验证")]
    [Tooltip("HitPlayer 时水平距离允许比攻击距离大多少（米），弥补武器挥砍前摇的位移")]
    public float hitPlayerDistanceBuffer = 0.8f;

    /// <summary>
    /// 供攻击动画在刀刃挥出帧调用 —— 触发击中判定。
    /// 【保底方案】水平距离验证：忽略 Y 轴高度差，防止因玩家相机高度异常导致攻击失效。
    /// </summary>
    public void HitPlayer()
    {
        // 保底：如果 playerTransform 未赋值或已失效，自动找主相机
        if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                playerTransform = cam.transform;
                Debug.Log("[EnemyAssault] 自动补获 playerTransform → Camera.main");
            }
        }

        if (playerTransform != null)
        {
            // 水平距离判断（忽略 Y 轴，只看 XZ 平面）
            Vector3 from = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 to   = new Vector3(playerTransform.position.x, 0f, playerTransform.position.z);
            float horizDist = Vector3.Distance(from, to);

            float threshold = attackDistance + hitPlayerDistanceBuffer;
            if (horizDist <= threshold)
            {
                Debug.Log($" 死士命中玩家！（水平距离 {horizDist:F2} ≤ {threshold:F2}）");
                onPlayerHit?.Invoke();
            }
            else
            {
                Debug.LogWarning($" 死士攻击被阻拦：水平距离 {horizDist:F2} > {threshold:F2}，忽略此次攻击");
            }
        }
        else
        {
            Debug.LogError(" 死士 playerTransform 为空且无法自动找到玩家！");
        }
    }

    /// <summary>
    /// 在攻击结束后恢复追击状态。
    /// </summary>
    IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(3f);
        Debug.Log(" 砍完了，继续追！");

        ResetWeaponTrail();
        currentState = EnemyState.ChasingPlayer;
        agent.isStopped = false; // 松开刹车继续追
        anim.SetTrigger("Run");
    }

    /// <summary>
    /// 供攻击动画事件调用，开启武器拖尾。
    /// </summary>
    public void EnableWeaponTrail()
    {
        if (weaponTrailController == null)
        {
            Debug.LogError(" 致命错误：死士没有绑定武器拖尾控制器！请去 Inspector 把 WeaponTrailController 拖给 EnemyAssault！");
            return;
        }

        weaponTrailController.EnableTrail();
    }

    /// <summary>
    /// 供攻击动画事件调用，关闭武器拖尾发射。
    /// </summary>
    public void DisableWeaponTrail()
    {
        if (weaponTrailController == null)
        {
            Debug.LogError(" 致命错误：死士没有绑定武器拖尾控制器！请去 Inspector 把 WeaponTrailController 拖给 EnemyAssault！");
            return;
        }

        weaponTrailController.DisableTrail();
    }

    /// <summary>
    /// 供攻击动画事件调用或状态收尾时调用，重置武器拖尾，停掉并清空残留尾迹
    /// </summary>
    public void ResetWeaponTrail()
    {
        if (weaponTrailController == null)
        {
            Debug.LogError(" 致命错误：死士没有绑定武器拖尾控制器！请去 Inspector 把 WeaponTrailController 拖给 EnemyAssault！");
            return;
        }

        weaponTrailController.ResetTrail();
    }
}
