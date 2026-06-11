using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class SquadCommander : MonoBehaviour
{
    [Header("绑定指挥部")]
    public SiegeManager siegeManager; 

    [Header("寻路设置")]
    public NavMeshAgent agent;
    public Transform targetWall; // 城墙下的终点
    public float baseSpeed = 2f; // 这支小队的标准行军速度

    [Header("麾下死士名单")]
    public List<NavMeshAgent> squadMembers = new List<NavMeshAgent>();

    // 内部字典：记录每个小兵落地时，相对于指挥官的“专属位置”
    private Dictionary<NavMeshAgent, Vector3> localOffsets = new Dictionary<NavMeshAgent, Vector3>();
    
    // 【新增】：用来记住每个小兵被 Randomizer 赋予的随机初始速度
    private Dictionary<NavMeshAgent, float> personalSpeeds = new Dictionary<NavMeshAgent, float>();

    [Header("梯子控制")]
    public SiegeLadder attachedLadder; // 在面板里把刚刚那个梯子拖进来
    private bool hasArrived = false;   // 防止重复触发

    [Header("梯子与工具人控制")]
    public PorterRetreat portersGroup; // 在这里把搬运工包工头拖进来！

    [Header("🆕 跟随死士控制")]
    [Tooltip("跟随本指挥官的精英死士，到达城墙架梯子时会让其原地待命")]
    public EnemyAssault followingElite;

    public void ReceiveOrders(Transform assignedTarget)
    {
        targetWall = assignedTarget; // 接下锦囊里的目标

        if (agent != null && targetWall != null)
        {
            // 立刻朝着指定的城墙进军！
            agent.SetDestination(targetWall.position);
        }
    }

    void Start()
    {
        // 落地第一件事：只需联系总指挥部获取阻尼即可
        if (siegeManager == null)
        {
            siegeManager = FindObjectOfType<SiegeManager>();
        }
        
        // 🚨 删掉自动找 TargetWall 的代码！不要让他们自己找了！
    }

    void Update()
    {
        if (hasArrived) return; 

        if (siegeManager == null || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        // 1. 核心缝合点：让队长的实时速度，受总指挥部的“阻尼”控制！
        agent.speed = baseSpeed * siegeManager.globalDamping;

        // 2. 让麾下的小兵也受控制，并跟着队长走
        MaintainFormation();

        // 3. 判断是否到达城墙
        // （只要跑到这里，说明 hasArrived 肯定是 false，agent 肯定是开着的）
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            // === 🧪🧪🧪 诊断日志：到达触发 🧪🧪🧪 ===
            float realDistToWall = Vector3.Distance(transform.position, targetWall.position);
            bool pathIsPartial = agent.pathStatus != NavMeshPathStatus.PathComplete;
            Debug.Log($"🧪 [SquadArrive] {name} remDist={agent.remainingDistance:F2} stopDist={agent.stoppingDistance:F2} pathStatus={agent.pathStatus} realDist2Wall={realDistToWall:F2} pathPartial={pathIsPartial}");

            // 🔥🔥🔥 【修复路中间搭梯子】双重校验：
            // ① NavMesh 路径必须是完整的（不全则说明被阻挡截断了）
            // ② 实际距离城墙必须在合理范围内
            if (pathIsPartial || realDistToWall > agent.stoppingDistance + 3f)
            {
                Debug.Log($"🧪 🛑 [SquadArrive] {name} 拦截！还在半路，重新寻路到城墙 targetWall={targetWall.position}");
                // 还在半路/路径被挡，重新下令寻路，绝不在路中间架梯子！
                agent.SetDestination(targetWall.position);
                return;
            }

            Debug.Log($"🧪 ✅ [SquadArrive] {name} 通过校验，真正到达城墙！架设梯子！");

            hasArrived = true; // 立刻上锁！下一帧大门就会被焊死！

            agent.enabled = false; // 关掉寻路引擎

            // 强行把梯子底座摆正，吸附到目标点的坐标和朝向
            transform.position = targetWall.position;
            transform.rotation = targetWall.rotation;

            // 1. 唤醒起重机，开始架梯子
            if (attachedLadder != null)
            {
                attachedLadder.StartErecting();

                // 2. 遣散工具人
                if (portersGroup != null)
                {
                    portersGroup.StartRetreat();
                }

                // 3. 🆕 让跟随的精英死士原地待命，别再往城墙走了
                if (followingElite != null)
                {
                    followingElite.StopAndWait();
                }
            }
            else
            {
                Debug.LogWarning("⚠️ 队长到达了目标点，但没有绑定梯子！请检查指挥官预制体上的 SquadCommander 组件，确保 attachedLadder 已经拖入了对应的梯子预制体！");
            }

            
        }
    }

    void MaintainFormation()
    {
        if (squadMembers.Count == 0 || targetWall == null) return;

        // 🌟 绝杀 1：算出一条永远指向城墙的“绝对基准轴”！
        // 这样一来，不管指挥官为了避障怎么扭头，整个方阵的朝向永远稳如泰山！
        Vector3 baseForward = targetWall.position - transform.position;
        baseForward.y = 0; // 忽略高度差
        if (baseForward == Vector3.zero) baseForward = Vector3.forward;
        Quaternion stableRotation = Quaternion.LookRotation(baseForward);

        foreach (NavMeshAgent member in squadMembers)
        {
            if (member == null || !member.isActiveAndEnabled || !member.isOnNavMesh) continue;

            if (!localOffsets.ContainsKey(member))
            {
                // 记录相对偏移
                Vector3 offset = transform.InverseTransformPoint(member.transform.position);
                localOffsets.Add(member, offset);
                personalSpeeds.Add(member, member.speed * 1.3f); 
            }

            // 🌟 绝杀 2：用“绝对基准轴”来还原士兵位置，彻底切断大风车甩尾！
            Vector3 targetFormationPos = transform.position + stableRotation * localOffsets[member];
            
            // 距离终点的距离
            float distToTarget = Vector3.Distance(member.transform.position, targetFormationPos);

            // 如果还没到刹车死区
            if (distToTarget > (member.stoppingDistance + 0.5f))
            {
                // 🌟 绝杀 3：寻路防抖（去高频 Spam化）！
                // 只有当目标点比小兵当前大脑里记的目的地，偏离了超过 1 米以上时，才重新下令！
                // 这样小兵就不会每帧都在重新算路了，半路的步伐会丝滑得像抹了黄油！
                if (Vector3.Distance(member.destination, targetFormationPos) > 1.0f)
                {
                    member.SetDestination(targetFormationPos);
                }

                // 追赶机制：掉队太多就加速
                if (distToTarget > 3f) member.speed = personalSpeeds[member];
                else member.speed = baseSpeed;
            }
            else
            {
                // 到达死区，立刻立正
                member.ResetPath(); 
                
                // 看着城墙
                Vector3 lookDir = targetWall.position - member.transform.position;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                    member.transform.rotation = Quaternion.Slerp(member.transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 5f);
            }
        }
    }
}