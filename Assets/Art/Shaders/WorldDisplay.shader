Shader "Hidden/Pokupka/WorldDisplay"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_WorldTexture);
            SAMPLER(sampler_point_clamp_world);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // One triangle that covers the whole screen, built from the vertex index.
            Varyings vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.5, 1.0);
                output.positionCS.y *= _ProjectionParams.x;
                output.uv = uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_WorldTexture, sampler_point_clamp_world, input.uv);
            }
            ENDHLSL
        }
    }
}
