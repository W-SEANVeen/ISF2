using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System;

public class EnemyAssault : MonoBehaviour
{
    public static event EventHandler onClimbEnd;

    public enum EnemyState { Idle, MarchingToWall, RunningToLadder, Climbing, ClimbingEnd, ChasingPlayer, InCombat }

    public EnemyState currentState = EnemyState.Idle;

    [Header("战斗引用")]
    public Transform playerTransform;

    /// <summary>战斗模块，由 EnemyAssault 在靠近玩家时接管</summary>
    private EnemyCombatController combatBrain;

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

    [Header("行军配置（开战即出发）")]
    [Tooltip("死士一开始要行军的目标（城墙位置），由 BattleDirector 在开战时下发")]
    public Transform marchTargetWall;

    private SquadCommander followCommander;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();
        combatBrain = GetComponent<EnemyCombatController>();

        // 自动找玩家：先找 Camera.main，再找 Player Tag
        if (playerTransform == null)
        {
            var cam = Camera.main;
            if (cam != null)
                playerTransform = cam.transform;
            else
            {
                var go = GameObject.FindWithTag("Player");
                if (go != null)
                    playerTransform = go.transform;
                else
                    Debug.LogWarning("EnemyAssault: 找不到玩家对象，请确保场景有 MainCamera 或 Player Tag");
            }
        }

        Debug.Log($" [死士Start] {name} 初始位置={transform.position} 原始缩放={transform.localScale} 状态={currentState}");

        agent.enabled = false;
        rb.isKinematic = true;

        originalScale = transform.localScale;
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        switch (currentState)
        {
            case EnemyState.MarchingToWall:
                if (agent.enabled && agent.isOnNavMesh)
                {
                    if (followCommander != null)
                    {
                        agent.SetDestination(followCommander.transform.position);
                        if (followCommander.agent != null)
                            agent.speed = followCommander.agent.speed;
                    }
                    else if (marchTargetWall != null)
                    {
                        agent.SetDestination(marchTargetWall.position);
                    }
                }
                break;

            case EnemyState.RunningToLadder:
                if (agent.enabled && agent.isOnNavMesh && myBottomAnchor != null)
                {
                    agent.SetDestination(myBottomAnchor.position);

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
                if (agent.enabled && agent.isOnNavMesh && playerTransform != null)
                {
                    Vector3 floorTarget = new Vector3(
                        playerTransform.position.x,
                        transform.position.y,
                        playerTransform.position.z
                    );
                    agent.SetDestination(floorTarget);

                    Vector3 from = new Vector3(transform.position.x, 0f, transform.position.z);
                    Vector3 to   = new Vector3(playerTransform.position.x, 0f, playerTransform.position.z);
                    if (Vector3.Distance(from, to) <= attackDistance)
                    {
                        currentState = EnemyState.InCombat;
                        agent.isStopped = true;
                        agent.enabled = false;
                        rb.isKinematic = true; // 锁死物理，防止被武器顶飞

                        if (combatBrain != null)
                            combatBrain.BeginCombatPhase(playerTransform);
                    }
                }
                break;

            case EnemyState.InCombat:
                // 战斗逻辑由 EnemyCombatController 独立处理
                break;
        }
    }

    public void StartAssaultWithTarget(Transform anchor)
    {
        myBottomAnchor = anchor;

        if (currentState == EnemyState.MarchingToWall)
        {
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

        transform.localScale = originalScale;

        currentState = EnemyState.RunningToLadder;
        rb.isKinematic = false;
        agent.enabled = true;

        agent.stoppingDistance = 0.1f;
        agent.acceleration = 60f;

        anim.SetTrigger("Run");
        Debug.Log(" 死士出击，目标锁定梯子！");

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

    public void ActivateAndFollow(SquadCommander commander)
    {
        followCommander = commander;
        marchTargetWall = null;

        Debug.Log($" [死士Activate] 跟随指挥官 [{commander.name}] 当前位置={commander.transform.position} 速度={commander.agent.speed}");

        transform.localScale = originalScale;

        currentState = EnemyState.MarchingToWall;
        rb.isKinematic = false;
        agent.enabled = true;
        agent.speed = commander.agent.speed;
        agent.acceleration = 60f;

        Vector3 spawnPos = FindSpawnNearCommander(commander);
        if (spawnPos != transform.position)
        {
            agent.Warp(spawnPos);
        }

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

    private Vector3 FindSpawnNearCommander(SquadCommander commander)
    {
        Vector3 commanderPos = commander.transform.position;
        float baseRadius = 3f;
        float maxRadius = 6f;

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

        Debug.LogWarning(" 死士找不到指挥官周围的 NavMesh 点，直接放在指挥官位置");
        return commanderPos;
    }

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

    public void StartMarching(Transform wallTarget)
    {
        marchTargetWall = wallTarget;
        followCommander = null;

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

        // 落脚后 warp 到 NavMesh 地面，防止浮空
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(transform.position, out navHit, 2f, NavMesh.AllAreas))
            agent.Warp(navHit.position);

        agent.speed = runSpeed;
        agent.acceleration = 60f;
        agent.isStopped = false;

        currentState = EnemyState.ChasingPlayer;
        anim.SetTrigger("Run");

        onClimbEnd?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 由 EnemyCombatController 在玩家跑出 chaseRange 时调用，
    /// 切换回追人模式，等到玩家靠近再重新进入战斗。
    /// </summary>
    public void ResumeChasing(Transform player)
    {
        Debug.Log(" [死士] 玩家跑远了，追击！");
        currentState = EnemyState.ChasingPlayer;
        playerTransform = player;

        // 重置战斗控制器的内部状态
        if (combatBrain != null)
            combatBrain.OnChaseStart();

        agent.enabled = true;
        agent.isStopped = false;
        agent.speed = runSpeed;
        agent.acceleration = 60f;
        rb.isKinematic = false; // 恢复物理，让 NavMeshAgent 能控制移动

        if (agent.isOnNavMesh)
        {
            Vector3 floorTarget = new Vector3(
                playerTransform.position.x,
                transform.position.y,
                playerTransform.position.z
            );
            agent.SetDestination(floorTarget);
        }

        anim.SetTrigger("Run");
    }
}
