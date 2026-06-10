using UnityEngine;

public class PorterRetreat : MonoBehaviour
{
    public float runSpeed = 3f; // 跑路速度
    private bool isRetreating = false;

    // 指挥官下达的撤退命令
    public void StartRetreat()
    {
        isRetreating = true;

        // 1. 斩断牵连：脱离指挥官和梯子的父子层级，留在原地
        transform.SetParent(null);

        // 2. 遍历底下的每一个工具人小兵
        foreach (Transform porter in transform)
        {
            // 给每个人一个随机的“逃跑朝向”（背对城墙，向左右散开）
            // 假设 Z 轴正前方是城墙，那就让他们往后方（120度到240度之间）跑
            float randomAngle = Random.Range(120f, 240f);
            porter.localRotation = Quaternion.Euler(0, randomAngle, 0);
            
            // 注意：如果你有跑步的动画，这里应该调用 Animator 播放 "Run" 动画！
            // Animator anim = porter.GetComponent<Animator>();
            // if (anim != null) anim.SetTrigger("Run");
        }

        // 3. 终极性能回收：跑路 3 秒后，直接把这群工具人连锅端掉（模拟水墨消散）
        Destroy(gameObject, 3f);
    }

    void Update()
    {
        if (isRetreating)
        {
            // 让底下的每一个工具人，沿着他们各自面朝的方向（Z轴）狂奔
            foreach (Transform porter in transform)
            {
                porter.Translate(Vector3.forward * runSpeed * Time.deltaTime);
            }
        }
    }
}