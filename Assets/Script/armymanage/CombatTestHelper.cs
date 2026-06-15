using UnityEngine;

/// <summary>
/// 战斗调试脚本 —— 挂到任意对象上，按 T 键直接让死士跳到玩家面前开战。
/// 跳过行军、爬梯子等流程，直接测试战斗逻辑。
/// </summary>
public class CombatTestHelper : MonoBehaviour
{
    [Header("调试按键")]
    public KeyCode spawnKey = KeyCode.T;

    [Header("生成参数")]
    [Tooltip("死士出现在玩家前方多远")]
    public float spawnDistance = 3f;

    void Update()
    {
        if (Input.GetKeyDown(spawnKey))
            SpawnEnemyForCombat();
    }

    void SpawnEnemyForCombat()
    {
        // 找玩家位置
        Transform player = null;
        var cam = Camera.main;
        if (cam != null) player = cam.transform;

        if (player == null)
        {
            Debug.LogError("[CombatTest] 找不到玩家位置（Camera.main 为空）");
            return;
        }

        // 找场景中的死士
        var enemy = FindObjectOfType<EnemyAssault>();
        if (enemy == null)
        {
            Debug.LogError("[CombatTest] 场景中找不到 EnemyAssault");
            return;
        }

        var combat = enemy.GetComponent<EnemyCombatController>();
        if (combat == null)
        {
            Debug.LogError("[CombatTest] 死士身上没有 EnemyCombatController");
            return;
        }

        // 在玩家前方生成
        Vector3 spawnPos = player.position + player.forward * spawnDistance;
        var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            agent.Warp(spawnPos);
        }
        else
        {
            enemy.transform.position = spawnPos;
        }

        // 死士 face 玩家
        enemy.transform.rotation = Quaternion.LookRotation(
            new Vector3(player.position.x - enemy.transform.position.x, 0f, player.position.z - enemy.transform.position.z)
        );

        // 激活死士（显示出来）
        enemy.transform.localScale = enemy.transform.localScale == Vector3.zero
            ? Vector3.one
            : enemy.transform.localScale;

        // 直接进入战斗
        combat.BeginCombatPhase(player);

        Debug.Log($"[CombatTest] 死士已在玩家面前，战斗开始！位置={spawnPos}");
    }
}
