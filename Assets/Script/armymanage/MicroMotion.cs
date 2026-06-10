using UnityEngine;

public class MicroMotion : MonoBehaviour
{
    float seed;
    void Start() { seed = Random.value * 10f; } // 每个人领一个随机种子，防止动作整齐划一

    void Update()
    {
        // 1. 微小位移扰动（原地左右晃）
        float jitterX = Mathf.Sin(Time.time * 2f + seed) * 0.05f;
        
        // 2. 模拟呼吸/换步的微小缩放（高矮变化）
        float breatheY = 1f + Mathf.Sin(Time.time * 3f + seed) * 0.02f;

        // 应用到本地坐标，不影响全局寻路
        transform.GetChild(0).localPosition = new Vector3(jitterX, 0, 0); // 建议模型作为子物体
        transform.localScale = new Vector3(1, breatheY, 1);
    }
}