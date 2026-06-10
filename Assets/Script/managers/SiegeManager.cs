using UnityEngine;

public class SiegeManager : MonoBehaviour
{
    [Header("全局战局数据")]
    [Tooltip("大军推进进度 (0到100，100即为兵临城下)")]
    [Range(0f, 100f)]
    public float siegeProgress = 0f;

    [Tooltip("基础推进速度 (每秒增加的进度百分比)")]
    public float baseAdvanceSpeed = 2f;

    [Tooltip("全局阻尼 (1=全速推进，越小越慢)")]
    public float globalDamping = 1f;

    [Header("压制恢复设置")]
    [Tooltip("被压制后恢复到全速的速度")]
    public float dampingRecoveryRate = 0.5f;

    void Update()
    {
        // 1. 幽灵进度条：随时间自动推进，受阻尼影响
        if (siegeProgress < 100f)
        {
            // 核心公式：实际速度 = 基础速度 * 阻尼
            float currentSpeed = baseAdvanceSpeed * globalDamping;
            siegeProgress += currentSpeed * Time.deltaTime;
            
            // 锁定最高进度为 100
            siegeProgress = Mathf.Clamp(siegeProgress, 0f, 100f);
        }

        // 2. 阻尼恢复系统：如果没有受到新的压制，阻尼会缓慢恢复到 1（满速）
        if (globalDamping < 1f)
        {
            globalDamping += dampingRecoveryRate * Time.deltaTime;
            globalDamping = Mathf.Clamp(globalDamping, 0f, 1f);
        }

        // === 电脑端测试专区 (已避开 XR 模拟器按键) ===
        // 按下回车键 (Return)，模拟玩家用大黄弩射杀了一个敌人
        // if (Input.GetKeyDown(KeyCode.Return))
        // {
        //     TriggerSuppressingFire();
        // }
    }

    // 开放给外部调用的接口：玩家击杀敌人时触发
    public void TriggerSuppressingFire()
    {
        // 瞬间将阻尼降到 0.1（几乎停滞），配合 Update 里的恢复逻辑，形成“顿挫感”
        globalDamping = 0.1f; 
        Debug.Log(" 命中！大军被大黄弩压制！推进减缓！当前阻尼：" + globalDamping);
    }
}