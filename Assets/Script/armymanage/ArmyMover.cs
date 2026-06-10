using UnityEngine;

public class ArmyMover : MonoBehaviour
{
    [Header("绑定指挥部")]
    public SiegeManager siegeManager; // 引用我们刚刚写的总指挥

    [Header("行军路线")]
    public Transform startPoint; // 老家（起点）
    public Transform endPoint;   // 城墙下（终点）

    void Update()
    {
        // 如果没有分配指挥部或者路线，就不执行，防止报错
        if (siegeManager == null || startPoint == null || endPoint == null) return;

        // 核心逃课魔法：Vector3.Lerp (线性插值)
        // 将 0~100 的进度，转换为 0~1 的百分比
        float progressPercentage = siegeManager.siegeProgress / 100f;

        // 让当前物体的位置，严格根据百分比，卡在起点和终点之间
        transform.position = Vector3.Lerp(startPoint.position, endPoint.position, progressPercentage);
        
        // 扩展：如果想让它到终点后触发什么（比如架梯子）
        if (progressPercentage >= 1f)
        {
            // Debug.Log("梯子已架设！第二阶段开始！");
            // 这里以后可以调用架梯子的动画
        }
    }
}