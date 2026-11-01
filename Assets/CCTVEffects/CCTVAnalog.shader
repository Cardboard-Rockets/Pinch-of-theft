Shader "Hidden/PinchOfTheft/CCTVAnalog"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _CCTVParamsA; // grain, scanlines, glitch, blur
            float4 _CCTVParamsB; // saturation, vignette, chromatic, flicker
            float4 _CCTVInfo;    // time, width, height, frame

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 pixel = _CCTVInfo.yz;
                float blur = saturate(_CCTVParamsA.w);
                float2 lowPixel = max(float2(1, 1), floor(pixel / lerp(1, 2.4, blur)));
                float2 sampleUV = lerp(uv, (floor(uv * lowPixel) + 0.5) / lowPixel, blur * 0.65);

                float burst = step(0.975, Hash(float2(floor(_CCTVInfo.x * 1.8), 17)));
                float scanBand = floor(uv.y * pixel.y / 7.0);
                float shift = (Hash(float2(scanBand, floor(_CCTVInfo.x * 13))) - 0.5) * burst * _CCTVParamsA.z * 0.025;
                sampleUV.x = saturate(sampleUV.x + shift);

                float2 center = uv - 0.5;
                float chroma = _CCTVParamsB.z * dot(center, center) * 0.0025;
                half3 color;
                color.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(sampleUV + float2(chroma, 0))).r;
                color.g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV).g;
                color.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(sampleUV - float2(chroma, 0))).b;

                half luma = dot(color, half3(0.2126, 0.7152, 0.0722));
                color = lerp(luma.xxx, color, 1 - saturate(_CCTVParamsB.x));
                float noise = Hash(floor(uv * pixel) + float2(_CCTVInfo.w * 23, _CCTVInfo.w * 47)) - 0.5;
                float scan = sin(uv.y * pixel.y * 3.14159265) * 0.5 + 0.5;
                float staticNoise = (Hash(floor(uv * pixel * 0.5) + floor(_CCTVInfo.x * 25)) - 0.5) * burst;
                color += noise * _CCTVParamsA.x * 0.007;
                color += staticNoise * _CCTVParamsA.z * 0.22;
                color *= 1 - scan * _CCTVParamsA.y * 0.11;
                color *= 1 - dot(center, center) * _CCTVParamsB.y * 0.55;
                color *= 1 + (sin(_CCTVInfo.x * 14.3) * 0.006 + (Hash(float2(floor(_CCTVInfo.x * 9), 5)) - 0.5) * 0.018) * _CCTVParamsB.w;
                return half4(max(color, 0), 1);
            }
            ENDHLSL
        }
    }
}
