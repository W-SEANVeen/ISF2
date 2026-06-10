Shader "GuCheng/InkTransparentCode"
{
    Properties
    {
        // 你的白底黑墨贴图
        _MainTex ("Ink Texture (White Background)", 2D) = "white" {}
        // 墨迹的颜色，默认给你调成死寂的纯黑
        _InkColor ("Ink Color", Color) = (0,0,0,1)
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
                // 对白底黑墨图进行采样
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 核心魔法：用 1 减去红色通道（白色变0透明，黑色变1不透明）
                half alpha = 1.0 - texColor.r;

                // 输出咱们定义的纯黑色，以及计算出来的透明度
                return half4(_InkColor.rgb, alpha * _InkColor.a);
            }
            ENDHLSL
        }
    }
}