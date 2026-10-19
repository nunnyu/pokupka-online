Shader "Pokupka/Lit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _WorldUVScale ("World UV Scale (0 = mesh UVs)", Float) = 0
        [ToggleUI] _VertexSnap ("Vertex Snap", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _WorldUVScale;
            float _VertexSnap;
        CBUFFER_END

        // Set globally by WorldLook.
        float4 _VertexSnapResolution;
        float4 _SunDirection;
        half4 _SunColor;
        half4 _AmbientColor;
        half4 _FogColor;
        float4 _FogRange;
        float4 _LanternPosition;
        half4 _LanternColor;
        float _Desaturation;
        float _Vignette;
        float _ColorLevels;
        float _AffineStrength;

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_point_repeat_tex);

        half4 SampleBase(float2 uv)
        {
            half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_point_repeat_tex, uv);
            clip(color.a - 0.5);
            return color;
        }

        float4 SnapToPixelGrid(float4 positionCS)
        {
            if (_VertexSnapResolution.y <= 0 || _VertexSnap < 0.5)
            {
                return positionCS;
            }
            float2 grid = _VertexSnapResolution.xy * 0.5;
            positionCS.xy = round(positionCS.xy / positionCS.w * grid) / grid * positionCS.w;
            return positionCS;
        }
        ENDHLSL

        Pass
        {
            Name "Lit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            static const float k_Bayer4x4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 affineUV : TEXCOORD1;
                half3 lighting : TEXCOORD2;
                float fog : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                half3 normalWS : TEXCOORD5;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 positionCS = SnapToPixelGrid(TransformWorldToHClip(positionWS));
                output.positionCS = positionCS;

                float2 uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uv = uv;
                output.positionWS = positionWS;
                // Interpolating uv * w and w, then dividing, cancels perspective correction.
                output.affineUV = float3(uv * positionCS.w, positionCS.w);

                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normalWS = normalWS;
                half nDotL = saturate(dot(normalWS, _SunDirection.xyz));
                output.lighting = _AmbientColor.rgb + _SunColor.rgb * nDotL;

                float distanceToCamera = distance(positionWS, GetCameraPositionWS());
                float fogLength = _FogRange.y - _FogRange.x;
                output.fog = fogLength > 0 ? saturate((distanceToCamera - _FogRange.x) / fogLength) : 0;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 affineUV = input.affineUV.xy / input.affineUV.z;
                float2 uv = lerp(input.uv, affineUV, _AffineStrength);
                if (_WorldUVScale > 0)
                {
                    // Lets one texture span many tiles so the pattern repeats less often.
                    uv = input.positionWS.xz / _WorldUVScale;
                }

                float3 toLantern = _LanternPosition.xyz - input.positionWS;
                float lanternFalloff = _LanternPosition.w > 0 ? saturate(1 - length(toLantern) / _LanternPosition.w) : 0;
                half lanternWrap = saturate(dot(normalize(input.normalWS), normalize(toLantern))) * 0.5 + 0.5;
                half3 lighting = input.lighting + _LanternColor.rgb * lanternFalloff * lanternFalloff * lanternWrap;

                half3 color = SampleBase(uv).rgb * _BaseColor.rgb * lighting;
                half luma = dot(color, half3(0.2126, 0.7152, 0.0722));
                color = lerp(color, luma.xxx, _Desaturation);
                color = lerp(color, _FogColor.rgb, input.fog);

                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy - 0.5;
                color *= 1 - _Vignette * saturate(dot(screenUV, screenUV) * 2);

                if (_ColorLevels > 1)
                {
                    uint2 pixel = (uint2)input.positionCS.xy % 4;
                    float threshold = (k_Bayer4x4[pixel.y * 4 + pixel.x] + 0.5) / 16.0;
                    float steps = _ColorLevels - 1;
                    half3 srgb = LinearToSRGB(saturate(color));
                    srgb = floor(srgb * steps + threshold) / steps;
                    color = SRGBToLinear(saturate(srgb));
                }
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            DepthVaryings depthVert(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = SnapToPixelGrid(TransformObjectToHClip(input.positionOS.xyz));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half depthFrag(DepthVaryings input) : SV_Target
            {
                SampleBase(input.uv);
                return 0;
            }
            ENDHLSL
        }
    }
}
