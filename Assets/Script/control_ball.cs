using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class control_ball : MonoBehaviour
{
[Header("发射设置")]
    [Tooltip("发射方向（默认向右上方）")]
    public Vector3 launchDirection = new Vector3(1f, 1.5f, 0f);
    
    [Tooltip("发射力度（不要太大）")]
    public float launchForce = 5f;

    private Rigidbody rb;
    private Vector3 startPosition;

    void Start()
    {
        // 获取刚体组件
        rb = GetComponent<Rigidbody>();
        
        // 记录物体的初始位置（比如你设定的原点 0,0,0）
        startPosition = transform.position;

        // 执行第一次发射
        Launch();
    }

    void Update()
    {
        // 每帧检测：如果 y 坐标小于 0
        if (transform.position.y < 0f)
        {
            ResetAndLaunch();
        }
    }

    // 发射逻辑
    private void Launch()
    {
        // 将方向向量标准化（变成长度为1的向量），这样受力大小只由 launchForce 决定
        Vector3 forceDir = launchDirection.normalized;
        
        // 使用 Impulse 模式施加一个瞬间的冲击力
        rb.AddForce(forceDir * launchForce, ForceMode.Impulse);
    }

    // 复位并重新发射逻辑
    private void ResetAndLaunch()
    {
        // 1. 还原位置到初始点
        transform.position = startPosition;

        // 2. 【关键】将刚体的速度和旋转速度清零！
        // 如果不清零，它会带着上一轮下落时的速度继续运动，导致轨迹彻底乱套
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 3. 再次发射
        Launch();
    }
}
