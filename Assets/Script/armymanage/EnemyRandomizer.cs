using UnityEngine;
using UnityEngine.AI;

public class EnemyRandomizer : MonoBehaviour
{
    void Start()
    {
        // 1. 随机体型：一点点微调，就能打破整齐划一的方阵感
        float randomScale = Random.Range(200f, 250f);
        transform.localScale = new Vector3(randomScale, randomScale, randomScale);

        // 2. 随机速度：有跑得快的疯狗，有跑得慢的肉盾
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            // 在基础速度上做 20% 的上下浮动
            agent.speed *= Random.Range(0.8f, 1f);
        }

        // // 3. 动画错帧：终极“反广播体操”大招
        // Animator anim = GetComponent<Animator>();
        // if (anim != null)
        // {
        //     // 强制从动画的随机进度（0% 到 100%）开始播放
        //     // 注意：这里的 "Run" 要换成你 Animator 里那个跑步状态的名字
        //     anim.Play("Run", 0, Random.Range(0f, 1f));
        // }
    }
}
