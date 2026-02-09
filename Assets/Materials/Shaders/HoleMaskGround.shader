Shader "Custom/HoleMaskGround"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Base (RGB)", 2D) = "white" {}
        // _HolePos and _HoleRadius are now Global Shader Properties set by HoleMaskController
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        // MOBİL FIX: fullforwardshadows yerine noshadow kullanarak mobil performansı artır
        #pragma surface surf Standard noshadow
        // MOBİL FIX: Shader target 3.0 yerine 2.0 kullan (Eski cihaz desteği)
        #pragma target 2.0

        sampler2D _MainTex;
        fixed4 _Color;
        float4 _HolePos;
        float _HoleRadius;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 holePosXZ = _HolePos.xz;
            float2 worldPosXZ = IN.worldPos.xz;
            float d = distance(worldPosXZ, holePosXZ);
            
            // Deliğin İÇİNİ kes (d < radius ise negatif olur ve kesilir)
            // d - radius: eğer d < radius ise negatif → kesilir (delik oluşur)
            //             eğer d > radius ise pozitif → görünür (zemin)
            clip(d - _HoleRadius);

            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    // MOBİL FIX: Fallback shader eklendi
    FallBack "Mobile/Diffuse"
}
