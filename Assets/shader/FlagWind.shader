Shader "GuCheng/FlagWind_Curved"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color (旗子颜色)", Color) = (0.0, 0.0, 0.0, 1.0) 
        _WindSpeed ("Wind Speed (风速)", Float) = 8.0
        _Frequency ("Wave Frequency (波浪频率)", Float) = 2.0
        _Amplitude ("Wave Amplitude (飘动幅度)", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        
        // 同样关掉背面剔除，保证正反面都能看到
        Cull Off 

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
                // 【新增】：读取模型在 Blender 里做好的弯曲面朝向（法线）
                float3 normal : NORMAL; 
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _BaseColor;
            float _WindSpeed;
            float _Frequency;
            float _Amplitude;

            v2f vert (appdata v)
            {
                v2f o;

                // 1. 依然是那个极其省性能的波浪公式
                float wave = sin(_Time.y * _WindSpeed + (v.vertex.x + v.vertex.y) * _Frequency) * _Amplitude;

                // 【核心修改】：不再是 v.vertex.z += ...
                // 而是将波浪的数值，乘以顶点自身的法线方向（v.normal）。
                // 这样无论你的旗子怎么弯曲，风都会顺着它表面的弧度自然地鼓起来！
                v.vertex.xyz += v.normal * wave * v.uv.x; 
                
                // 3. 转换到屏幕空间
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _BaseColor;
            }
            ENDCG
        }
    }
}