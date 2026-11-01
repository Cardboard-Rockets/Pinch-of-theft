Shader "PinchOfTheft/NightSky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.035, 0.045, 0.065, 1)
        _HorizonColor ("Horizon", Color) = (0.075, 0.09, 0.115, 1)
        _GroundColor ("Ground", Color) = (0.025, 0.03, 0.04, 1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float elevation = normalize(input.direction).y;
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, smoothstep(0.0, 0.75, max(0.0, elevation)));
                sky = lerp(sky, _GroundColor.rgb, smoothstep(0.0, 0.25, max(0.0, -elevation)));
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
