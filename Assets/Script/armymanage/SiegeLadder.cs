using UnityEngine;

public class SiegeLadder : MonoBehaviour
{
    [Header("架梯参数")]
    public float targetAngle = 75f; 
    public float erectDuration = 2f; 

    [Header("梯子专属定位器")]
    [Tooltip("拖入梯子底部的空物体，用来给小兵提供位置和身体倾斜校准")]
    public Transform bottomAnchor; 

    private bool isErecting = false;
    private float currentLerpTime = 0f;
    private Quaternion startRotation;
    private Quaternion endRotation;

    void Start()
    {
        startRotation = transform.localRotation;
        endRotation = Quaternion.Euler(targetAngle, 0, 0) * startRotation;
    }

    void Update()
    {
        if (isErecting)
        {
            currentLerpTime += Time.deltaTime;
            float percent = currentLerpTime / erectDuration;
            float smoothPercent = Mathf.SmoothStep(0f, 1f, percent); 

            transform.localRotation = Quaternion.Lerp(startRotation, endRotation, smoothPercent);

            if (percent >= 1f)
            {
                isErecting = false;
                LadderReady();
            }
        }
    }

    public void StartErecting()
    {
        if (!isErecting) isErecting = true;
    }

    private void LadderReady()
    {
        // 🌟 核心：直接呼叫导演，并把自己这个实例传过去，让导演自己去对比
        if (BattleDirector.Instance != null)
        {
            BattleDirector.Instance.OnLadderErected(this);
        }     
    }
}