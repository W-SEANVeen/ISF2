using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("音效资源库")]
    public AudioCollectionSO audioCollection;

    [Header("附加音效池")]
    [Tooltip("同时播放的附加音效数量，箭雨场景建议 8~12")]
    public int attachedPoolSize = 8;
    [Range(0f, 1f)]
    public float attachedVolume = 0.8f;
    [Range(0f, 1f)]
    [Tooltip("0=纯2D（全屏出声），1=纯3D（跟位置走）。建议 0.6~0.8，既定位又不会太闷")]
    public float spatialBlend = 0.6f;
    [Tooltip("音效在多少米内保持最大音量")]
    public float minDistance = 10f;
    [Tooltip("音效在多少米外完全听不见")]
    public float maxDistance = 100f;
    public AnimationCurve volumeRolloff = AnimationCurve.Linear(0, 1, 1, 0);

    private Queue<AudioSource> attachedPool = new Queue<AudioSource>();

    void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning($"AudioManager 重复，销毁 {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 初始化附加音效池
        for (int i = 0; i < attachedPoolSize; i++)
        {
            GameObject go = new GameObject($"AttachedAudio_{i}");
            DontDestroyOnLoad(go);                 // 🛡️ 即使临时父级被销毁也不丢失
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;

            AudioSource src = go.AddComponent<AudioSource>();
            src.spatialBlend = spatialBlend;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.rolloffMode = AudioRolloffMode.Custom;
            src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, volumeRolloff);
            src.playOnAwake = false;

            attachedPool.Enqueue(src);
        }
    }

    /// <summary>通过 key 查找音效，找不到则报错</summary>
    public AudioClip GetClip(string key)
    {
        if (audioCollection == null)
        {
            Debug.LogError($"AudioManager：audioCollection 未赋值！无法查找 key=\"{key}\"");
            return null;
        }
        return audioCollection.GetClip(key);
    }

    /// <summary>
    /// 从池借一个 AudioSource，Parent 到目标上播放，播完自动归还。
    /// 目标销毁/隐藏也不怕——池对象有 DontDestroyOnLoad。
    /// </summary>
    public void PlayAttached(string key, Transform parent,
                             float volume = -1f,
                             float pitchMin = 0.9f, float pitchMax = 1.1f)
    {
        AudioClip clip = GetClip(key);
        if (clip == null || attachedPool.Count == 0) return;

        AudioSource src = attachedPool.Dequeue();
        src.clip = clip;
        src.volume = volume >= 0f ? volume : attachedVolume;
        src.pitch = Random.Range(pitchMin, pitchMax);

        // 🎯 Parent 到目标上，自动跟随位置和旋转
        src.transform.SetParent(parent, false);
        src.transform.localPosition = Vector3.zero;
        src.Play();

        // 等音效播完 + 缓冲后自动归还
        StartCoroutine(ReturnWhenDone(src, clip.length + 0.2f));
    }

    private IEnumerator ReturnWhenDone(AudioSource src, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (src != null)   // 防止目标销毁连带炸了池对象（DontDestroyOnLoad 兜底）
        {
            src.Stop();
            src.transform.SetParent(transform);
            src.transform.localPosition = Vector3.zero;
            attachedPool.Enqueue(src);
        }
        else
        {
            Debug.LogWarning("AudioManager：一个附加音效丢失（父级被销毁）");
        }
    }
}
