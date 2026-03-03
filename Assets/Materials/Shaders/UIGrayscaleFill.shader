Shader "UI/GrayscaleFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FillAmount ("Fill Amount", Range(0,1)) = 0.5
        _GrayscaleAmount ("Grayscale Amount", Range(0,1)) = 1.0
        _AlphaEmpty ("Alpha Empty Area", Range(0,1)) = 0.3
        
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [ZTest]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            fixed4 _Color;
            sampler2D _MainTex;
            float _FillAmount;
            float _GrayscaleAmount;
            float _AlphaEmpty;
            float4 _ClipRect;
            float4 _UIMaskSoftnessX; // Some versions might use these
            float4 _UIMaskSoftnessY;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, IN.texcoord) * IN.color;
                
                // Grayscale calculation
                float gray = dot(col.rgb, float3(0.299, 0.587, 0.114));
                fixed3 grayCol = fixed3(gray, gray, gray);
                
                // Check if current pixel is within fill amount (Horizontal Left to Right)
                if (IN.texcoord.x > _FillAmount)
                {
                    // Exterior (Empty) area: Always grayscale and semi-transparent
                    col.rgb = grayCol;
                    col.a *= _AlphaEmpty; 
                }
                else
                {
                    // Interior (Filled) area: Lerp between color and grayscale based on _GrayscaleAmount
                    col.rgb = lerp(col.rgb, grayCol, _GrayscaleAmount);
                }
                
                // Unity UI standard clipping/masking
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                
                return col;
            }
            ENDCG
        }
    }
}
