using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using System.Collections;
using System;

public class EnemyAssault : MonoBehaviour
{
    public static event EventHandler onClimbEnd;

    public enum EnemyState { Idle, RunningToLadder, Climbing, ClimbingEnd, ChasingPlayer, Attacking }

    public EnemyState currentState = EnemyState.Idle;

    [Header("战斗引用")]
    public Transform playerTransform;
    public WeaponTrailController weaponTrailController;

    [Header("死士事件")]
    /// <summary>攻击到玩家时触发（由 HitPlayer() 方法发射，可在 Animation Event 中调用）</summary>
    public UnityEvent onPlayerHit;

    // 🌟 【新增】：移动速度控制面板
    [Header("移动与攀爬参数")]
    [Tooltip("精英死士冲向梯子和追玩家的速度")]
    public float runSpeed = 4f; 
    public float climbSpeed = 2f;
    public float climbEndTime = 1.5f; 
    public float attackDistance = 1.5f; 

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Animator anim;
    private Vector3 originalScale; 

    private Transform myBottomAnchor;

    /// <summary>
    /// 初始化死士的运行依赖和初始状态。
    /// </summary>
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();

        agent.enabled = false; 

        // 🌟 绝杀补丁：锁死物理引擎！不准往下掉！
        rb.isKinematic = true; 

        originalScale = transform.localScale;
        transform.localScale = Vector3.zero; 

        if (weaponTrailController == null)
        {
            Debug.LogError("❌ 致命错误：死士没有绑定武器拖尾控制器！请去 Inspector 把 WeaponTrailController 拖给 EnemyAssault！");
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
            case EnemyState.RunningToLadder:
                // 🌟 修复点 1：加上 agent.isOnNavMesh，确保他脚踏实地了再发坐标
                if (agent.enabled && agent.isOnNavMesh && myBottomAnchor != null) 
                    agent.SetDestination(myBottomAnchor.position);
                break;

            case EnemyState.Climbing:
                transform.Translate(Vector3.up * climbSpeed * Time.deltaTime);
                break;

            case EnemyState.ChasingPlayer:
                // 🌟 修复点 2：追逐玩家时也要加这个判断
                if (agent.enabled && agent.isOnNavMesh && playerTransform != null) 
                {
                    agent.SetDestination(playerTransform.position);
                    if (Vector3.Distance(transform.position, playerTransform.position) <= attackDistance)
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

    // 🌟 导演调用的出击指令
    /// <summary>
    /// 接收导演指令，激活死士并让其冲向指定梯子。
    /// </summary>
    public void StartAssaultWithTarget(Transform anchor)
    {
        myBottomAnchor = anchor;
        transform.localScale = originalScale; 
        
        currentState = EnemyState.RunningToLadder;
        rb.isKinematic = false;
        agent.enabled = true;

        agent.speed = runSpeed;
        agent.acceleration = 60f;
        
        anim.SetTrigger("Run");
        Debug.Log("🚀 死士出击，目标锁定梯子！");

        // 🌟 修复点 3：唤醒的瞬间如果他还没踩实网格，就不强行发命令（Update 循环会等他踩实了再发）
        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(myBottomAnchor.position);
        }
        else
        {
            Debug.LogWarning("⚠️ 死士已现身，但双脚暂未接触寻路网格，正在等待物理吸附...");
        }
    }
    
    /// <summary>
    /// 处理死士在梯子底部和顶部的状态切换。
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("BottomTrigger") && currentState == EnemyState.RunningToLadder)
        {
            Debug.Log("🧗 抵达梯子，校准姿态并开爬！");
            currentState = EnemyState.Climbing;
            
            // 爬墙前，把寻路关掉并踩死刹车
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
        Debug.Log("🧗 翻越垛口...");
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

        // 🚩 翻墙完成，通知外部（ForceDrop 弩、拿起刀等）
        onClimbEnd?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 让死士进入攻击状态并触发攻击动画。
    /// </summary>
    void StartAttack()
    {
        Debug.Log("⚔️ 纳命来！");
        currentState = EnemyState.Attacking;

        agent.isStopped = true; // 寻路猛踩刹车
        anim.SetTrigger("Attack");
        // 🚩 不再自动恢复追击 —— 击中后直接进入结算
    }

    /// <summary>
    /// 供攻击动画在刀刃挥出帧调用 —— 触发击中判定。
    /// </summary>
    public void HitPlayer()
    {
        Debug.Log("💀 死士命中玩家！");
        onPlayerHit?.Invoke();
    }

    /// <summary>
    /// 在攻击结束后恢复追击状态。
    /// </summary>
    IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(3f);
        Debug.Log("🏃 砍完了，继续追！");
        
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
            Debug.LogError("❌ 致命错误：死士没有绑定武器拖尾控制器！请去 Inspector 把 WeaponTrailController 拖给 EnemyAssault！");
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
            Debug.LogError("❌ 致命错误：死士没有绑定武器拖尾控制器！请去 Inspector 把 WeaponTrailController 拖给 EnemyAssault！");
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
            Debug.LogError("❌ 致命错误：死士没有绑定武器拖尾控制器！请去 Inspector 把 WeaponTrailController 拖给 EnemyAssault！");
            return;
        }

        weaponTrailController.ResetTrail();
    }
}
