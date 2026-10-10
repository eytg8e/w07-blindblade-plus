Shader "Hidden/BlindBlade/OutlineTransparentDepth"
{
    Properties { _DepthBlendOp ("Depth Blend Operation", Float) = 4 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend One One
            BlendOp [_DepthBlendOp]
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            // 투명 기하의 렌더 정점을 화면 좌표로 변환한다.
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            // 불투명 표면 앞의 투명 기하 깊이를 최근접 값으로 누적한다.
            float4 Frag(Varyings input) : SV_Target
            {
                return float4(input.positionCS.z, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}
