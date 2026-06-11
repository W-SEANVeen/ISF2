using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System;
public class BattleDirector : MonoBehaviour
{
    public static BattleDirector Instance;

    [Header("统帅部兵权绑定")]
    public ArrowRainVolley arrowRainManager;

    [Header("攻城波次配置")]
    public GameObject[] wave1Prefabs;
    public Transform[] spawnPoints;
    public Transform[] targetWalls;
    [Header("骑兵配置")]
    public GameObject horsePrefab;
    [Tooltip("马在指挥官周围撒开的半径")]
    public float horseScatterRadius = 8f;
    [Tooltip("骑兵冲锋目标——城门，到附近后消失（寓意冲入城内）")]
    public Transform targetGate;

    [Header("🐴 骑兵容量与节奏")]
    [Tooltip("战场上最多同时存在的骑兵数量")]
    public int maxHorsesOnField = 15;
    [Tooltip("开局立即生成多少骑兵（一波出）")]
    public int initialHorseWaveSize = 10;
    [Tooltip("之后每批补充几匹")]
    public int horsesPerBatch = 3;
    [Tooltip("补充间隔（秒）")]
    public float horseSpawnInterval = 8f;
    [Tooltip("开局后延迟多少秒才开始第一波补充（给初始骑兵冲锋时间）")]
    public float firstHorseDelay = 12f;

    // === 👇 精英死士控制区 👇 ===
    [Header("第二阶段：精英死士单挑")]
    public EnemyAssault theOneAndOnlyElite;
    [Tooltip("指定第几路(0,1,2...)的梯子搭好后，才释放精英死士")]
    public int designatedEliteRouteIndex = 1;

    // 🌟 导演的小本本：记录每一路对应的梯子实例
    private SiegeLadder[] routeLadders;
    private bool hasDeployedElite = false;
    // === 👆 结束 👆 ===

    [Header("动态平衡参数")]
    public int maxEnemiesOnField = 30;
    public int currentActiveEnemies = 0;

    [Header("战争节奏控制")]
    public float firstArrowDelay = 5f;
    public float arrowRainInterval = 15f;

    private bool isBattleActive = false;
    private int currentHorseCount = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 初始化小本本
        routeLadders = new SiegeLadder[spawnPoints.Length];
    }

    void Start()
    {
        Invoke("StartBattle", 3f);
    }

    [ContextMenu("📯 吹响进攻号角！")]
    public void StartBattle()
    {
        if (isBattleActive) return;
        isBattleActive = true;
        SpawnFirstWave();
        StartCoroutine(ArrowRainRoutine());
        StartCoroutine(ContinuousHorseRoutine());

        Debug.Log($"🧪 [导演] StartBattle 死士状态: theOneAndOnlyElite={(theOneAndOnlyElite != null ? theOneAndOnlyElite.name : "NULL")} " +
                  $"targetWalls.Length={targetWalls.Length} designatedEliteRouteIndex={designatedEliteRouteIndex}");

        Debug.Log("🚩 战斗开始！统帅发布了进攻命令！");
    }

    /// <summary>
    /// 开局生成一波步兵，每路一个指挥官。
    /// </summary>
    void SpawnFirstWave()
    {
        if (wave1Prefabs == null || wave1Prefabs.Length == 0) return;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            GameObject prefabToSpawn = wave1Prefabs[UnityEngine.Random.Range(0, wave1Prefabs.Length)];
            GameObject newSquad = Instantiate(prefabToSpawn, spawnPoints[i].position, spawnPoints[i].rotation);

            SquadCommander commander = newSquad.GetComponent<SquadCommander>();

            if (commander != null && targetWalls.Length > i)
            {
                commander.ReceiveOrders(targetWalls[i]);

                // 🌟🌟🌟 【核心逻辑】：记录第 i 路生成的梯子是谁
                if (commander.attachedLadder != null && routeLadders[i] == null)
                {
                    routeLadders[i] = commander.attachedLadder;

                    // 🔥🔥🔥 【死士跟随指定指挥官】
                    if (i == designatedEliteRouteIndex && theOneAndOnlyElite != null)
                    {
                        theOneAndOnlyElite.ActivateAndFollow(commander);
                        commander.followingElite = theOneAndOnlyElite;
                        Debug.Log($"💀 死士已激活并跟随指挥官 [{commander.name}]！（第{i}路）");
                    }
                }
            }
        }

        Debug.Log("📯 第一波步兵已出发！");
    }

    // 🐴 在指定位置周围撒马，每匹冲向城门
    void SpawnHorsesAround(Vector3 center, int count)
    {
        if (horsePrefab == null || targetGate == null) return;

        for (int h = 0; h < count; h++)
        {
            // 随机偏移
            Vector2 randomPoint = UnityEngine.Random.insideUnitCircle * horseScatterRadius;
            Vector3 randomPos = center + new Vector3(randomPoint.x, 0, randomPoint.y);

            // 找 NavMesh 地面
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPos, out hit, 5.0f, NavMesh.AllAreas))
            {
                GameObject horse = Instantiate(horsePrefab, hit.position, Quaternion.identity);
                EnemyHorse horseScript = horse.GetComponent<EnemyHorse>();
                if (horseScript != null)
                {
                    horseScript.receiveorders(targetGate);
                }
            }
        }
    }

    // 🐴 骑兵登记/注销
    public void RegisterHorse()
    {
        currentHorseCount++;
        currentActiveEnemies++;
    }
    public void UnregisterHorse()
    {
        currentHorseCount--;
        currentActiveEnemies--;
    }

    /// <summary>
    /// 🐴 骑兵持续生成协程：
    /// 1. 开局先出一大波 ➜ initialHorseWaveSize 匹
    /// 2. 之后按 horsesPerBatch 慢慢补充，达到 maxHorsesOnField 上限停止
    /// </summary>
    IEnumerator ContinuousHorseRoutine()
    {
        // ========== 第一波：开局大量骑兵 ==========
        if (horsePrefab != null && targetGate != null && spawnPoints.Length > 0)
        {
            int toSpawn = Mathf.Min(initialHorseWaveSize, maxHorsesOnField);
            // 把初始骑兵分散到各个出兵点
            for (int i = 0; i < toSpawn; i++)
            {
                Transform spawnCenter = spawnPoints[i % spawnPoints.Length];
                SpawnHorsesAround(spawnCenter.position, 1);
            }
            Debug.Log($"🐴🐴🐴 开局骑兵潮！初始生成 {toSpawn} 匹，当前场上 {currentHorseCount}/{maxHorsesOnField}");
        }

        // ========== 第二波起：等一会儿再开始持续补充 ==========
        yield return new WaitForSeconds(firstHorseDelay);

        while (isBattleActive)
        {
            // 达到上限 → 跳过本轮
            if (currentHorseCount >= maxHorsesOnField)
            {
                Debug.Log($"🐴 骑兵已达上限({currentHorseCount}/{maxHorsesOnField})，暂停补充");
                yield return new WaitForSeconds(horseSpawnInterval);
                continue;
            }

            if (spawnPoints.Length > 0)
            {
                Transform spawnCenter = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                // 不超过上限
                int batch = Mathf.Min(horsesPerBatch, maxHorsesOnField - currentHorseCount);
                SpawnHorsesAround(spawnCenter.position, batch);
                Debug.Log($"🐴 补充骑兵 {batch}匹 在 [{spawnCenter.name}] 周围，场上 {currentHorseCount}/{maxHorsesOnField}");
            }
            else if (targetWalls.Length > 0)
            {
                Vector3 midPoint = Vector3.zero;
                foreach (var wall in targetWalls) midPoint += wall.position;
                midPoint /= targetWalls.Length;
                int batch = Mathf.Min(horsesPerBatch, maxHorsesOnField - currentHorseCount);
                SpawnHorsesAround(midPoint, batch);
            }

            yield return new WaitForSeconds(horseSpawnInterval);
        }
    }


    public void OnLadderErected(SiegeLadder reporter)
    {
        Debug.Log("================ 🕵️ 导演查岗开始 ================");
        Debug.Log("📢 导演收到汇报！来汇报的梯子是：" + reporter.gameObject.name);

        // 🔍 第一关：检查提早退出（极其容易踩坑）
        if (hasDeployedElite)
        {
            Debug.Log("⛔ 拦截：死士已经释放过了，导演拒绝重复下令！");
            return;
        }
        if (theOneAndOnlyElite == null)
        {
            Debug.Log("❌ 致命错误：导演手里根本没有死士！(theOneAndOnlyElite 为 null，请去 Inspector 检查有没有把死士拖给导演！)");
            return;
        }

        // 🔍 第二关：检查数组和索引状态
        Debug.Log("🎯 导演设定的暗杀路线索引是: [" + designatedEliteRouteIndex + "]");

        if (routeLadders == null || routeLadders.Length == 0)
        {
            Debug.Log("❌ 致命错误：导演的 routeLadders 数组是空的！你是不是忘了在面板里给导演分配梯子了？");
            return;
        }
        if (designatedEliteRouteIndex >= routeLadders.Length)
        {
            Debug.Log("❌ 致命错误：索引越界！指定的路线是 " + designatedEliteRouteIndex + "，但总共只有 " + routeLadders.Length + " 条路线！");
            return;
        }

        SiegeLadder expectedLadder = routeLadders[designatedEliteRouteIndex];
        if (expectedLadder == null)
        {
            Debug.Log("❌ 致命错误：数组里第 [" + designatedEliteRouteIndex + "] 个槽位是空的！请检查 Inspector 面板！");
            return;
        }

        // 🔍 第三关：终极对比（最关键的一步）
        int reporterIndex = Array.IndexOf(routeLadders, reporter);
        string reporterIndexStr = reporterIndex >= 0 ? reporterIndex.ToString() : "（不在数组中！）";
        Debug.Log($"⚖️ 导演对比中... 预期的梯子是: {designatedEliteRouteIndex} | 实际汇报的梯子是: {reporterIndexStr}");

        if (reporter == expectedLadder)
        {
            Debug.Log("✅ 匹配成功！对上暗号了！释放死士！");
            hasDeployedElite = true;
            theOneAndOnlyElite.StartAssaultWithTarget(reporter.bottomAnchor);
        }
        else
        {
            Debug.Log("⛔ 匹配失败：这把梯子不是导演等的那把，导演继续喝茶...");
        }
        Debug.Log("==================================================");
    }

    // 其余箭雨和停止逻辑保持不变...
    IEnumerator ArrowRainRoutine()
    {
        yield return new WaitForSeconds(firstArrowDelay);

        while (isBattleActive)
        {
            if (arrowRainManager != null) arrowRainManager.FireArrowRain();

            float randomInterval = arrowRainInterval + UnityEngine.Random.Range(-3f, 3f);
            yield return new WaitForSeconds(randomInterval);
        }
    }
    public void StopBattle()
    {
        isBattleActive = false;
        StopAllCoroutines();
        Debug.Log("🏳️ 鸣金收兵！");
    }
}
