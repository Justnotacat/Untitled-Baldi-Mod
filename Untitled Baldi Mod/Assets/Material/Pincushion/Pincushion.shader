Shader "Custom/PincushionDistortion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Distortion Strength", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Strength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float2 Distort(float2 uv)
            {
                float2 c = uv * 2.0 - 1.0;

                float aspect = _ScreenParams.x / _ScreenParams.y;
                c.x *= aspect;

                float r2 = dot(c, c);
                c *= 1.0 + _Strength * r2;

                c.x /= aspect;
                return c * 0.5 + 0.5;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = Distort(i.uv);

                if (any(uv < 0.0) || any(uv > 1.0))
                    return fixed4(0, 0, 0, 1);

                return tex2D(_MainTex, uv);
            }
            ENDHLSL
        }
    }
}