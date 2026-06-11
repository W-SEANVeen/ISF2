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
    public GameObject[] laterWavePrefabs;
    public Transform[] spawnPoints;
    public Transform[] targetWalls;
    [Header("骑兵配置")]
    public GameObject horsePrefab;
    [Tooltip("每批生成几匹马")]
    public int horsesPerBatch = 3;
    [Tooltip("马在指挥官周围撒开的半径")]
    public float horseScatterRadius = 8f;
    [Tooltip("骑兵冲锋目标——城门，到附近后消失（寓意冲入城内）")]
    public Transform targetGate;
    [Tooltip("🆕 骑兵生成间隔（秒），持续生成不再按波次")]
    public float horseSpawnInterval = 8f;

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
    public float waveInterval = 25f;
    public float firstArrowDelay = 5f;
    public float arrowRainInterval = 15f;

    private bool isBattleActive = false;
    private int currentWave = 1;

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
        StartCoroutine(ArmyChargeRoutine());
        StartCoroutine(ArrowRainRoutine());
        StartCoroutine(ContinuousHorseRoutine());

        // 🆕 死士不再 StartBattle 时出发，而是等第一波生成后跟随指定指挥官
        // 避免提前跑到城墙下罚站 (见下方 ArmyChargeRoutine 中 ActivateAndFollow 调用)
        Debug.Log($"🧪 [导演] StartBattle 死士状态: theOneAndOnlyElite={(theOneAndOnlyElite != null ? theOneAndOnlyElite.name : "NULL")} " +
                  $"targetWalls.Length={targetWalls.Length} designatedEliteRouteIndex={designatedEliteRouteIndex}");

        Debug.Log("🚩 战斗开始！统帅发布了进攻命令！");
    }

    IEnumerator ArmyChargeRoutine()
    {
        while (isBattleActive)
        {
            if (currentActiveEnemies >= maxEnemiesOnField)
            {
                yield return new WaitForSeconds(waveInterval);
                continue;
            }

            GameObject[] currentPrefabs = (currentWave == 1) ? wave1Prefabs : laterWavePrefabs;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (currentPrefabs.Length > 0)
                {
                    GameObject prefabToSpawn = currentPrefabs[UnityEngine.Random.Range(0, currentPrefabs.Length)];
                    GameObject newSquad = Instantiate(prefabToSpawn, spawnPoints[i].position, spawnPoints[i].rotation);

                    SquadCommander commander = newSquad.GetComponent<SquadCommander>();

                    if (commander != null && targetWalls.Length > i)
                    {
                        commander.ReceiveOrders(targetWalls[i]);

                        // 🌟🌟🌟 【核心逻辑】：记录第 i 路生成的梯子是谁
                        // 只记录第一波梯子，避免后续波次覆盖引用
                        if (commander.attachedLadder != null && routeLadders[i] == null)
                        {
                            routeLadders[i] = commander.attachedLadder;

                            // 🔥🔥🔥 【死士跟随指定指挥官】第一波生成时，
                            // 让死士跟着 designatedEliteRouteIndex 路的指挥官走
                            if (i == designatedEliteRouteIndex && theOneAndOnlyElite != null)
                            {
                                theOneAndOnlyElite.ActivateAndFollow(commander);
                                // 🆕 双向绑定：让指挥官也知道这个死士，到达城墙时通知它停下
                                commander.followingElite = theOneAndOnlyElite;
                                Debug.Log($"💀 死士已激活并跟随指挥官 [{commander.name}]！（第{i}路）");
                            }
                        }

                        // 🐴 骑兵已改为持续生成（见 ContinuousHorseRoutine），这里不再按波次配马
                    }
                }
            }
            currentWave++;
            yield return new WaitForSeconds(waveInterval);
        }
    }

    // 🐴 在指定位置周围撒马，每匹冲向城门
    void SpawnHorsesAround(Vector3 center)
    {
        if (horsePrefab == null || targetGate == null) return;

        for (int h = 0; h < horsesPerBatch; h++)
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

    /// <summary>
    /// 🆕 骑兵持续生成协程 —— 每隔 horseSpawnInterval 秒在随机出兵点生成一波骑兵，
    /// 不再依赖按波次生成的指挥官。
    /// </summary>
    IEnumerator ContinuousHorseRoutine()
    {
        float firstHorseDelay = 10f; // 战斗开始后等一会儿再出骑兵
        yield return new WaitForSeconds(firstHorseDelay);

        while (isBattleActive)
        {
            if (spawnPoints.Length > 0)
            {
                // 随机选一个出兵点作为中心来散马
                Transform spawnCenter = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                SpawnHorsesAround(spawnCenter.position);
                Debug.Log($"🐴 持续生成骑兵（{horsesPerBatch}匹）在 [{spawnCenter.name}] 周围");
            }
            else if (targetWalls.Length > 0)
            {
                // 保底：如果没有 spawnPoints，就在目标城墙附近散马
                Vector3 midPoint = Vector3.zero;
                foreach (var wall in targetWalls) midPoint += wall.position;
                midPoint /= targetWalls.Length;
                SpawnHorsesAround(midPoint);
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
