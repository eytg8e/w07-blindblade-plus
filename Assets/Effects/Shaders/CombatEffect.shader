Shader "BlindBlade/CombatEffect"
{
    Properties
    {
        [HDR] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Enum(Disc,0,Ribbon,1,RingMesh,2,BillboardRing,3,ImpactStar,4)] _Shape ("Particle Shape", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 10
        [HDR] _HighlightColor ("Ribbon Highlight", Color) = (0.9, 0.92, 0.94, 1)
        _HighlightWidth ("Ribbon Highlight Reach", Range(0.05, 0.8)) = 0.45
        _BaseStrength ("Ribbon Base Strength", Range(0, 1)) = 0.3
        _FlowSpeed ("Ribbon Flow Speed", Range(0, 8)) = 2
        _DetailStrength ("Ribbon Detail Strength", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Shape;
                float _DstBlend;
                half4 _HighlightColor;
                float _HighlightWidth;
                float _BaseStrength;
                float _FlowSpeed;
                float _DetailStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            // 정점 위치를 화면 좌표로 변환하고 입자 색상과 UV를 전달한다.
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor;
                output.uv = input.uv;
                return output;
            }

            // UV로 원형 입자와 띠 경계를 계산하고 투명 색상을 반환한다.
            half4 Frag(Varyings input) : SV_Target
            {
                float mask = 1.0;
                if (_Shape < 0.5)
                {
                    float2 offset = input.uv * 2.0 - 1.0;
                    mask = pow(saturate(1.0 - dot(offset, offset)), 1.6);
                }
                else if (_Shape < 1.5)
                {
                    float sweep = saturate(input.uv.x);
                    float innerEdge = 0.02 + 0.18 * pow(1.0 - sweep, 1.2);
                    float section = saturate((input.uv.y - innerEdge) / (0.98 - innerEdge));
                    float edge = smoothstep(innerEdge, innerEdge + 0.04, input.uv.y)
                        * (1.0 - smoothstep(0.95, 0.99, input.uv.y));
                    float flow = input.uv.x * 18.0 - _Time.y * _FlowSpeed;
                    float streak = sin(flow + input.uv.y * 26.0) * 0.5 + 0.5;
                    float dissolve = smoothstep(0.0, 0.15 + streak * 0.1, input.color.a);
                    float front = smoothstep(1.0 - _HighlightWidth, 1.0, sweep);
                    float rim = pow(abs(section * 2.0 - 1.0), 3.0);
                    float highlight = front * 1.15 + rim * 0.4;
                    half3 baseColor = input.color.rgb * _BaseStrength
                        * lerp(1.0, streak, _DetailStrength);
                    half3 ribbonColor = baseColor + _HighlightColor.rgb * highlight * input.color.rgb;
                    float alpha = input.color.a * edge * smoothstep(0.0, 0.15, input.uv.x) * dissolve;
                    return half4(ribbonColor, alpha);
                }
                else if (_Shape > 3.5)
                {
                    float2 offset = input.uv * 2.0 - 1.0;
                    float radius = length(offset);
                    float angle = atan2(offset.y, offset.x);
                    float rays = pow(abs(cos(angle * 2.0)), 16.0)
                        + 0.45 * pow(abs(sin(angle * 2.0)), 24.0);
                    mask = pow(saturate(1.0 - radius / (0.2 + 0.8 * rays)), 0.65);
                }
                else if (_Shape > 2.5)
                {
                    float radius = length(input.uv * 2.0 - 1.0);
                    mask = smoothstep(0.68, 0.82, radius) * (1.0 - smoothstep(0.86, 1.0, radius));
                }
                half4 color = input.color;
                color.a *= mask;
                return color;
            }
            ENDHLSL
        }
    }
}
