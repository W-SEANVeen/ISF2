using UnityEngine;
using System.Collections;

/// <summary>
/// 战斗全局管理器 —— 处理胜利/失败结算与黑屏转场。
/// 挂在场景中一个常驻 GameObject 上。
/// </summary>
public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    /// <summary>是否已结算（防止重复触发）</summary>
    private bool isGameOver = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ============================================================
    // 战斗结算
    // ============================================================

    /// <summary>
    /// 敌人死亡 → 玩家胜利。
    /// 延迟2秒后黑屏 → 显示"赢了"结算画面。
    /// </summary>
    public void WinCombat()
    {
        if (isGameOver) return;
        isGameOver = true;

        Debug.Log("[CombatManager] 死士死亡 —— 玩家胜利！");

        StartCoroutine(DelayedLoadGameEnd(2f));
    }

    /// <summary>
    /// 玩家死亡 → 玩家失败。
    /// 延迟1.5秒后黑屏 → 显示"被打死"结算画面。
    /// </summary>
    public void LoseCombat()
    {
        if (isGameOver) return;
        isGameOver = true;

        Debug.Log("[CombatManager] 玩家死亡 —— 战斗失败！");

        StartCoroutine(DelayedLoadGameEnd(1.5f));
    }

    private IEnumerator DelayedLoadGameEnd(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (SceneChanger.Instance != null)
            SceneChanger.Instance.LoadGameEndScene();
        else
            Debug.LogError("[CombatManager] SceneChanger.Instance 为空，无法加载结算场景！");
    }
}
