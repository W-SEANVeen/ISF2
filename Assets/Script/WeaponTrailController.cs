using UnityEngine;

public class WeaponTrailController : MonoBehaviour
{
    [Header("把你的拖尾组件拖到这里")]
    public TrailRenderer myTrail;

    /// <summary>
    /// 初始化拖尾组件并确保开场关闭发射。
    /// </summary>
    void Start()
    {
        ResetTrail();
    }

    /// <summary>
    /// 开启武器拖尾发射。
    /// </summary>
    public void EnableTrail()
    {
        if (!HasValidTrail())
        {
            return;
        }

        myTrail.Clear();
        myTrail.emitting = true;
    }

    /// <summary>
    /// 关闭武器拖尾发射。
    /// </summary>
    public void DisableTrail()
    {
        if (!HasValidTrail())
        {
            return;
        }

        myTrail.emitting = false;
    }

    /// <summary>
    /// 重置武器拖尾，确保没有残留尾迹。
    /// </summary>
    public void ResetTrail()
    {
        if (!HasValidTrail())
        {
            return;
        }

        myTrail.Clear();
        myTrail.emitting = false;
    }

    /// <summary>
    /// 检查拖尾引用是否完整，缺失时直接报错。
    /// </summary>
    bool HasValidTrail()
    {
        if (myTrail != null)
        {
            return true;
        }

        Debug.LogError("❌ 致命错误：WeaponTrailController 没有绑定 TrailRenderer！请去 Inspector 把武器拖尾组件拖进来！");
        return false;
    }
}
