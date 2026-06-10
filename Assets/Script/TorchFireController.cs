using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TorchFireController : MonoBehaviour
{
[SerializeField] 
    private ParticleSystem torchParticles;

    void Start()
    {
        if (torchParticles == null)
        {
            torchParticles = GetComponentInChildren<ParticleSystem>();
        }

        if (torchParticles == null)
        {
            Debug.LogError("【火把报错】找不到 ParticleSystem！请把脚本挂在粒子物体上，或将其拖入槽位中。", this);
            return; 
        }

        SetupTorchFire();
    }

    private void SetupTorchFire()
    {
        // ------------------------------------------------
        // 1. 主模块 (Main Module)
        // ------------------------------------------------
        var main = torchParticles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.8f; 
        main.startColor = new Color(1f, 0.1f, 0.1f, 1f); 
        main.startSize = 0.15f; 
        main.startLifetime = 0.6f; 
        main.startSpeed = 1f; 

        // 【新增：提高最大粒子上限】
        // 如果你打算把发射率调得很高，记得把上限也抬高，防止粒子被强行“掐断”
        main.maxParticles = 300;

        var emission = torchParticles.emission;
        emission.enabled = true;

        emission.rateOverTime = 80f;

        // 【修复1：限制粒子大小】
        // 强制把初始大小缩小到原来的 0.1 倍左右（可根据你的模型比例微调）
        main.startSize = 0.05f; 

        // 【修复2：限制火焰高度】
        // 火焰的高度 = 上升速度 * 存活时间。缩短生命周期，火焰就会在升得太高之前消失。
        // 原默认值通常是 5 秒，这对火把来说太长了，改成 0.5 到 0.8 秒比较合适。
        main.startLifetime = 0.5f; 

        main.startSpeed = 1.2f;

        // ------------------------------------------------
        // 2. 形状模块 (Shape Module)
        // ------------------------------------------------
        var shape = torchParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 5f; 
        shape.radius = 0.1f; 

        // ------------------------------------------------
        // 3. 生命周期大小变化模块 (Size over Lifetime)
        // ------------------------------------------------
        // 【修复3：实现火苗尖端变小消失的效果】
        var sizeOverLifetime = torchParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true; // 开启该模块

        // 用代码创建一条曲线 (AnimationCurve)：时间从 0 到 1，大小从 1 降到 0
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0.0f, 1.0f); // 粒子出生时，保持 1 倍大小 (即上面的 startSize)
        sizeCurve.AddKey(1.0f, 0.02f); // 粒子死亡时，缩小到 0
        
        // 将曲线赋值给模块
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);
    }

    public void Extinguish()
    {
        if (torchParticles != null) torchParticles.Stop();
    }

    public void Ignite()
    {
        if (torchParticles != null) torchParticles.Play();
    }
}
