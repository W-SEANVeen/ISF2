Shader "GuCheng/InkFlagWind"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color (旗子底色)", Color) = (0.0, 0.0, 0.0, 1.0)
        _MainTex ("Ink Texture (白底黑墨贴图)", 2D) = "white" {}
        [HDR] _InkColor ("Ink Color (墨迹颜色)", Color) = (0, 0, 0, 1)
        _WindSpeed ("Wind Speed (风速)", Float) = 8.0
        _Frequency ("Wave Frequency (波浪频率)", Float) = 2.0
        _Amplitude ("Wave Amplitude (飘动幅度)", Float) = 0.5
        _Stagger ("Frame Stagger (错帧度)", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionHCS  : SV_POSITION;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MainTex_ST;
                float4 _InkColor;
                float _WindSpeed;
                float _Frequency;
                float _Amplitude;
                float _Stagger;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 世界坐标错帧飘动：基于世界坐标计算相位，使不同位置产生错帧效果
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float phase = worldPos.x * _Frequency + worldPos.y * _Frequency * _Stagger;
                float wave = sin(_Time.y * _WindSpeed + phase) * _Amplitude;
                float3 posOS = IN.positionOS.xyz + IN.normalOS * wave * IN.uv.x;

                OUT.positionHCS = TransformObjectToHClip(posOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 采样白底黑墨贴图
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 红色通道反相得到透明度（白色→透明，黑色→不透明）
                half inkAlpha = 1.0 - texColor.r;

                // 颜色混合：白底部分显示旗子底色，墨迹部分显示墨色
                half3 color = lerp(_BaseColor.rgb, _InkColor.rgb, inkAlpha);

                return half4(color, inkAlpha * _InkColor.a);
            }
            ENDHLSL
        }
    }
}
