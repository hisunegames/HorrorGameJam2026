// Old CRT TV look for the Full Screen Pass Renderer Feature (Renderer2D)
// Curved glass, scanlines, RGB mask, colour fringing, rolling band, noise, flicker, vignette
Shader "Jam/CRT"
{
    Properties
    {
        _Curvature ("Curvature", Range(0, 0.6)) = 0.18
        _ScanlineCount ("Scanline Count (180 = one per game pixel)", Float) = 180
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.35
        _MaskStrength ("RGB Mask Strength", Range(0, 1)) = 0.12
        _Aberration ("Colour Fringing", Range(0, 0.02)) = 0.003
        _RollSpeed ("Rolling Band Speed", Float) = 0.12
        _RollStrength ("Rolling Band Strength", Range(0, 0.4)) = 0.08
        _NoiseStrength ("Noise", Range(0, 0.3)) = 0.05
        _FlickerStrength ("Flicker", Range(0, 0.2)) = 0.03
        _Vignette ("Vignette", Range(0, 1)) = 0.35
        _Brightness ("Brightness", Range(0.5, 2)) = 1.1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "CRT"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float _Curvature;
            float _ScanlineCount;
            float _ScanlineStrength;
            float _MaskStrength;
            float _Aberration;
            float _RollSpeed;
            float _RollStrength;
            float _NoiseStrength;
            float _FlickerStrength;
            float _Vignette;
            float _Brightness;

            float2 CurveUV(float2 uv)
            {
                uv = uv * 2.0 - 1.0;
                float2 offset = abs(uv.yx) * _Curvature;
                uv = uv + uv * offset * offset;
                return uv * 0.5 + 0.5;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = CurveUV(input.texcoord);
                // Outside the curved glass is the black TV bezel
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return half4(0, 0, 0, 1);

                // Colour fringing, stronger towards the edges
                float2 dir = uv - 0.5;
                half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + dir * _Aberration).r;
                half g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).g;
                half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv - dir * _Aberration).b;
                half3 col = half3(r, g, b) * _Brightness;

                // Scanlines
                float scan = sin(uv.y * _ScanlineCount * PI * 2.0) * 0.5 + 0.5;
                col *= 1.0 - _ScanlineStrength * (1.0 - scan);

                // RGB phosphor mask on screen pixel columns
                float column = fmod(floor(input.texcoord.x * _ScreenParams.x), 3.0);
                half3 mask = column < 1.0 ? half3(1, 0.7, 0.7) : (column < 2.0 ? half3(0.7, 1, 0.7) : half3(0.7, 0.7, 1));
                col *= lerp(half3(1, 1, 1), mask, _MaskStrength);

                // Slow rolling band
                float band = frac(uv.y + _Time.y * _RollSpeed);
                col *= 1.0 - _RollStrength * (1.0 - smoothstep(0.0, 0.15, band));

                // Static noise and whole-screen flicker
                float frame = floor(_Time.y * 24.0);
                col += (Hash(floor(uv * _ScreenParams.xy) + frame) - 0.5) * _NoiseStrength;
                col *= 1.0 - _FlickerStrength * Hash(float2(frame, 7.0));

                // Vignette
                float vig = uv.x * uv.y * (1.0 - uv.x) * (1.0 - uv.y) * 16.0;
                col *= pow(saturate(vig), _Vignette);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
