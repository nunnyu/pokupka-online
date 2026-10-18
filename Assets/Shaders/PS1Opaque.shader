// Per-object PS1 shader: vertex grid-snapping + flat unlit shading.
// Use this on all game objects to get the PS1 vertex-wobble look.
Shader "pkill9/PS1Opaque"
{
    Properties
    {
        _BaseMap   ("Texture",    2D)    = "white" {}
        _BaseColor ("Color",      Color) = (1,1,1,1)
        // Higher = less wobble. 80-160 is a good PS1 range.
        _SnapRes   ("Snap Resolution", Float) = 120
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "PS1Opaque"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _SnapRes;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            // Snap clip-space XY to an integer grid — the PS1 vertex-wobble effect.
            float4 SnapToGrid(float4 clip, float res)
            {
                float2 ndc  = clip.xy / clip.w;
                float2 snap = round(ndc * res) / res;
                return float4(snap * clip.w, clip.z, clip.w);
            }

            Varyings Vert(Attributes i)
            {
                Varyings o;
                float4 clip  = TransformObjectToHClip(i.positionOS.xyz);
                o.positionCS = SnapToGrid(clip, _SnapRes);
                o.uv         = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
            }
            ENDHLSL
        }

    }
}
