Shader "Custom/HoleHollowRim"
{
    Properties
    {
        _Color ("Rim Color", Color) = (0, 0.5, 1, 1)
        _InsideRadius ("Inside Radius", Range(0, 1)) = 0.8
        _OutsideRadius ("Outside Radius", Range(0, 1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
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

            fixed4 _Color;
            float _InsideRadius;
            float _OutsideRadius;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 distVec = i.uv - float2(0.5, 0.5);
                float dist = length(distVec) * 2.0; // Range 0 to 1

                if (dist < _InsideRadius || dist > _OutsideRadius)
                {
                    discard;
                }

                return _Color;
            }
            ENDCG
        }
    }
}
