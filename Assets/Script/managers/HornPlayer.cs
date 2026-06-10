using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HornPlayer : MonoBehaviour
{
    [Header("号角音源")]
    public AudioSource audioSource;

    [Header("号角音效队列（按顺序播放）")]
    public List<AudioClip> hornClips;

    [Tooltip("启动时自动开始播放序列")]
    public bool playOnAwake = true;

    void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            Debug.LogError("HornPlayer: 未指定 AudioSource！");
            enabled = false;
            return;
        }

        audioSource.playOnAwake = false;  // 脚本接管播放控制
    }

    void Start()
    {
        if (playOnAwake && hornClips.Count > 0)
            PlaySequence();
    }

    [ContextMenu("▶ 播放号角序列")]
    public void PlaySequence()
    {
        if (hornClips == null || hornClips.Count == 0)
        {
            Debug.LogWarning("HornPlayer: 号角音效队列为空！");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(PlaySequenceCoroutine());
    }

    private IEnumerator PlaySequenceCoroutine()
    {
        for (int i = 0; i < hornClips.Count; i++)
        {
            audioSource.clip = hornClips[i];
            audioSource.Play();

            // 等待当前号角播完，再播下一个
            yield return new WaitForSeconds(hornClips[i].length);
        }
    }

    [ContextMenu("⏹ 停止播放")]
    public void Stop()
    {
        StopAllCoroutines();
        if (audioSource != null)
            audioSource.Stop();
    }
}
