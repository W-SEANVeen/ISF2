using UnityEngine;
using UnityEngine.AI;

public class ArmyBeanScatterer : MonoBehaviour
{
    [Header("撒豆子配置")]
    public GameObject enemyPrefab; // 你的吐蕃小兵预制体
    public int beanCount = 30;     // 一次性撒多少个？(注意 Pico 4 性能！)
    public float scatterRadius = 20f; // 撒豆子的圆盘半径

    void Start()
    {
        // 游戏一运行，啪！直接撒豆子！
        ScatterBeans();
    }

    public void ScatterBeans()
    {
        int successfulSpawns = 0;

        for (int i = 0; i < beanCount; i++)
        {
            // 1. 概率模型：在指定半径的圆内，随机生成一个二维坐标 (均匀撒豆)
            Vector2 randomPoint = Random.insideUnitCircle * scatterRadius;
            
            // 将二维坐标转换到三维世界的 X 和 Z 轴，Y 轴先跟发射器平齐
            Vector3 randomPos = transform.position + new Vector3(randomPoint.x, 0, randomPoint.y);

            // 2. 灵魂校验：用 NavMesh 的探测针，看看这个点下方有没有“蓝色地毯”
            NavMeshHit hit;
            // 5.0f 是探测针的长度，如果在随机点上下 5 米内找到了可走地面，就返回 true
            if (NavMesh.SamplePosition(randomPos, out hit, 5.0f, NavMesh.AllAreas))
            {
                // 3. 啪！把豆子精准地砸在地面上
                GameObject newBean = Instantiate(enemyPrefab, hit.position, Quaternion.identity);
                successfulSpawns++;

                // 自动寻找挂在同一个空物体上的指挥官脚本
                SquadCommander commander = GetComponent<SquadCommander>();
                if (commander != null)
                {
                    commander.squadMembers.Add(newBean.GetComponent<NavMeshAgent>());
                }
            }
        }
        
        Debug.Log($"将军！计划撒 {beanCount} 颗豆子，成功落地 {successfulSpawns} 名死士！");
    }
}