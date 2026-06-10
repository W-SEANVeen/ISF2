using UnityEngine;
using System.Collections;

public class RickIgniteController : MonoBehaviour
{
    [Header("特效预制体 (从商店包里拖入)")]
    [Tooltip("刚接触时的初始小火苗")]
    public GameObject startFirePrefab;
    [Tooltip("随后爆出的大面积浓烟/大火")]
    public GameObject mainSmokePrefab;

    [Header("燃烧演出设置")]
    [Tooltip("从碰到火把，到爆出大浓烟需要等待几秒？")]
    public float smokeDelay = 1.2f;
    [Tooltip("指定起火点（比如衣服下摆或脚部）。不填就默认在中心点起火。")]
    public Transform targetBone; 

    // 内部状态控制
    private bool isOnFire = false;
    private GameObject currentStartFire;
    private GameObject currentMainSmoke;

    // 推荐在 VR 中使用 OnTriggerEnter (而不是 OnCollisionEnter)
    // 这样火把扫过 Rick 时不会产生生硬的物理反弹阻力，体验更顺滑
    void OnTriggerEnter(Collider other)
    {
        // 检查是不是被火把（标签为 Igniter）碰到了，并且当前没有着火
        if (!isOnFire && other.CompareTag("Igniter"))
        {
            // 获取碰撞发生的大致位置，作为起火点
            Vector3 contactPoint = other.ClosestPoint(transform.position);
            StartCoroutine(IgniteRoutine(contactPoint));
        }
    }

    private IEnumerator IgniteRoutine(Vector3 contactPoint)
    {
        isOnFire = true;

        // 确定生成位置：如果有指定骨骼就用骨骼，没有就用刚才算出的接触点
        Transform spawnParent = targetBone != null ? targetBone : transform;
        Vector3 spawnPos = targetBone != null ? targetBone.position : contactPoint;

        // 【第一阶段：生成小火】
        // 直接生成商店里的特效，把它作为 Rick 的子物体，这样 Rick 走动时火会跟着走
        if (startFirePrefab != null)
        {
            currentStartFire = Instantiate(startFirePrefab, spawnPos, Quaternion.identity, spawnParent);
        }

        // 等待设定的时间，让火势“蔓延”一会儿
        yield return new WaitForSeconds(smokeDelay);

        // 【第二阶段：爆出大浓烟】
        if (mainSmokePrefab != null)
        {
            // 浓烟通常很大，直接生成在 Rick 的中心点即可
            currentMainSmoke = Instantiate(mainSmokePrefab, transform.position,mainSmokePrefab.transform.rotation, transform);
        }

        // 【第三阶段：熄灭初始小火】
        // 为了过渡自然，不要直接 Destroy。找到粒子组件让它停止发射，自然消散
        if (currentStartFire != null)
        {
            ParticleSystem ps = currentStartFire.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); // 停止发射，包括子特效
            }
            // 延时 2 秒后彻底销毁物体，释放内存 (对 VR 性能很关键)
            Destroy(currentStartFire, 2f); 
        }
    }

    // 留一个灭火接口，以防剧情需要把火浇灭
    public void Extinguish()
    {
        if (!isOnFire) return;

        // 停止并销毁所有特效
        if (currentStartFire != null) Destroy(currentStartFire);
        if (currentMainSmoke != null)
        {
            ParticleSystem ps = currentMainSmoke.GetComponentInChildren<ParticleSystem>();
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(currentMainSmoke, 3f);
        }

        isOnFire = false;
    }
}
