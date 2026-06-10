using UnityEngine;

public class CrossbowShoot : MonoBehaviour
{
    public GameObject arrowPrefab;  // 你的弩箭预制体
    public Transform firePoint;     // 你的枪口位置
    public float shootForce = 2000f; // 射击力度（大黄弩力度必须大！）

    [Header("音效")]
    [Tooltip("对应 AudioManager > AudioCollection 里的 key")]
    public string shootSoundKey = "Arrow_whoosh";
    [Range(0f, 1f)]
    public float shootVolume = 0.8f;

    // 这个就是开火的绝招
    public void Fire()
    {
        // 1. 在枪口位置凭空捏造一根箭出来
        GameObject arrow = Instantiate(arrowPrefab, firePoint.position, firePoint.rotation);

        // 2. 找到这根箭的物理刚体
        Rigidbody rb = arrow.GetComponent<Rigidbody>();

        // 3. 顺着枪口的正前方（Z轴）狠狠踹它一脚！
        rb.AddForce(firePoint.forward * shootForce);

        // 4. 打扫战场：5秒后自动销毁这根箭，防止同屏箭太多把 Pico 4 卡死
        Destroy(arrow, 5f);

        // 🔊 在箭上挂一个独享的 AudioSource，跟随箭飞行，不占用箭雨音效池
        AudioClip clip = AudioManager.Instance?.GetClip(shootSoundKey);
        if (clip != null)
        {
            GameObject audioGo = new GameObject("PlayerArrowAudio");
            audioGo.transform.SetParent(arrow.transform, false);
            audioGo.transform.localPosition = Vector3.zero;

            AudioSource src = audioGo.AddComponent<AudioSource>();
            src.spatialBlend = 0.6f;
            src.minDistance = 5f;
            src.maxDistance = 50f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.clip = clip;
            src.volume = shootVolume;
            src.pitch = Random.Range(0.9f, 1.1f);
            src.Play();

            // 播完后自动销毁，不留下任何垃圾
            Destroy(audioGo, clip.length + 0.2f);
        }
    }
}