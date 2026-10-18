Shader "pkill9/PS1Post"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "PS1Post"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Exposed via material properties
            float _PixelRows;    // vertical pixel count, e.g. 240
            float _ColorBits;    // bits per channel, e.g. 5 (PS1 = 5-bit)
            float _ScanlineStr;  // scanline darkness [0,1]
            float _DitherStr;    // dither strength [0,1]

            // Bayer 8x8 ordered dither matrix
            float Bayer8(int2 p)
            {
                static const int M[64] = {
                     0, 32,  8, 40,  2, 34, 10, 42,
                    48, 16, 56, 24, 50, 18, 58, 26,
                    12, 44,  4, 36, 14, 46,  6, 38,
                    60, 28, 52, 20, 62, 30, 54, 22,
                     3, 35, 11, 43,  1, 33,  9, 41,
                    51, 19, 59, 27, 49, 17, 57, 25,
                    15, 47,  7, 39, 13, 45,  5, 37,
                    63, 31, 55, 23, 61, 29, 53, 21
                };
                return M[(p.x & 7) | ((p.y & 7) << 3)] / 64.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // --- Pixelation: snap UV to PS1-res grid ---
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 grid  = float2(floor(_PixelRows * aspect), _PixelRows);
                float2 uv    = floor(input.texcoord * grid) / grid;

                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // --- Ordered dithering + colour quantisation ---
                int2  px        = int2(input.texcoord * _ScreenParams.xy);
                float threshold = (Bayer8(px) - 0.5) * _DitherStr;
                float levels    = pow(2.0, _ColorBits) - 1.0;
                col.rgb = saturate(round(col.rgb * levels + threshold) / levels);

                // --- Scanlines at display resolution ---
                col.rgb *= 1.0 - _ScanlineStr * float(px.y & 1);

                return col;
            }
            ENDHLSL
        }
    }
}
