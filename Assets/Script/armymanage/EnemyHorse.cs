using UnityEngine;
using UnityEngine.AI;

public class EnemyHorse : MonoBehaviour
{
    [SerializeField] private Animator animator;
    public NavMeshAgent agent;
    private bool isDead = false;

    [Header("行军参数")]
    [Tooltip("马匹的进攻目标位置（通常指向城墙方向）")]
    public Transform targetDestination;
    [Tooltip("马匹冲锋速度")]
    public float chargeSpeed = 5f;
    [Tooltip("马匹加速度")]
    public float chargeAcceleration = 8f;

    [Header("中枢神经")]
    // 自动寻找全局的攻城压制指挥部
    private SiegeManager globalSiegeManager;

    [Header("抵达行为")]
    [Tooltip("距目标多近算到达城墙")]
    public float stoppingDistance = 3f;

    void Start()
    {
        // 🐴 设定马匹冲锋参数（比步兵更快更猛）
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
        agent.speed = chargeSpeed;
        agent.acceleration = chargeAcceleration;
        agent.stoppingDistance = stoppingDistance;

        // 🐴 补发命令：receiveorders 可能在 Start 之前被调用（agent 那时为 null）
        // 如果已经有目标了，趁 Start 里 agent 就绪赶紧设上
        if (targetDestination != null && agent.isOnNavMesh)
        {
            agent.SetDestination(targetDestination.position);
        }

        // 落地第一件事：找到战场上的旧版压制指挥部，存入脑海
        globalSiegeManager = FindObjectOfType<SiegeManager>();

        // 🌟 【新增核心】：出生登记！向新版波次总导演报到，场上总兵力 +1！
        if (BattleDirector.Instance != null)
        {
            BattleDirector.Instance.currentActiveEnemies++;
        }
    }

    void Update()
    {
        if (isDead || agent == null) return;

        // 🐴 还没设路线但有目标 → 重试（agent 可能延迟才踩上 NavMesh）
        if (targetDestination != null && agent.isOnNavMesh && !agent.hasPath)
        {
            agent.SetDestination(targetDestination.position);
        }

        // 🐴 抵达判定：到了就消失
        if (targetDestination != null && agent.isOnNavMesh && !agent.pathPending)
        {
            float dist = Vector3.Distance(transform.position, targetDestination.position);
            if (dist <= stoppingDistance)
            {
                Destroy(gameObject);
            }
        }
    }

    // 接收导演的进攻指令
    public void receiveorders(Transform assignedTarget)
    {
        //Debug.Log("🎯 骑兵收到进攻指令，目标是：" + assignedTarget.name);
        targetDestination = assignedTarget;
        
        if (agent != null && targetDestination != null)
        {
            // 立刻朝着指定的城墙进军！
            agent.SetDestination(targetDestination.position);
        }
    }

    // 当有其他带有 Trigger 碰撞体的物体碰到自己时触发
    void OnTriggerEnter(Collider other)
    {
        // 如果自己还没死，且碰自己的是“箭” (注意：大小写要和 Unity 里设置的 Tag 绝对一致！)
        if (!isDead && other.CompareTag("Arrow"))
        {
            Die();

            // 将箭矢作为子物体绑定在敌人身上（模拟插在身上的效果），并停止箭的物理运动
            other.transform.SetParent(this.transform);
            Rigidbody arrowRb = other.GetComponent<Rigidbody>();
            if (arrowRb != null)
            {
                arrowRb.isKinematic = true; // 关掉箭的物理，避免插在身上乱晃消耗性能
            }
        }
    }

    void Die()
    {
        isDead = true;

        // === 1. 触发《孤城》核心压制机制 ===
        if (globalSiegeManager != null)
        {
            globalSiegeManager.TriggerSuppressingFire();
            // Debug.Log("🎯 命中死士！全军推进受挫！");
        }

        // === 2. 掐断大军的”蜂群大脑” ===
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }

        // === 3. 物理冻结：防止尸体因重力 + 碰撞弹跳 ===
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // === 4. 停止死亡后多余的微运动，避免干扰倒地动画 ===
        MicroMotion micro = GetComponent<MicroMotion>();
        if (micro != null)
        {
            micro.enabled = false;
        }

        // === 5. 触发死亡倒地动画 ===
        animator.SetTrigger("Dead");

        // === 6. 2 秒后销毁尸体，回收 Pico 4 性能 ===
        Destroy(gameObject, 2f);
    }

    // 🌟 【新增核心】：死亡销号！
    // 无论是被箭射死后过了 2 秒被 Destroy，还是游戏机制把他强制 Destroy
    // 只要模型一消失，立刻从总导演的账本里注销，腾出兵力名额！
    void OnDestroy()
    {
        // 加个防错判定，防止游戏直接关闭时，总导演先被销毁而报错
        if (BattleDirector.Instance != null)
        {
            BattleDirector.Instance.currentActiveEnemies--;
        }
    }
}
