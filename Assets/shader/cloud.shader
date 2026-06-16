Shader "GuCheng/RollingInkClouds"
{
    Properties
    {
        _MainTex ("Ink Cloud Texture (白底黑墨云图)", 2D) = "white" {}
        [HDR] _CloudColor ("Cloud Color (乌云颜色)", Color) = (0.1, 0.1, 0.1, 1.0)
        _ScrollSpeed ("Wind Speed (风速 X,Y)", Vector) = (0.02, 0.0, 0, 0)
        
        // 【新增】：云层密度滑块，范围从 0（万里无云）到 5（黑云压城）
        _Density ("Cloud Density (云层密度)", Range(0.0, 5.0)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _CloudColor;
            float2 _ScrollSpeed;
            // 【新增】：在代码里声明密度变量
            float _Density; 

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // 风力注入
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) + _ScrollSpeed * _Time.y;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv);

                // 孤城基础去底大法：白(1)变0，黑(0)变1
                float rawAlpha = 1.0 - texColor.r;

                // 【密度控制魔法】：
                // 将算出的透明度乘以你的密度值。
                // saturate() 函数是保护机制，确保算出来的值就算超过了1，也会被强行压死在1（完全不透明），防止渲染出错。
                float finalAlpha = saturate(rawAlpha * _Density);

                // 输出最终的颜色和算好的厚重透明度
                return fixed4(_CloudColor.rgb, _CloudColor.a * finalAlpha);
            }
            ENDCG
        }
    }
}