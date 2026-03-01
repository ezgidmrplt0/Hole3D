Shader "Custom/HoleHollowRim"
{
    Properties
    {
        _Color ("Rim Color", Color) = (1, 1, 1, 1)
        _MainTex ("Skin Texture", 2D) = "white" {}
        _InsideRadius ("Inside Radius", Range(0, 1)) = 0.8
        _OutsideRadius ("Outside Radius", Range(0, 2)) = 1.0
        _Softness ("Edge Softness", Range(0, 0.1)) = 0.02
        _RotationSpeed ("Rotation Speed", Float) = 0.0
        _RepeatCount ("Repeat Count", Float) = 1.0
        _EmissionStrength ("Emission Strength", Range(0, 5)) = 1.0
        _UVOffset ("UV Offset", Vector) = (0,0,0,0)
        [Toggle] _UsePlanarMapping ("Use Planar Mapping (Circle PNG)", Float) = 0
        [Toggle] _SwapUVs ("Swap UV Orientation (Polar Only)", Float) = 0
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
            #pragma target 2.0
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
            fixed4 _Color;
            half _InsideRadius;
            half _OutsideRadius;
            half _Softness;
            half _RotationSpeed;
            half _RepeatCount;
            half _EmissionStrength;
            half2 _UVOffset;
            half _UsePlanarMapping;
            half _SwapUVs;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 dir = i.uv - float2(0.5, 0.5);
                float dist = length(dir) * 2.0;

                // Smooth edges using smoothstep instead of hard clip
                float edgeAlpha = smoothstep(_InsideRadius - _Softness, _InsideRadius, dist);
                edgeAlpha *= smoothstep(_OutsideRadius + _Softness, _OutsideRadius, dist);

                // Discard if completely transparent
                if (edgeAlpha <= 0.001) discard;

                float2 finalUV;

                if (_UsePlanarMapping > 0.5)
                {
                    // --- PLANAR MAPPING (Circle PNG) ---
                    float angle = _Time.y * _RotationSpeed;
                    float s = sin(angle);
                    float c = cos(angle);
                    float2x2 rotMat = float2x2(c, -s, s, c);
                    
                    // Rotate and Offset UVs around center (0.5, 0.5)
                    finalUV = mul(rotMat, i.uv - 0.5 - _UVOffset) + 0.5;
                    // Apply tiling and wrap UVs
                    finalUV = frac((finalUV - 0.5) * _RepeatCount + 0.5);
                }
                else
                {
                    // --- POLAR MAPPING (Strip PNG) ---
                    float angle = atan2(dir.y, dir.x) / (2.0 * 3.1415926) + 0.5;
                    angle += _Time.y * _RotationSpeed;

                    float radialDist = (dist - _InsideRadius) / max(0.001, _OutsideRadius - _InsideRadius);
                    
                    if (_SwapUVs > 0.5)
                        finalUV = float2(radialDist, angle * _RepeatCount) + _UVOffset;
                    else
                        finalUV = float2(angle * _RepeatCount, radialDist) + _UVOffset;

                    // Wrap UVs for infinite tiling/rotation
                    finalUV = frac(finalUV);
                }
                
                fixed4 texCol = tex2D(_MainTex, finalUV);
                
                // Final color with emission
                fixed4 finalCol = texCol * _Color * _EmissionStrength;
                finalCol.a = texCol.a * _Color.a * edgeAlpha;

                return finalCol;
            }
            ENDCG
        }
    }
    FallBack "Mobile/Particles/Alpha Blended"
}
