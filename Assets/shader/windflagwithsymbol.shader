Shader "GuCheng/InkFlagWind"
{
    Properties
    {
        // --- 旗子本体控制 ---
        [HDR] _BaseColor ("Base Color (旗子底色)", Color) = (0.0, 0.0, 0.0, 1.0)
        _MainTex ("Flag Texture (旗子形状：白底黑图)", 2D) = "white" {}
        [HDR] _InkColor ("Flag Ink Color (旗子墨迹染色)", Color) = (0, 0, 0, 1)
        
        // --- 叠加图案控制 ---
        _DecalTex ("Decal Texture (叠加图案：白底黑字)", 2D) = "white" {}
        [HDR] _DecalColor ("Decal Color (图案任意染色)", Color) = (1, 0, 0, 1) // 默认大唐血红
        
        // --- 风力控制 ---
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
                float2 decalUv      : TEXCOORD1; 
                float4 positionHCS  : SV_POSITION;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            TEXTURE2D(_DecalTex);
            SAMPLER(sampler_DecalTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MainTex_ST;
                float4 _InkColor;
                
                float4 _DecalTex_ST;
                float4 _DecalColor;
                
                float _WindSpeed;
                float _Frequency;
                float _Amplitude;
                float _Stagger;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 世界坐标错帧飘动逻辑
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float phase = worldPos.x * _Frequency + worldPos.y * _Frequency * _Stagger;
                float wave = sin(_Time.y * _WindSpeed + phase) * _Amplitude;
                float3 posOS = IN.positionOS.xyz + IN.normalOS * wave * IN.uv.x;

                OUT.positionHCS = TransformObjectToHClip(posOS);
                
                // 计算两张贴图的 UV
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.decalUv = TRANSFORM_TEX(IN.uv, _DecalTex);
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ==========================================
                // 1. 处理旗子本体 (白底黑墨去底)
                // ==========================================
                half4 flagTexColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                // 魔法：1减去红色通道。白(1)变成透明(0)，黑(0)变成不透明(1)
                half flagAlpha = 1.0 - flagTexColor.r; 
                half3 baseFlagColor = lerp(_BaseColor.rgb, _InkColor.rgb, flagAlpha);


                // ==========================================
                // 2. 处理叠加的字/图案 (白底黑字去底 + 染色)
                // ==========================================
                half4 decalTexColor = SAMPLE_TEXTURE2D(_DecalTex, sampler_DecalTex, IN.decalUv);
                // 同样的魔法：用 1 减去把白底变透明，黑字提取成掩码(Mask)
                half decalAlpha = 1.0 - decalTexColor.r; 
                
                
                // ==========================================
                // 3. 终极融合 (将染色后的字印在旗子上)
                // ==========================================
                // 计算字的显示强度。
                // 乘以 _DecalColor.a 让你能在面板上调字的整体透明度
                // 乘以 flagAlpha 是“物理锁”，保证字绝对不会飘在旗子破裂的透明窟窿外面！
                half blendFactor = decalAlpha * _DecalColor.a * flagAlpha; 
                
                // 颜色插值：如果没有字(blendFactor=0)，显示旗子底色；如果有字(blendFactor=1)，显示你配的字色
                half3 finalColor = lerp(baseFlagColor, _DecalColor.rgb, blendFactor);

                // 返回最终颜色，以及旗子本体的整体透明度
                return half4(finalColor, flagAlpha * _InkColor.a);
            }
            ENDHLSL
        }
    }
}