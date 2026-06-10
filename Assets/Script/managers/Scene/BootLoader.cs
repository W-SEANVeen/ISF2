using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BootLoader : MonoBehaviour
{
    private void Start()
    {
        SceneChanger.Instance.LoadMenuScene();
    }
}
