using UnityEngine;

public class ArmyAdvance : MonoBehaviour
{
    [Header("大军推进速度 (米/秒)")]
    public float advanceSpeed = 0.5f; 

    void Update()
    {
        // 假设孤城在中心 (0,0,0)，大军在 Z=150 的位置
        // Vector3.back 就是 (0, 0, -1)，也就是让大军缓慢朝着 0 的方向压过来
        transform.Translate(Vector3.back * advanceSpeed * Time.deltaTime);
    }
}
