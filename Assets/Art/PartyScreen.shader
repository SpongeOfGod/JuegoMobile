Shader "Gluttony/PartyScreen"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Pass
        {
            Name "PartyScreen"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _Grid;
            float _Beat;
            float _Wave;
            float _Split;
            float _Rainbow;
            float _Scan;
            float _Track;
            float _TrackAmount;
            float _Static;
            float _Sepia;
            float _Vignette;

            float Random(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float3 HueColor(float hue)
            {
                return saturate(abs(frac(hue + float3(0.0, 0.667, 0.333)) * 6.0 - 3.0) - 1.0);
            }

            float3 Read(float2 cell, float offset)
            {
                float2 uv = frac((cell + 0.5 + float2(offset, 0.0)) / _Grid.xy);
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 cell = floor(input.texcoord * _Grid.xy);
                float wobbleX = sin(cell.y * 0.09 + _Beat * 1.57) * _Wave + sin(cell.y * 0.31 - _Beat * 0.8) * _Wave * 0.4;
                float wobbleY = sin(cell.x * 0.12 + _Beat * 1.1) * _Wave * 0.6;
                if (abs(input.texcoord.y - _Track) < 0.03)
                    wobbleX += (Random(float2(cell.y, floor(_Beat * 8.0))) - 0.5) * _TrackAmount;
                float2 source = cell + round(float2(wobbleX, wobbleY));

                float3 color = float3(Read(source, _Split).r, Read(source, 0.0).g, Read(source, -_Split).b);
                float hue = (cell.x + cell.y) * 0.008 + sin(cell.y * 0.05 + _Beat * 0.5) * 0.15 + _Beat * 0.12;
                float redness = saturate((color.r - max(color.g, color.b)) * 4.0);
                color = lerp(color, HueColor(hue) * max(color.r, 0.5), redness * _Rainbow);
                float gray = dot(color, float3(0.299, 0.587, 0.114));
                color = lerp(color, gray * float3(1.12, 0.86, 0.6), _Sepia * (1.0 - redness));
                color *= 1.0 - _Scan * fmod(cell.y, 2.0);
                color = lerp(color, Random(cell + floor(_Beat * 16.0)).xxx, _Static);
                float edge = floor(saturate((length((cell + 0.5) / _Grid.xy - 0.5) - 0.3) / 0.4) * 4.0) / 4.0;
                color *= 1.0 - edge * 0.7 * _Vignette;
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
