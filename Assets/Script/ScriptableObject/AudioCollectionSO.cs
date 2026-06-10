using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/AudioCollectionSO", fileName = "AudioCollectionSO")]
public class AudioCollectionSO : ScriptableObject
{
    [System.Serializable]
    public class AudioEntry
    {
        public string key;
        public AudioClip clip;
    }

    public AudioEntry[] entries;

    /// <summary>通过字符串 key 查找音效，找不到则报错并返回 null</summary>
    public AudioClip GetClip(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError($"AudioCollection [{name}]：传入的 key 为空！");
            return null;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].key == key)
                return entries[i].clip;
        }

        Debug.LogError($"AudioCollection [{name}]：找不到 key \"{key}\"，请检查拼写或添加该条目。");
        return null;
    }
}
