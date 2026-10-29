// Old low-res CRT TV look for the Full Screen Pass Renderer Feature (Renderer2D)
// The image is rebuilt at a low virtual resolution, then scanlines, phosphor mask,
// colour bleed, fringing, rolling band, noise, flicker and vignette are applied per virtual pixel
Shader "Jam/CRT"
{
    Properties
    {
        _Resolution ("Virtual Resolution (320x180 = game pixels, 160x90 = chunkier)", Vector) = (320, 180, 0, 0)
        _Curvature ("Curvature", Range(0, 0.6)) = 0.18
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.5
        _MaskStrength ("RGB Mask Strength", Range(0, 1)) = 0.3
        _Bleed ("Horizontal Colour Bleed", Range(0, 1)) = 0.35
        _Aberration ("Colour Fringing (virtual pixels)", Range(0, 2)) = 0.5
        _ColorSteps ("Colour Steps (0 = off)", Range(0, 32)) = 0
        _RollSpeed ("Rolling Band Speed", Float) = 0.12
        _RollStrength ("Rolling Band Strength", Range(0, 0.4)) = 0.08
        _NoiseStrength ("Noise", Range(0, 0.3)) = 0.06
        _FlickerStrength ("Flicker", Range(0, 0.2)) = 0.03
        _Vignette ("Vignette", Range(0, 1)) = 0.35
        _Brightness ("Brightness", Range(0.5, 2)) = 1.25
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

            float4 _Resolution;
            float _Curvature;
            float _ScanlineStrength;
            float _MaskStrength;
            float _Bleed;
            float _Aberration;
            float _ColorSteps;
            float _RollSpeed;
            float _RollStrength;
            float _NoiseStrength;
            float _FlickerStrength;
            float _Vignette;
            float _Brightness;
            //Set from code (Shader.SetGlobalFloat), not the material: 0 = normal colour, 1 = black and white (no-eyes body)
            float _JamDesaturate;

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

            // Sample the centre of a virtual pixel
            half3 SamplePixel(float2 pixel)
            {
                float2 uv = (pixel + 0.5) / _Resolution.xy;
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;
            }

            // One virtual pixel with its neighbours bleeding in horizontally
            half3 SampleBled(float2 pixel)
            {
                half3 centre = SamplePixel(pixel);
                half3 left = SamplePixel(pixel - float2(1, 0));
                half3 right = SamplePixel(pixel + float2(1, 0));
                return lerp(centre, (left + centre * 2.0 + right) * 0.25, _Bleed);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = CurveUV(input.texcoord);
                // Outside the curved glass is the black TV bezel
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return half4(0, 0, 0, 1);

                float2 res = max(_Resolution.xy, 1.0);
                float2 pos = uv * res;
                float2 pixel = floor(pos);
                float2 inPixel = frac(pos);

                // Fringing: red and blue guns slightly out of line, in virtual pixels
                float2 fringe = float2(_Aberration, 0);
                half3 col;
                col.r = SampleBled(floor(pos + fringe)).r;
                col.g = SampleBled(pixel).g;
                col.b = SampleBled(floor(pos - fringe)).b;

                if (_ColorSteps >= 2.0)
                    col = floor(col * _ColorSteps + 0.5) / _ColorSteps;

                //Before the scanlines/mask so the CRT tint still shows through
                col = lerp(col, dot(col, half3(0.299, 0.587, 0.114)).xxx, saturate(_JamDesaturate));
                col *= _Brightness;

                // Fat scanlines: bright in the middle of each virtual row, dark gap between rows
                float scan = sin(inPixel.y * PI);
                col *= lerp(1.0, scan, _ScanlineStrength);

                // Each virtual pixel split into R, G, B phosphor stripes
                float stripe = floor(inPixel.x * 3.0);
                half3 mask = stripe < 1.0 ? half3(1, 0.6, 0.6) : (stripe < 2.0 ? half3(0.6, 1, 0.6) : half3(0.6, 0.6, 1));
                col *= lerp(half3(1, 1, 1), mask, _MaskStrength);

                // Slow rolling band
                float band = frac(uv.y + _Time.y * _RollSpeed);
                col *= 1.0 - _RollStrength * (1.0 - smoothstep(0.0, 0.15, band));

                // Static per virtual pixel and whole-screen flicker
                float frame = floor(_Time.y * 24.0);
                col += (Hash(pixel + frame) - 0.5) * _NoiseStrength;
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
