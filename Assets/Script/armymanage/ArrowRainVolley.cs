using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ArrowRainVolley : MonoBehaviour
{
    [Header("箭雨配置")]
    public GameObject enemyArrowPrefab;
    [Tooltip("【绝杀：出发点盒子】拉到敌军阵地天空，让箭从不同位置出生！")]
    public BoxCollider sourceFiringArea; // 🌟 新增：发射区盒子

    [Tooltip("把这个盒子拉到城墙正上方的天空中，当作云层")]
    public BoxCollider skyCloudBox;
    public LayerMask wallLayer;

    public int arrowCount = 200; // 箭数量
    public float flightTime = 1.8f;
    public float spawnInterval = 0.005f; // 射箭间隔

    // 对象池配置保持不变...
    public int poolSize = 250;
    private Queue<GameObject> arrowPool = new Queue<GameObject>();

    [Header("破空音效")]
    [Tooltip("对应 AudioManager > AudioCollection 里的 key")]
    private string whooshSoundKey = "Arrow";
    [Range(0f, 1f)]
    public float whooshVolume = 0.4f;

    void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject arrow = Instantiate(enemyArrowPrefab, transform.position, Quaternion.identity);
            arrow.SetActive(false);
            ArrowFlight flightScript = arrow.GetComponent<ArrowFlight>();
            if (flightScript != null) flightScript.myPool = this;
            arrowPool.Enqueue(arrow);
        }
    }

    void Start() { }  // AudioManager 接管音效池，此处留空

    [ContextMenu("🚀 呼叫满帧铺天盖地箭雨！")]
    public void FireArrowRain()
    {
        StartCoroutine(SpawnAndLaunchArrows());
    }

    IEnumerator SpawnAndLaunchArrows()
    {
        // 🚨 检查出发点盒子是否挂载
        if (sourceFiringArea == null)
        {
            Debug.LogError("大将！忘了挂【sourceFiringArea】发射区盒子了！箭没法出生！");
            yield break;
        }

        for (int i = 0; i < arrowCount; i++)
        {
            Vector3 preciseStartPos = GetRandomPointInBounds(sourceFiringArea.bounds);
            Vector3? exactTargetPos = GetPreciseTargetOnWall();

            if (exactTargetPos.HasValue)
            {
                GameObject arrow = GetArrowFromPool();

                arrow.transform.position = preciseStartPos;
                arrow.SetActive(true);

                // 🔊 借一个 AudioSource 挂到箭上 → 自动跟随 → 破空呼啸
                AttachWhooshToArrow(arrow.transform);

                Rigidbody rb = arrow.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 launchVelocity = CalculateLaunchVelocity(preciseStartPos, exactTargetPos.Value, flightTime);
                    rb.velocity = launchVelocity;
                    arrow.transform.rotation = Quaternion.LookRotation(launchVelocity);
                }
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    /// <summary>委托 AudioManager 挂一个破空音效到箭上（自动跟随、自动归还）</summary>
    void AttachWhooshToArrow(Transform arrowTransform)
    {
        AudioManager.Instance?.PlayAttached(whooshSoundKey, arrowTransform, whooshVolume);
    }

    private GameObject GetArrowFromPool()
    {
        if (arrowPool.Count == 0)
        {
            GameObject arrow = Instantiate(enemyArrowPrefab, transform.position, Quaternion.identity);
            ArrowFlight flightScript = arrow.GetComponent<ArrowFlight>();
            if (flightScript != null) flightScript.myPool = this;
            return arrow;
        }
        return arrowPool.Dequeue();
    }

    public void ReturnArrowToPool(GameObject arrow)
    {
        // 🔊 AudioManager 会自动回收挂上去的音效，此处无需处理
        arrow.SetActive(false);
        arrowPool.Enqueue(arrow);
    }

    Vector3? GetPreciseTargetOnWall()
    {
        Bounds bounds = skyCloudBox.bounds;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector3 randomSkyPos = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.max.y,
                Random.Range(bounds.min.z, bounds.max.z)
            );
            RaycastHit hit;
            if (Physics.Raycast(randomSkyPos, Vector3.down, out hit, 100f, wallLayer))
            {
                return hit.point;
            }
        }
        return null;
    }

    Vector3 GetRandomPointInBounds(Bounds bounds)
    {
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }

    Vector3 CalculateLaunchVelocity(Vector3 start, Vector3 target, float time)
    {
        Vector3 distance = target - start;
        Vector3 distanceXZ = distance; distanceXZ.y = 0;
        float velocityXZ = distanceXZ.magnitude / time;
        float velocityY = (distance.y - 0.5f * Physics.gravity.y * time * time) / time;
        Vector3 result = distanceXZ.normalized * velocityXZ;
        result.y = velocityY;
        return result;
    }
}
