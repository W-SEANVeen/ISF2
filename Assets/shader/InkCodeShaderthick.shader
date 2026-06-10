Shader "GuCheng/InkTransparentCode"
{
    Properties
    {
        // 你的透明底色黑墨贴图（如 PNG 透明通道图）
        _MainTex ("Ink Texture (Transparent BG)", 2D) = "white" {}
        // 墨迹的颜色，默认给你调成死寂的纯黑
        _InkColor ("Ink Color", Color) = (0,0,0,1)
        // 边缘裁切阈值（0~1），越大墨迹越收缩、边缘越锐利
        _Cutoff ("Edge Sharpness", Range(0, 1)) = 0.3
        // 整体不透明度（0~1）
        _Opacity ("Opacity", Range(0, 1)) = 0.9
    }
    SubShader
    {
        // 告诉渲染管线：这是透明物体
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        // 核心混合模式：传统的 Alpha 混合
        Blend SrcAlpha OneMinusSrcAlpha
        // 关闭深度写入，防止半透明遮挡剔除错误
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // 引入 URP 核心库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionHCS  : SV_POSITION;
            };

            // 声明贴图和采样器
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 常量缓冲区（提升性能）
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _InkColor;
                float _Cutoff;
                float _Opacity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // 将顶点从对象空间转换到屏幕裁剪空间
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 对透明底色黑墨贴图进行采样
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 用 smoothstep 做硬边裁切：在 [_Cutoff - 0.05, _Cutoff + 0.05] 区间内锐利过渡
                half alpha = smoothstep(_Cutoff - 0.05, _Cutoff + 0.05, texColor.a);

                // 乘上整体不透明度
                alpha *= _Opacity;

                // 输出咱们定义的纯黑色，以及计算出来的透明度
                return half4(_InkColor.rgb, alpha * _InkColor.a);
            }
            ENDHLSL
        }
    }
}