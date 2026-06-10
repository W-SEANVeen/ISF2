using UnityEngine;
using UnityEngine.AI; // 必须加上这句，才能召唤寻路法术！

public class EnemyCharge : MonoBehaviour
{
    public Transform target; // 你的目标（玩家）的位置
    private NavMeshAgent agent;

    void Start()
    {
        // 游戏刚开始，小兵找到自己身上的“寻路脑子”
        agent = GetComponent<NavMeshAgent>(); 
    }

    void Update()
    {
        // 只要目标还活着，每时每刻都在重新锁定位置，死咬不放！
        if (target != null)
        {
            agent.SetDestination(target.position); 
        }
    }
}